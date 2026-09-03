using Xunit;

// Интеграционные тесты используют общую БД — отключаем параллельный запуск
[assembly: CollectionBehavior(DisableTestParallelization = true)]
