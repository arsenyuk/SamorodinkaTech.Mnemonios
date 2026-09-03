using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mnemonios.Worker.Configuration;
using Mnemonios.Worker.Tasks;

namespace Mnemonios.Worker.Scheduling;

/// <summary>
/// Фоновый планировщик периодических задач. Опрос каждую секунду,
/// выполнение по cron-расписанию с защитой от параллельного запуска.
/// </summary>
public sealed class WorkerTaskScheduler : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkerTaskScheduler> _logger;
    private readonly List<ScheduledTaskInfo> _tasks = [];
    private readonly HashSet<string> _runningTasks = [];
    private readonly object _lock = new();

    /// <summary>
    /// Создаёт экземпляр планировщика.
    /// </summary>
    public WorkerTaskScheduler(
        IServiceProvider serviceProvider,
        IOptions<WorkerConfig> config,
        ILogger<WorkerTaskScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        foreach (var taskConfig in config.Value.Tasks.Where(t => t.Enabled))
        {
            _tasks.Add(new ScheduledTaskInfo
            {
                Config = taskConfig,
                CronExpression = new CronExpression(taskConfig.CronExpression),
                NextExecution = new CronExpression(taskConfig.CronExpression).GetNextExecution(DateTime.UtcNow)
            });

            _logger.LogInformation("Зарегистрирована задача: {Name} (ID: {Id}), cron: {Cron}",
                taskConfig.Name, taskConfig.Id, taskConfig.CronExpression);
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Планировщик запущен, задач: {Count}", _tasks.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            foreach (var taskInfo in _tasks)
            {
                if (now < taskInfo.NextExecution)
                    continue;

                lock (_lock)
                {
                    if (_runningTasks.Contains(taskInfo.Config.Id))
                    {
                        _logger.LogWarning("Задача '{Name}' уже выполняется, пропуск", taskInfo.Config.Name);
                        continue;
                    }

                    _runningTasks.Add(taskInfo.Config.Id);
                }

                _ = ExecuteTaskAsync(taskInfo, stoppingToken);
            }

            try
            {
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Планировщик остановлен");
    }

    private async Task ExecuteTaskAsync(ScheduledTaskInfo taskInfo, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Задача '{Name}' начата", taskInfo.Config.Name);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(taskInfo.Config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            using var scope = _serviceProvider.CreateScope();
            var task = scope.ServiceProvider.GetRequiredKeyedService<IWorkerTask>(taskInfo.Config.Id);
            await task.ExecuteAsync(linkedCts.Token);

            var next = taskInfo.CronExpression.GetNextExecution(DateTime.UtcNow);
            taskInfo.NextExecution = next;

            _logger.LogInformation("Задача '{Name}' завершена успешно, следующий запуск: {Next}",
                taskInfo.Config.Name, next);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Остановка планировщика — не ошибка
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Задача '{Name}' завершена с ошибкой", taskInfo.Config.Name);

            if (taskInfo.Config.RetryIntervalMinutes > 0)
            {
                taskInfo.NextExecution = DateTime.UtcNow.AddMinutes(taskInfo.Config.RetryIntervalMinutes);
                _logger.LogInformation("Повтор '{Name}' запланирован через {Minutes} мин",
                    taskInfo.Config.Name, taskInfo.Config.RetryIntervalMinutes);
            }
            else
            {
                taskInfo.NextExecution = taskInfo.CronExpression.GetNextExecution(DateTime.UtcNow);
            }
        }
        finally
        {
            lock (_lock)
            {
                _runningTasks.Remove(taskInfo.Config.Id);
            }
        }
    }

    private sealed class ScheduledTaskInfo
    {
        public required TaskConfig Config { get; set; }
        public required CronExpression CronExpression { get; set; }
        public DateTime NextExecution { get; set; }
    }
}
