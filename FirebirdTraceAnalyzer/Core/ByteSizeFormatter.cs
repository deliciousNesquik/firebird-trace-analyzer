namespace FirebirdTraceAnalyzer.Core;

/// <summary>
/// Единое форматирование размеров и скоростей в человекочитаемый вид.
/// Заменяет разбросанные по проекту копии FormatFileSize/FormatBytes/FormatSize и FormatSpeed.
/// </summary>
public static class ByteSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>
    /// Форматирует размер в байтах: сырые байты — как целое (N0), крупнее — с двумя знаками (0.##).
    /// Например: 512 → "512 B", 1536 → "1,5 KB", 1073741824 → "1 GB".
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes:N0} {Units[unitIndex]}"
            : $"{size:0.##} {Units[unitIndex]}";
    }

    /// <summary>
    /// Форматирует скорость в байтах/сек: B/s, KB/s или MB/s.
    /// </summary>
    public static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
            return $"{bytesPerSecond:F0} B/s";

        if (bytesPerSecond < 1024 * 1024)
            return $"{bytesPerSecond / 1024:F2} KB/s";

        return $"{bytesPerSecond / (1024 * 1024):F2} MB/s";
    }
}
