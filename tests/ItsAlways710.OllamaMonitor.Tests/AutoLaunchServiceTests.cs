using ItsAlways710.OllamaMonitor.Diagnostics;
using ItsAlways710.OllamaMonitor.Services;

namespace ItsAlways710.OllamaMonitor.Tests;

/// <summary>
/// Tests AutoLaunchService registration logic against a fake Run-key store:
/// enable writes the quoted current exe path, disable removes the entry (and
/// is safe to call twice), and IsEnabled reflects only a registration that
/// still matches this app's executable (a stale path counts as unregistered).
/// </summary>
public sealed class AutoLaunchServiceTests
{
    private static (AutoLaunchService Service, FakeStartupRegistryStore Store) CreateService(string? storedCommand)
    {
        var store = new FakeStartupRegistryStore { StoredCommand = storedCommand };
        var service = new AutoLaunchService(
            new DiagnosticsLogService(Path.Combine(Path.GetTempPath(), "ElBruno.OllamaMonitor.Tests")),
            store);
        return (service, store);
    }

    private static string? CurrentExeCommand()
    {
        var path = Environment.ProcessPath;
        Assert.False(string.IsNullOrWhiteSpace(path), "test host must have an executable path");
        return $"\"{path}\"";
    }

    [Fact]
    public void IsEnabled_TrueWhenStoredCommandMatchesCurrentExe()
    {
        var (service, _) = CreateService(CurrentExeCommand());

        Assert.True(service.IsEnabled());
    }

    [Fact]
    public void IsEnabled_FalseWhenNoRegistration()
    {
        var (service, _) = CreateService(null);

        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void IsEnabled_FalseWhenStoredCommandIsStale()
    {
        var (service, _) = CreateService("\"C:\\old\\location\\ElBruno.OllamaMonitor.exe\"");

        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void SetEnabled_True_WritesQuotedCurrentExePath()
    {
        var (service, store) = CreateService(null);

        service.SetEnabled(true);

        Assert.Equal(CurrentExeCommand(), store.LastWrittenCommand);
        Assert.Equal(1, store.WriteCalls);
        Assert.True(service.IsEnabled());
    }

    [Fact]
    public void SetEnabled_False_RemovesRegistration()
    {
        var (service, store) = CreateService(CurrentExeCommand());

        service.SetEnabled(false);

        Assert.Equal(1, store.DeleteCalls);
        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void SetEnabled_FalseWhenNothingRegistered_DoesNotThrow()
    {
        var (service, store) = CreateService(null);

        service.SetEnabled(false);
        service.SetEnabled(false);

        Assert.Equal(2, store.DeleteCalls);
    }

    private sealed class FakeStartupRegistryStore : IStartupRegistryStore
    {
        public string? StoredCommand { get; set; }
        public int WriteCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public string? LastWrittenCommand { get; private set; }

        public string? ReadCommand() => StoredCommand;

        public void WriteCommand(string command)
        {
            StoredCommand = command;
            LastWrittenCommand = command;
            WriteCalls++;
        }

        public void DeleteCommand()
        {
            StoredCommand = null;
            DeleteCalls++;
        }
    }
}
