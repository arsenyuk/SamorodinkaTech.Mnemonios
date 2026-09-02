namespace Mnemonios.Worker.Configuration;

/// <summary>
/// Конфигурация фонового планировщика задач.
/// </summary>
public class WorkerConfig
{
    /// <summary>Список задач планировщика.</summary>
    public List<TaskConfig> Tasks { get; set; } = [];
}

/// <summary>
/// Конфигурация одной периодической задачи.
/// </summary>
public class TaskConfig
{
    /// <summary>Уникальный идентификатор задачи.</summary>
    public required string Id { get; set; }

    /// <summary>Читаемое имя задачи для логов.</summary>
    public required string Name { get; set; }

    /// <summary>Cron-выражение (5 полей: minute hour dayOfMonth month dayOfWeek).</summary>
    public required string CronExpression { get; set; }

    /// <summary>Включена ли задача.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Таймаут выполнения в секундах.</summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>Интервал повтора при ошибке (в минутах).</summary>
    public int RetryIntervalMinutes { get; set; } = 5;
}
