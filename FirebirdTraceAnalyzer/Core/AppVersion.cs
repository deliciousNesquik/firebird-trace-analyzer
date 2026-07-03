using System.Reflection;

namespace FirebirdTraceAnalyzer.Core;

/// <summary>
/// Версия приложения, вшитая в сборку. Берётся из <see cref="AssemblyInformationalVersionAttribute"/>
/// (его заполняет MinVer из git-тега vX.Y.Z), с отсечением build-metadata после '+'. Фолбэк —
/// числовая версия сборки.
/// </summary>
public static class AppVersion
{
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    }
}
