using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mnemonios.Domain.Interfaces;

namespace Mnemonios.Worker.Tasks;

/// <summary>
/// Периодическая задача удаления записей, к которым прекращена обработка ПДн.
/// 1. Обрабатывает отложенные отзывы (date &lt;= NOW) → создаёт пометки cessation.
/// 2. Удаляет помеченные записи и золотые записи без оставшихся внешних ссылок.
/// </summary>
public sealed class ReconcileCessationsTask : IWorkerTask
{
    /// <inheritdoc/>
    public string TaskId => "reconcile-cessations";

    /// <inheritdoc/>
    public string TaskName => "Reconcile Cessations";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReconcileCessationsTask> _logger;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="ReconcileCessationsTask"/>.
    /// </summary>
    public ReconcileCessationsTask(
        IServiceScopeFactory scopeFactory,
        ILogger<ReconcileCessationsTask> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var cessationService = scope.ServiceProvider.GetRequiredService<IPersonCessationService>();

        // Фаза 1: обработать отложенные отзывы с наступившей датой
        var deferredCount = await cessationService.ProcessDeferredCessationsAsync(ct);
        _logger.LogInformation("Обработано отложенных отзывов: {Count}", deferredCount);

        // Фаза 2: удалить помеченные записи и осиротевшие золотые записи
        var reconciledCount = await cessationService.ReconcileAsync(ct);
        _logger.LogInformation("Реконсилизировано записей: {Count}", reconciledCount);
    }
}
