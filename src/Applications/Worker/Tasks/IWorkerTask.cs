namespace Mnemonios.Worker.Tasks;

/// <summary>
/// Интерфейс периодической задачи планировщика.
/// </summary>
public interface IWorkerTask
{
    /// <summary>Уникальный идентификатор задачи (для логов и конфигурации).</summary>
    string TaskId { get; }

    /// <summary>Читаемое имя задачи.</summary>
    string TaskName { get; }

    /// <summary>Выполнить задачу.</summary>
    /// <param name="ct">Токен отмены.</param>
    Task ExecuteAsync(CancellationToken ct);
}
