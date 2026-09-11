namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Kind of a toast notification. Drives its color, icon and auto-dismiss behavior:
/// <see cref="Success"/> auto-hides after a few seconds; <see cref="Warning"/> and
/// <see cref="Error"/> stay until the user closes them.
/// </summary>
public enum ToastType
{
    /// <summary>Операция завершилась успешно (зелёный, авто-скрытие).</summary>
    Success,

    /// <summary>Предупреждение — операция не выполнена, но это не сбой (янтарный, до закрытия).</summary>
    Warning,

    /// <summary>Ошибка/сбой (красный, до закрытия).</summary>
    Error,
}
