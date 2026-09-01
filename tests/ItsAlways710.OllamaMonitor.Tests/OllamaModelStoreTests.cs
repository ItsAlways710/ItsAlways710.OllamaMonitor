using ItsAlways710.OllamaMonitor.Ollama;

namespace ItsAlways710.OllamaMonitor.Tests;

/// <summary>
/// Tests for the Ollama local model store bridge: model name → manifest path →
/// GGUF weight digest, plus digest canonicalization. Uses a temporary store layout;
/// no real Ollama installation is required.
/// </summary>
public sealed class OllamaModelStoreTests : IDisposable
{
    private const string ModelHex = "A1B2C3D4E5F60718293A4B5C6D7E8F90A1B2C3D4E5F60718293A4B5C6D7E8F90";
    private const string ProjectorHex = "a3714bfdddeca31351f2752bf1a63f266f4df87c0b68c895e44945ca704448e";
    private const string LicenseHex = "4c6a8e842ef0d8504549facfb03f1273a8d0022991519bc1888522cc1a5517d1";
    private const string ConfigHex = "492b2922d38e553cabc2d319345644ed482874fbf5e5c9e4495cbf8e17b0cf5f";

    private readonly string _root;
    private readonly OllamaModelStore _sut;
    private readonly string? _ollamaModelsEnv;

    public OllamaModelStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"OllamaModelStoreTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_root, "manifests"));
        _sut = new OllamaModelStore(_root);
        _ollamaModelsEnv = Environment.GetEnvironmentVariable("OLLAMA_MODELS");
        Environment.SetEnvironmentVariable("OLLAMA_MODELS", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("OLLAMA_MODELS", _ollamaModelsEnv);
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void WriteManifest(string relativeManifestPath, string modelDigestHex)
    {
        var manifestPath = Path.Combine(_root, relativeManifestPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var json = $$"""
            {"schemaVersion":2,
             "mediaType":"application/vnd.docker.distribution.manifest.v2+json",
             "config":{"mediaType":"application/vnd.docker.container.image.v1+json","digest":"sha256:{{ConfigHex}}","size":215},
             "layers":[
               {"mediaType":"application/vnd.ollama.image.projector","digest":"sha256:{{ProjectorHex}}","size":931},
               {"mediaType":"application/vnd.ollama.image.model","digest":"sha256:{{modelDigestHex}}","size":100},
               {"mediaType":"application/vnd.ollama.image.license","digest":"sha256:{{LicenseHex}}","size":10}]}
            """;
        File.WriteAllText(manifestPath, json);
    }

    [Fact]
    public void GetModelDigest_OneSegmentName_ResolvesLibraryManifest()
    {
        WriteManifest("manifests/registry.ollama.ai/library/alpaca/4k", ModelHex);

        var digest = _sut.GetModelDigest("alpaca:4k");

        Assert.Equal($"sha256:{ModelHex.ToLowerInvariant()}", digest);
    }

    [Fact]
    public void GetModelDigest_TwoSegmentName_ResolvesUserNamespaceManifest()
    {
        WriteManifest("manifests/registry.ollama.ai/beta/mini/2k", ModelHex);

        var digest = _sut.GetModelDigest("beta/mini:2k");

        Assert.Equal($"sha256:{ModelHex.ToLowerInvariant()}", digest);
    }

    [Fact]
    public void GetModelDigest_ThreeSegmentName_UsesVerbatimRegistryPath()
    {
        WriteManifest("manifests/hf.co/org/big/latest", ModelHex);

        var digest = _sut.GetModelDigest("hf.co/org/big:latest");

        Assert.Equal($"sha256:{ModelHex.ToLowerInvariant()}", digest);
    }

    [Fact]
    public void GetModelDigest_NameWithoutTag_FallsBackToLatest()
    {
        WriteManifest("manifests/hf.co/org/big/latest", ModelHex);

        var digest = _sut.GetModelDigest("hf.co/org/big");

        Assert.Equal($"sha256:{ModelHex.ToLowerInvariant()}", digest);
    }

    [Fact]
    public void GetModelDigest_ReturnsModelLayer_NotProjectorConfigOrLicense()
    {
        WriteManifest("manifests/registry.ollama.ai/library/alpaca/4k", ModelHex);

        var digest = _sut.GetModelDigest("alpaca:4k");

        Assert.DoesNotContain(ProjectorHex, digest);
        Assert.DoesNotContain(LicenseHex, digest);
        Assert.DoesNotContain(ConfigHex, digest);
        Assert.Equal(71, digest!.Length); // "sha256:" + 64 hex
    }

    [Fact]
    public void GetModelDigest_UnknownModel_ReturnsNull()
    {
        Assert.Null(_sut.GetModelDigest("ghost:latest"));
    }

    [Fact]
    public void GetModelDigest_MalformedManifest_ReturnsNull()
    {
        var directory = Path.Combine(_root, "manifests", "registry.ollama.ai", "library", "broken");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "latest"), "{not json");

        Assert.Null(_sut.GetModelDigest("broken:latest"));
    }

    [Fact]
    public void GetModelDigest_EmptyName_ReturnsNull()
    {
        Assert.Null(_sut.GetModelDigest(""));
        Assert.Null(_sut.GetModelDigest("  "));
    }

    [Fact]
    public void ResolveModelRoot_MostRecentlyWrittenManifestRoot_Wins()
    {
        var older = Path.Combine(_root, "older");
        var newer = Path.Combine(_root, "newer");
        foreach (var candidate in new[] { older, newer })
        {
            Directory.CreateDirectory(Path.Combine(candidate, "manifests"));
        }
        var stamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        new DirectoryInfo(older).LastWriteTimeUtc = stamp;
        new DirectoryInfo(newer).LastWriteTimeUtc = stamp.AddSeconds(60);

        var resolved = OllamaModelStore.ResolveModelRoot([older, newer]);
        Assert.Equal(newer, resolved);
    }

    [Fact]
    public void ResolveModelRoot_NoneValid_ReturnsFirstCandidate()
    {
        var missingA = Path.Combine(_root, "missingA");
        var missingB = Path.Combine(_root, "missingB");

        var resolved = OllamaModelStore.ResolveModelRoot([missingA, missingB]);

        Assert.Equal(missingA, resolved);
    }

    [Fact]
    public void GetManifestPathCandidates_HostPortName_HasLatestTagCandidate()
    {
        var candidates = OllamaModelStore.GetManifestPathCandidates(_root, "localhost:11434/foo");

        Assert.Contains(candidates, candidate => candidate.EndsWith(Path.Combine("manifests", "localhost:11434", "foo", "latest"), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("not-a-digest", null)]
    [InlineData("sha256:abc", null)]
    [InlineData(ModelHex, "{hex}")]            // bare hex (api/ps form)
    [InlineData($"sha256-{ModelHex}", "{hex}")] // dash prefix (blob filename form)
    [InlineData($"sha256:{ModelHex}", "{hex}")] // colon prefix (manifest form)
    public void NormalizeDigest_CanonicalizesAllConventions(string? raw, string? expectation)
    {
        var normalized = OllamaModelStore.NormalizeDigest(raw);

        if (expectation is null)
        {
            Assert.Null(normalized);
        }
        else
        {
            Assert.Equal($"sha256:{ModelHex.ToLowerInvariant()}", normalized);
        }
    }

    [Fact]
    public void NormalizeDigest_ShortOrLongHex_ReturnsNull()
    {
        Assert.Null(OllamaModelStore.NormalizeDigest("0123456789abcdef"));
        Assert.Null(OllamaModelStore.NormalizeDigest(ModelHex + "00"));
    }
}
