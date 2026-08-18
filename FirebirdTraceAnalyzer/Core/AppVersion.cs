using System.Reflection;

namespace FirebirdTraceAnalyzer.Core;


public static class AppVersion
{
    /// <summary>
    /// Gets the application version embedded in the assembly. It is derived from the <see cref="AssemblyInformationalVersionAttribute"/>
    /// (populated by MinVer from the git tag vX.Y.Z), with the build metadata following the '+' character stripped off.
    /// </summary>
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informationalVersion))
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        
        // return string before '+' (build-metadata) if present, otherwise return the whole string
        // "1.5.8+build.324.sha.f122c1d" this example would return "1.5.8"
        var indexOfPlus = informationalVersion.IndexOf('+');
        return indexOfPlus >= 0 ? informationalVersion[..indexOfPlus] : informationalVersion;

    }
}
