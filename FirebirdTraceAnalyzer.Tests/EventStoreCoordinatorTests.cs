using FirebirdTraceAnalyzer.Models;
using FirebirdTraceAnalyzer.Services.Persistence;
using FirebirdTraceParser.Models.Events;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// A1: координатор хранилища. Ключевой инвариант режима Off — хранилище не активно и Lazy-диспетчер
/// НЕ форсится (иначе создалась бы events.db). Фабрика Lazy здесь бросает: если к ней обратятся — тест упадёт.
/// </summary>
public sealed class EventStoreCoordinatorTests
{
    private static Lazy<EventStoreDispatcher> ThrowingLazy() =>
        new(() => throw new InvalidOperationException("dispatcher must not be resolved in Off mode"));

    private static TraceFileInfoModel File() =>
        new("f.log", "/tmp/f.log", 1, DateTime.MinValue, DateTime.MinValue, 0, "hash");

    [Fact]
    public void OffMode_IsDisabled_AndNeverResolvesDispatcher()
    {
        var coord = new EventStoreCoordinator(ThrowingLazy(), new OffSettings());

        Assert.False(coord.IsEnabled);
        // Ни одна операция не должна обратиться к Lazy (иначе исключение из фабрики).
        coord.Persist(File(), []);
        coord.RemoveIfSession(["hash"]);
        coord.ClearIfSession();
    }

    [Fact]
    public async Task OffMode_Reads_ReturnEmpty_NoThrow()
    {
        var coord = new EventStoreCoordinator(ThrowingLazy(), new OffSettings());

        Assert.Empty(await coord.ReadFileAsync("hash"));
        Assert.Empty(await coord.ListFilesAsync());
        await coord.RunPendingMaintenanceAsync(); // no-op, без обращения к диспетчеру
    }

    private sealed class OffSettings : Interfaces.ISettingsService
    {
        public AppSettings App { get; } = new() { StorageMode = StorageMode.Off, StorageMaintenancePending = true };
        public UiSectionSettings Ui => throw new NotImplementedException();
        public WindowSettings Window => throw new NotImplementedException();
        public string GetRemoteDownloadDirectory() => throw new NotImplementedException();
        public string GetReportsDirectory() => throw new NotImplementedException();
        public string GetEventStoreDirectory() => throw new NotImplementedException();
        public void Save() { }
        public UserSettings GetDefaults() => throw new NotImplementedException();
        public Task ExportAsync(string path, UserSettings settings) => throw new NotImplementedException();
        public Task<UserSettings> ReadFromFileAsync(string path) => throw new NotImplementedException();
    }
}
