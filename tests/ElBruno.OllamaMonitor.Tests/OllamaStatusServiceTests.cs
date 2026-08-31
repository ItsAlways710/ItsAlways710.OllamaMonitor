using System.Net;
using System.Net.Http.Json;
using ElBruno.OllamaMonitor.Configuration;
using ElBruno.OllamaMonitor.Diagnostics;
using ElBruno.OllamaMonitor.Ollama;
using ElBruno.OllamaMonitor.Services;

namespace ElBruno.OllamaMonitor.Tests;

public sealed class OllamaStatusServiceTests
{
    [Fact]
    public async Task UnloadModel_AutoLocal_UsesCliStop()
    {
        var fixture = CreateFixture(cliStopResult: Success());
        var settings = new AppSettings { Endpoint = "http://localhost:11434", UnloadStrategy = ModelUnloadStrategy.Auto };

        var result = await fixture.Service.UnloadModelAsync(settings, "phi4", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, fixture.Cli.StopCalls);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task UnloadModel_AutoLocal_CliFailure_FallsBackToApi()
    {
        var fixture = CreateFixture(cliStopResult: Fail("stop failed"));
        var settings = new AppSettings { Endpoint = "http://localhost:11434", UnloadStrategy = ModelUnloadStrategy.Auto };

        var result = await fixture.Service.UnloadModelAsync(settings, "phi4", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, fixture.Cli.StopCalls);
        Assert.Single(fixture.Handler.Requests);
        Assert.Equal("/api/generate", fixture.Handler.Requests[0].RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task UnloadModel_AutoRemote_UsesApiOnly()
    {
        var fixture = CreateFixture(cliStopResult: Success());
        var settings = new AppSettings { Endpoint = "http://192.168.1.20:11434", UnloadStrategy = ModelUnloadStrategy.Auto };

        var result = await fixture.Service.UnloadModelAsync(settings, "phi4", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, fixture.Cli.StopCalls);
        Assert.Single(fixture.Handler.Requests);
        Assert.Equal("/api/generate", fixture.Handler.Requests[0].RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task UnloadModel_CliRemote_ReturnsValidationError()
    {
        var fixture = CreateFixture(cliStopResult: Success());
        var settings = new AppSettings { Endpoint = "http://192.168.1.20:11434", UnloadStrategy = ModelUnloadStrategy.Cli };

        var result = await fixture.Service.UnloadModelAsync(settings, "phi4", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("requires a local Ollama endpoint", result.ErrorMessage);
        Assert.Equal(0, fixture.Cli.StopCalls);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task PullModel_RemoteEndpoint_ReturnsLocalOnlyError()
    {
        var fixture = CreateFixture(cliStopResult: Success());
        var settings = new AppSettings { Endpoint = "http://192.168.1.20:11434" };

        var result = await fixture.Service.PullModelAsync(settings, "phi4", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("local Ollama endpoints", result.ErrorMessage);
    }

    [Fact]
    public async Task GetRunningModelNames_AutoLocal_UsesCliResult()
    {
        var fixture = CreateFixture(
            cliStopResult: Success(),
            cliPsResult: new OllamaApiCallResult<IReadOnlyList<string>>(true, ["phi4", "llama3"]));
        var settings = new AppSettings { Endpoint = "http://localhost:11434", UnloadStrategy = ModelUnloadStrategy.Auto };

        var result = await fixture.Service.GetRunningModelNamesAsync(settings, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["phi4", "llama3"], result.Value);
        Assert.Equal(1, fixture.Cli.PsCalls);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GetSnapshot_WithModels_SetsProcessorUsageFromApiData()
    {
        // size=4GB, size_vram=3GB → 75% GPU, 25% CPU
        var size = 4_000_000_000L;
        var sizeVram = 3_000_000_000L;
        var fixture = CreateFixtureWithModels(size, sizeVram);
        var settings = new AppSettings { Endpoint = "http://localhost:11434" };

        var snapshot = await fixture.Service.GetSnapshotAsync(settings, CancellationToken.None);

        var model = Assert.Single(snapshot.Models);
        Assert.Equal("25% CPU · 75% GPU", model.ProcessorUsage);
        Assert.Equal(sizeVram, model.SizeVram);
    }

    [Fact]
    public async Task GetSnapshot_WithCpuOnlyModel_SetsProcessorUsageTo100PercentCpu()
    {
        var fixture = CreateFixtureWithModels(size: 2_000_000_000L, sizeVram: 0L);
        var settings = new AppSettings { Endpoint = "http://localhost:11434" };

        var snapshot = await fixture.Service.GetSnapshotAsync(settings, CancellationToken.None);

        var model = Assert.Single(snapshot.Models);
        Assert.Equal("100% CPU", model.ProcessorUsage);
    }

    [Fact]
    public async Task GetSnapshot_WithGpuOnlyModel_SetsProcessorUsageTo100PercentGpu()
    {
        var size = 2_000_000_000L;
        var fixture = CreateFixtureWithModels(size: size, sizeVram: size);
        var settings = new AppSettings { Endpoint = "http://localhost:11434" };

        var snapshot = await fixture.Service.GetSnapshotAsync(settings, CancellationToken.None);

        var model = Assert.Single(snapshot.Models);
        Assert.Equal("100% GPU", model.ProcessorUsage);
    }

    private static SnapshotFixture CreateFixtureWithModels(long size, long sizeVram)
    {
        var diagnostics = new DiagnosticsLogService(Path.Combine(Path.GetTempPath(), "ElBruno.OllamaMonitor.Tests"));
        var handler = new ModelResponseHttpMessageHandler(size, sizeVram);
        var client = new OllamaClient(new HttpClient(handler), diagnostics);
        var cli = new FakeOllamaCliService(new OllamaApiCallResult<bool>(true, true));
        var service = new OllamaStatusService(
            client,
            cli,
            new ProcessMetricsService(diagnostics),
            new NvidiaSmiMetricsService(diagnostics),
            new OsMetricsService(diagnostics),
            new ContextTrackingService(new OllamaLogService(diagnostics)),
            diagnostics);
        return new SnapshotFixture(service);
    }

    private static TestFixture CreateFixture(
        OllamaApiCallResult<bool> cliStopResult,
        OllamaApiCallResult<IReadOnlyList<string>>? cliPsResult = null)
    {
        var diagnostics = new DiagnosticsLogService(Path.Combine(Path.GetTempPath(), "ElBruno.OllamaMonitor.Tests"));
        var handler = new RecordingHttpMessageHandler();
        var client = new OllamaClient(new HttpClient(handler), diagnostics);
        var cli = new FakeOllamaCliService(cliStopResult, cliPsResult);
        var service = new OllamaStatusService(
            client,
            cli,
            new ProcessMetricsService(diagnostics),
            new NvidiaSmiMetricsService(diagnostics),
            new OsMetricsService(diagnostics),
            new ContextTrackingService(new OllamaLogService(diagnostics)),
            diagnostics);

        return new TestFixture(service, cli, handler);
    }

    private static OllamaApiCallResult<bool> Success() => new(true, true);
    private static OllamaApiCallResult<bool> Fail(string message) => new(false, ErrorMessage: message);

    private sealed record TestFixture(OllamaStatusService Service, FakeOllamaCliService Cli, RecordingHttpMessageHandler Handler);

    private sealed record SnapshotFixture(OllamaStatusService Service);

    private sealed class FakeOllamaCliService : IOllamaCliService
    {
        private readonly OllamaApiCallResult<bool> _stopResult;
        private readonly OllamaApiCallResult<IReadOnlyList<string>> _psResult;

        public FakeOllamaCliService(OllamaApiCallResult<bool> stopResult, OllamaApiCallResult<IReadOnlyList<string>>? psResult = null)
        {
            _stopResult = stopResult;
            _psResult = psResult ?? new OllamaApiCallResult<IReadOnlyList<string>>(true, []);
        }

        public int StopCalls { get; private set; }
        public int PsCalls { get; private set; }

        public Task<OllamaApiCallResult<IReadOnlyList<string>>> GetRunningModelsAsync(CancellationToken cancellationToken)
        {
            PsCalls++;
            return Task.FromResult(_psResult);
        }

        public Task<OllamaApiCallResult<bool>> StopModelAsync(string modelName, CancellationToken cancellationToken)
        {
            StopCalls++;
            return Task.FromResult(_stopResult);
        }

        public Task<OllamaApiCallResult<bool>> PullModelAsync(string modelName, CancellationToken cancellationToken) =>
            Task.FromResult(new OllamaApiCallResult<bool>(true, true));

        public Task<OllamaApiCallResult<bool>> RemoveModelAsync(string modelName, CancellationToken cancellationToken) =>
            Task.FromResult(new OllamaApiCallResult<bool>(true, true));

        public Task<OllamaApiCallResult<bool>> CopyModelAsync(string sourceModelName, string targetModelName, CancellationToken cancellationToken) =>
            Task.FromResult(new OllamaApiCallResult<bool>(true, true));

        public OllamaApiCallResult<bool> StartOllama() => new(true, true);
    }

    private sealed class ModelResponseHttpMessageHandler : HttpMessageHandler
    {
        private readonly long _size;
        private readonly long _sizeVram;

        public ModelResponseHttpMessageHandler(long size, long sizeVram)
        {
            _size = size;
            _sizeVram = sizeVram;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = request.RequestUri?.AbsolutePath switch
            {
                "/api/ps" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        models = new[]
                        {
                            new
                            {
                                name = "test-model:latest",
                                size = _size,
                                size_vram = _sizeVram,
                                expires_at = DateTimeOffset.UtcNow.AddMinutes(5),
                                details = new { format = "gguf", family = "llama", parameter_size = "7B", quantization_level = "Q4_0" }
                            }
                        }
                    })
                },
                "/api/version" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { version = "0.0.0-test" })
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { ok = true })
                }
            };

            return Task.FromResult(response);
        }
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            var response = request.RequestUri?.AbsolutePath switch
            {
                "/api/ps" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        models = Array.Empty<object>()
                    })
                },
                "/api/version" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        version = "0.0.0-test"
                    })
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { ok = true })
                }
            };

            return Task.FromResult(response);
        }
    }
}
