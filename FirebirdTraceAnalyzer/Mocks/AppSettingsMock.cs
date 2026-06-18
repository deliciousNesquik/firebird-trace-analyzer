using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Mocks;

public class AppSettingsMock: AppSettings
{
    public AppSettingsMock()
    {
        IsClassicSearch = true;
        Theme = AppTheme.Auto;
        RemoteDownloadPath = string.Empty;
    }
}

public class UiSectionSettingsMock: UiSectionSettings
{
    public UiSectionSettingsMock()
    {
        Files = true;
        Search = true;
        Events = true;
        Statistics = true;
        Logs = true;
    }
}