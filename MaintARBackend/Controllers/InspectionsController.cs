using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaintARBackend.Context;
using MaintARBackend.Models;
using MaintARBackend.Services.Ai;

namespace MaintARBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "EmailVerified")]
    public class InspectionsController : ControllerBase
    {
        private const long MaxPhotoSize = 10 * 1024 * 1024;

        private readonly AppDbContext _db;
        private readonly IAiAnalysisClient _ai;
        private readonly ILogger<InspectionsController> _logger;

        public InspectionsController(AppDbContext db, IAiAnalysisClient ai,
            ILogger<InspectionsController> logger)
        {
            _db = db;
            _ai = ai;
            _logger = logger;
        }

        // Новая проверка: фото + код/серийник -> определяем оборудование -> ИИ -> сохраняем находки -> отдаём результат.
        [HttpPost]
        [RequestSizeLimit(MaxPhotoSize + 1024 * 1024)]
        public async Task<IActionResult> Create([FromForm] CreateInspectionModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (model.Photo.Length == 0 || model.Photo.Length > MaxPhotoSize)
            {
                return BadRequest("Photo must be between 1 byte and 10 MB");
            }

            if (!model.Photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("File must be an image");
            }

            var value = model.Value.Trim();
            var equipment = await _db.Equipment
                .Include(e => e.EquipmentType).ThenInclude(t => t.DefectTypes)
                .Include(e => e.Components)
                .FirstOrDefaultAsync(e => e.Code == value || e.SerialNumber == value, ct);

            if (equipment == null)
            {
                return NotFound("Equipment not found");
            }

            // Фото не сохраняем — только передаём в ИИ.
            var inspection = new Inspection
            {
                UserId = GetUserId(),
                EquipmentId = equipment.Id,
                CreatedAt = DateTime.UtcNow,
                Status = InspectionStatus.Pending,
                IdentificationMethod = model.IdentificationMethod
            };
            _db.Inspections.Add(inspection);
            await _db.SaveChangesAsync(ct);

            try
            {
                var request = new AiAnalysisRequest
                {
                    EquipmentId = equipment.Id,
                    EquipmentType = equipment.EquipmentType.Name,
                    Model = equipment.Model,
                    Components = equipment.Components.Select(c => new AiComponentDto { Id = c.Id, Name = c.Name }).ToList(),
                    DefectTypes = equipment.EquipmentType.DefectTypes.Select(d => new AiDefectTypeDto { Code = d.Code, Name = d.Name }).ToList()
                };

                await using var photo = model.Photo.OpenReadStream();
                var result = await _ai.AnalyzeAsync(photo, model.Photo.FileName, model.Photo.ContentType, request, ct);

                inspection.RawAiResponse = result.RawJson;

                var componentIds = equipment.Components.Select(c => c.Id).ToHashSet();
                var defectIdsByCode = equipment.EquipmentType.DefectTypes.ToDictionary(d => d.Code, d => d.Id);

                foreach (var f in result.Response.Findings)
                {
                    // Находки по чужим/несуществующим узлам пропускаем, они останутся в RawAiResponse.
                    if (!componentIds.Contains(f.ComponentId))
                    {
                        _logger.LogWarning("AI returned unknown component {ComponentId} for inspection {InspectionId}", f.ComponentId, inspection.Id);
                        continue;
                    }

                    inspection.Findings.Add(new InspectionFinding
                    {
                        ComponentId = f.ComponentId,
                        DefectTypeId = f.DefectCode != null && defectIdsByCode.TryGetValue(f.DefectCode, out var defectId) ? defectId : null,
                        Category = f.Category,
                        Confidence = f.Confidence,
                        Explanation = f.Explanation,
                        Recommendation = f.Recommendation,
                        BoxX = f.Box?.X,
                        BoxY = f.Box?.Y,
                        BoxWidth = f.Box?.Width,
                        BoxHeight = f.Box?.Height
                    });
                }

                inspection.Status = InspectionStatus.Completed;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
            {
                _logger.LogError(ex, "AI analysis failed for inspection {InspectionId}", inspection.Id);
                inspection.Status = InspectionStatus.Failed;
            }

            await _db.SaveChangesAsync(CancellationToken.None);

            // Отдаём результат в том же виде, что и GET /api/Inspections/{id}.
            return await GetInspection(inspection.Id);
        }

        // История проверок текущего пользователя, новые сверху.
        [HttpGet("my")]
        public async Task<IActionResult> GetMyInspections([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var userId = GetUserId();

            var query = _db.Inspections
                .AsNoTracking()
                .Where(i => i.UserId == userId);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new InspectionSummaryDto
                {
                    Id = i.Id,
                    CreatedAt = i.CreatedAt,
                    Status = i.Status,
                    IdentificationMethod = i.IdentificationMethod,
                    EquipmentId = i.EquipmentId,
                    EquipmentCode = i.Equipment.Code,
                    EquipmentName = i.Equipment.Name,
                    EquipmentModel = i.Equipment.Model,
                    DefectsCount = i.Findings.Count(f => f.Category != FindingCategory.NoVisibleProblems)
                })
                .ToListAsync();

            foreach (var item in items)
            {
                item.Result = GetResult(item.Status, item.DefectsCount);
            }

            return Ok(new PagedResult<InspectionSummaryDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = items
            });
        }

        // Подробности одной проверки. Чужие проверки не отдаём.
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetInspection(int id)
        {
            var userId = GetUserId();

            var inspection = await _db.Inspections
                .AsNoTracking()
                .Include(i => i.Equipment)
                .Include(i => i.Findings).ThenInclude(f => f.Component)
                .Include(i => i.Findings).ThenInclude(f => f.DefectType)
                .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

            if (inspection == null)
            {
                return NotFound("Inspection not found");
            }

            var defectsCount = inspection.Findings.Count(f => f.Category != FindingCategory.NoVisibleProblems);

            var components = inspection.Findings
                .GroupBy(f => f.Component)
                .Select(g => new ComponentDefectsDto
                {
                    ComponentId = g.Key.Id,
                    ComponentName = g.Key.Name,
                    Findings = g.Select(f => new DefectDto
                    {
                        Category = f.Category,
                        DefectCode = f.DefectType?.Code,
                        DefectName = f.DefectType?.Name,
                        DefectDescription = f.DefectType?.Description,
                        Confidence = f.Confidence,
                        Explanation = f.Explanation,
                        Recommendation = f.Recommendation
                    }).ToList()
                })
                .ToList();

            return Ok(new InspectionDetailsDto
            {
                Summary = new InspectionSummaryDto
                {
                    Id = inspection.Id,
                    CreatedAt = inspection.CreatedAt,
                    Status = inspection.Status,
                    IdentificationMethod = inspection.IdentificationMethod,
                    EquipmentId = inspection.EquipmentId,
                    EquipmentCode = inspection.Equipment.Code,
                    EquipmentName = inspection.Equipment.Name,
                    EquipmentModel = inspection.Equipment.Model,
                    DefectsCount = defectsCount,
                    Result = GetResult(inspection.Status, defectsCount)
                },
                PhotoPath = inspection.PhotoPath,
                Components = components
            });
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        private static EquipmentReportStatus? GetResult(InspectionStatus status, int defectsCount)
        {
            if (status != InspectionStatus.Completed)
            {
                return null;
            }

            return defectsCount > 0 ? EquipmentReportStatus.HasDefects : EquipmentReportStatus.Ok;
        }
    }
}
