namespace Mnemonios.Worker.Scheduling;

/// <summary>
/// Парсер cron-вычислений. Поддерживает 5 полей: minute hour dayOfMonth month dayOfWeek.
/// </summary>
public class CronExpression
{
    private readonly int[] _minutes;
    private readonly int[] _hours;
    private readonly int[] _daysOfMonth;
    private readonly int[] _months;
    private readonly int[] _daysOfWeek;

    /// <summary>
    /// Создаёт экземпляр парсера cron-выражения.
    /// </summary>
    /// <param name="expression">Cron-выражение (5 полей).</param>
    public CronExpression(string expression)
    {
        var parts = expression.Split(' ');
        if (parts.Length != 5)
        {
            throw new ArgumentException("Cron-выражение должно содержать 5 полей: minute hour dayOfMonth month dayOfWeek");
        }

        try
        {
            _minutes = ParseField(parts[0], 0, 59);
            _hours = ParseField(parts[1], 0, 23);
            _daysOfMonth = ParseField(parts[2], 1, 31);
            _months = ParseField(parts[3], 1, 12);
            _daysOfWeek = ParseField(parts[4], 0, 6);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Некорректное cron-выражение: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Вычисляет следующее время выполнения после указанного момента.
    /// </summary>
    public DateTime GetNextExecution(DateTime after)
    {
        var current = new DateTime(after.Year, after.Month, after.Day, after.Hour, after.Minute, 0, DateTimeKind.Utc);
        current = current.AddMinutes(1);

        // Ограничение поиска — 4 года вперёд
        var maxDate = current.AddYears(4);

        while (current < maxDate)
        {
            if (Matches(current))
            {
                return current;
            }

            current = current.AddMinutes(1);
        }

        throw new InvalidOperationException("Не удалось найти следующее время выполнения в пределах 4 лет");
    }

    private bool Matches(DateTime dt)
    {
        return _minutes.Contains(dt.Minute)
            && _hours.Contains(dt.Hour)
            && _daysOfMonth.Contains(dt.Day)
            && _months.Contains(dt.Month)
            && _daysOfWeek.Contains((int)dt.DayOfWeek);
    }

    private static int[] ParseField(string field, int min, int max)
    {
        if (field == "*")
        {
            return Enumerable.Range(min, max - min + 1).ToArray();
        }

        var values = new List<int>();

        foreach (var part in field.Split(','))
        {
            if (part.Contains('-'))
            {
                var range = part.Split('-');
                int start = int.Parse(range[0]);
                int end = int.Parse(range[1]);

                for (int i = start; i <= end; i++)
                {
                    values.Add(i);
                }
            }
            else if (part.Contains('/'))
            {
                var step = part.Split('/');
                int stepValue = int.Parse(step[1]);

                for (int i = min; i <= max; i += stepValue)
                {
                    values.Add(i);
                }
            }
            else
            {
                values.Add(int.Parse(part));
            }
        }

        return values.Distinct().ToArray();
    }
}
