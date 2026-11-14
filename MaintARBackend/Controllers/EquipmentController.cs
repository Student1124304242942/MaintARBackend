using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaintARBackend.Context;
using MaintARBackend.Models;

namespace MaintARBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "EmailVerified")]
    public class EquipmentController : ControllerBase
    {
        private readonly AppDbContext _db;

        public EquipmentController(AppDbContext db)
        {
            _db = db;
        }

        // value — строка из QR-кода, серийный номер или маркировка. Ищем по Code или SerialNumber.
        [HttpGet("identify")]
        public async Task<IActionResult> Identify([FromQuery] string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return BadRequest("Value is required");
            }

            var normalized = value.Trim();

            var equipment = await _db.Equipment
                .AsNoTracking()
                .Include(e => e.EquipmentType)
                .FirstOrDefaultAsync(e => e.Code == normalized || e.SerialNumber == normalized);

            if (equipment == null)
            {
                return NotFound("Equipment not found");
            }

            var info = new EquipmentInfoDto
            {
                Id = equipment.Id,
                Code = equipment.Code,
                SerialNumber = equipment.SerialNumber,
                Name = equipment.Name,
                Model = equipment.Model,
                Manufacturer = equipment.Manufacturer,
                Type = equipment.EquipmentType.Name,
                Location = equipment.Location
            };

            // Берём последний завершённый осмотр вместе с находками.
            var lastInspection = await _db.Inspections
                .AsNoTracking()
                .Where(i => i.EquipmentId == equipment.Id && i.Status == InspectionStatus.Completed)
                .OrderByDescending(i => i.CreatedAt)
                .Include(i => i.Findings).ThenInclude(f => f.Component)
                .Include(i => i.Findings).ThenInclude(f => f.DefectType)
                .FirstOrDefaultAsync();

            if (lastInspection == null)
            {
                return Ok(new EquipmentReportDto
                {
                    Status = EquipmentReportStatus.NotInspected,
                    Message = "Оборудование ещё не проверялось",
                    Equipment = info
                });
            }

            var problems = lastInspection.Findings
                .Where(f => f.Category != FindingCategory.NoVisibleProblems)
                .ToList();

            if (problems.Count == 0)
            {
                return Ok(new EquipmentReportDto
                {
                    Status = EquipmentReportStatus.Ok,
                    Message = "Всё в порядке, дефектов не обнаружено",
                    Equipment = info,
                    LastInspectionDate = lastInspection.CreatedAt
                });
            }

            var defects = problems
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

            return Ok(new EquipmentReportDto
            {
                Status = EquipmentReportStatus.HasDefects,
                Message = $"Обнаружены дефекты в узлах: {defects.Count}",
                Equipment = info,
                LastInspectionDate = lastInspection.CreatedAt,
                Defects = defects
            });
        }
    }
}
