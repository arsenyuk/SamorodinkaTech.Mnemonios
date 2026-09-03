using System.Collections;
using Microsoft.Extensions.Logging;

namespace Mnemonios.Infrastructure.Common.Exceptions;

/// <summary>
/// Утилита для рекурсивного обхода цепочки исключений и логирования.
/// </summary>
public static class ExceptionFlattener
{
    public record ExceptionInfo(string Type, string Message, string? StackTrace);

    public static List<ExceptionInfo> Flatten(Exception ex)
    {
        var result = new List<ExceptionInfo>();
        Traverse(ex, result);
        return result;
    }

    private static void Traverse(Exception ex, List<ExceptionInfo> acc)
    {
        if (ex is null) return;
        acc.Add(new ExceptionInfo(ex.GetType().FullName ?? ex.GetType().Name, ex.Message, ex.StackTrace));

        if (ex is AggregateException aex && aex.InnerExceptions is { Count: > 0 })
        {
            foreach (var ie in aex.InnerExceptions)
                Traverse(ie, acc);
            return;
        }

        var innerExceptionsProp = ex.GetType().GetProperty("InnerExceptions");
        if (innerExceptionsProp?.GetValue(ex) is IEnumerable enumerable)
        {
            foreach (var ie in enumerable)
                if (ie is Exception e) Traverse(e, acc);
            return;
        }

        if (ex.InnerException != null)
            Traverse(ex.InnerException, acc);
    }

    public static void LogFlattened(ILogger logger, Exception ex)
    {
        var infos = Flatten(ex);
        logger.LogError("Unhandled exception(s): {Count}", infos.Count);
        foreach (var i in infos)
        {
            logger.LogError("{Type}: {Message}\n{Stack}", i.Type, i.Message, i.StackTrace);
        }
    }

    /// <summary>Извлекает сообщение самого глубокого исключения в цепочке.</summary>
    public static string Unwrap(Exception ex)
    {
        while (ex.InnerException != null)
            ex = ex.InnerException;
        return ex.Message;
    }
}
