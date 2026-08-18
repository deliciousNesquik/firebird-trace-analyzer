namespace FirebirdTraceAnalyzer.Core;

/// <summary>
/// Provides methods to format byte sizes and speeds into human-readable strings.
/// </summary>
public static class ByteSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];
    
    /// <summary>
    /// Formats a size in bytes: raw bytes as an integer (N0), larger sizes with two decimal places (0.##).
    /// </summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The formatted size string.</returns>
    /// <example>
    ///     <code>
    ///         var size = ByteSizeFormatter.FormatBytes(512);
    ///     </code>
    /// results in <c>size</c>'s having the value "512 B".     
    ///     <code>
    ///         size = ByteSizeFormatter.FormatBytes(1536);
    ///     </code>
    /// results in <c>size</c>'s having the value "1,5 KB".     
    ///     <code>
    ///         size = ByteSizeFormatter.FormatBytes(1073741824); 
    ///     </code>
    /// results in <c>size</c>'s having the value "1 GB".     
    /// </example>
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
    /// Formats the speed in bytes/s: B/s, KB/s, or MB/s.
    /// </summary>
    /// <param name="bytesPerSecond">The speed in bytes per second.</param>
    /// <returns>The formatted speed string.</returns>
    /// <example>
    ///     <code>
    ///         var speed = ByteSizeFormatter.FormatSpeed(1024);
    ///     </code>
    /// results in <c>speed</c>'s having the value "1,02 KB/s".
    /// </example>
    public static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
            return $"{bytesPerSecond:F0} B/s";

        if (bytesPerSecond < 1024 * 1024)
            return $"{bytesPerSecond / 1024:F2} KB/s";

        return $"{bytesPerSecond / (1024 * 1024):F2} MB/s";
    }
}
