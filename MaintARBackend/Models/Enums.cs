namespace MaintARBackend.Models;

/// <summary>Для чего выдан код из письма.</summary>
public enum CodePurpose
{
    EmailVerification,
    PasswordReset
}

/// <summary>Состояние проверки: анализ на сервере ИИ может идти долго или упасть.</summary>
public enum InspectionStatus
{
    Pending,
    Completed,
    Failed
}

/// <summary>Каким способом было определено оборудование (п. 4.1 задания).</summary>
public enum IdentificationMethod
{
    QrCode,
    SerialNumber,
    Marking,
    Image,
    Manual
}

/// <summary>Категория результата ИИ по узлу (п. 4.2 задания).</summary>
public enum FindingCategory
{
    /// <summary>Видимых проблем не обнаружено.</summary>
    NoVisibleProblems,

    /// <summary>Рекомендуется проверка.</summary>
    CheckRecommended,

    /// <summary>Обнаружены признаки возможного дефекта.</summary>
    PossibleDefect
}

/// <summary>Что инженер сделал с узлом (п. 4.6 задания).</summary>
public enum MaintenanceType
{
    Checked,
    Serviced,
    Replaced,
    Note
}
