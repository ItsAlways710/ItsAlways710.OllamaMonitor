using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ItsAlways710.OllamaMonitor.Ollama;

/// <summary>
/// Resolves what /api/ps cannot say directly: the weight-file (GGUF) digest of a loaded
/// model, read from Ollama's local model store.
/// /api/ps's own `digest` field is the hash of the model's MANIFEST — verified to never
/// equal the weight blob the runner loads (the runner log line names the weight blob,
/// `blobs/sha256-&lt;hex&gt;`). The weight digest is only recoverable from the manifest's
/// "application/vnd.ollama.image.model" layer, so both sides of any runner attribution
/// join must be normalized through <see cref="NormalizeDigest"/>.
/// The model root is resolved like OllamaLogService resolves its log file: an explicit
/// OLLAMA_MODELS directory wins, then the most recently written known root
/// (%USERPROFILE%\.ollama\models for CLI installs, %LOCALAPPDATA%\Ollama\models for
/// the desktop app), and finally the first candidate for fresh installs.
/// </summary>
public sealed class OllamaModelStore
{
    internal const string DefaultRegistry = "registry.ollama.ai";
    internal const string DefaultNamespace = "library";
    internal const string ModelLayerMediaType = "application/vnd.ollama.image.model";

    private static readonly Regex PrefixedDigestRegex = new(@"sha256[-:]([0-9a-fA-F]{64})", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BareDigestRegex = new(@"^[0-9a-fA-F]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _modelRoot;

    public OllamaModelStore()
        : this(ResolveModelRoot(GetDefaultModelDirectories()))
    {
    }

    internal OllamaModelStore(string modelRoot)
    {
        _modelRoot = modelRoot ?? throw new ArgumentNullException(nameof(modelRoot));
    }

    /// <summary>
    /// Resolves the GGUF weight digest of the named model from the local store, or null
    /// when the store, its manifest, or the model layer cannot be found/read.
    /// </summary>
    public string? GetModelDigest(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        foreach (var manifestPath in GetManifestPathCandidates(_modelRoot, modelName))
        {
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = JsonSerializer.Deserialize<ManifestDto>(File.ReadAllText(manifestPath), JsonOptions);
                var layer = manifest?.Layers?.FirstOrDefault(candidate =>
                    string.Equals(candidate.MediaType, ModelLayerMediaType, StringComparison.Ordinal));

                var normalized = NormalizeDigest(layer?.Digest);
                if (normalized is not null)
                {
                    return normalized;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                // Fall through to the next candidate path.
            }
        }

        return null;
    }

    internal static IReadOnlyList<string> GetDefaultModelDirectories() =>
    [
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ollama", "models"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ollama", "models")
    ];

    /// <summary>
    /// Chooses the model root among the candidates: OLLAMA_MODELS (the variable Ollama
    /// itself honors) when it exists, then the most recently written candidate that holds
    /// a manifests/ directory, and finally the first candidate so a store created by a
    /// fresh install resolves as soon as it appears (same preference as OllamaLogService).
    /// </summary>
    internal static string ResolveModelRoot(IReadOnlyList<string> candidates)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("OLLAMA_MODELS");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && Directory.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        string? bestRoot = null;
        var bestWriteTime = DateTimeOffset.MinValue;
        foreach (var candidate in candidates)
        {
            if (!Directory.Exists(Path.Combine(candidate, "manifests")))
            {
                continue;
            }

            try
            {
                var writeTime = new DirectoryInfo(candidate).LastWriteTimeUtc;
                if (bestRoot is null || writeTime > bestWriteTime)
                {
                    bestRoot = candidate;
                    bestWriteTime = writeTime;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return bestRoot ?? candidates.FirstOrDefault()
            ?? throw new InvalidOperationException("No Ollama model store candidates were provided.");
    }

    /// <summary>
    /// Maps a /api/ps model name to the candidate manifest paths under a model root.
    /// Ollama's on-disk layout: one name segment → registry.ollama.ai/library/&lt;name&gt;,
    /// two segments → registry.ollama.ai/&lt;ns&gt;/&lt;name&gt;, three or more → verbatim.
    /// A missing tag defaults to "latest". Both layouts are tried so a name that happens
    /// to follow the other convention still resolves.
    /// </summary>
    internal static IReadOnlyList<string> GetManifestPathCandidates(string modelRoot, string modelName)
    {
        var (baseName, tag) = SplitNameAndTag(modelName.Trim());
        var segments = baseName.Split('/');
        var manifestRoot = Path.Combine(modelRoot, "manifests");

        if (segments.Length == 1)
        {
            return new List<string>
            {
                Path.Combine(manifestRoot, DefaultRegistry, DefaultNamespace, segments[0], tag)
            };
        }

        var literal = Path.Combine(segments);
        return segments.Length == 2
            ? new List<string>
            {
                Path.Combine(manifestRoot, DefaultRegistry, literal, tag),
                Path.Combine(manifestRoot, literal, tag)
            }
            : new List<string>
            {
                Path.Combine(manifestRoot, literal, tag),
                Path.Combine(manifestRoot, DefaultRegistry, DefaultNamespace, literal, tag)
            };
    }

    /// <summary>
    /// Splits a model name into its base name and tag (defaulting to "latest").
    /// A colon inside a path segment (e.g. a "host:port" registry) is not a tag
    /// separator, so only a colon whose remainder contains no "/" counts.
    /// </summary>
    private static (string BaseName, string Tag) SplitNameAndTag(string model)
    {
        for (var i = model.LastIndexOf(':'); i > 0; i = model.LastIndexOf(':', i - 1))
        {
            var remainder = model[(i + 1)..];
            if (!remainder.Contains('/'))
            {
                return (model[..i], remainder);
            }
        }

        return (model, "latest");
    }

    /// <summary>
    /// Canonicalizes every digest convention (sha256:&lt;hex&gt;, sha256-&lt;hex&gt;, or bare
    /// &lt;hex&gt;) to "sha256:&lt;lowercase hex&gt;", so separator spelling never matters
    /// in a comparison. Returns null when the input is not a recognizable digest.
    /// </summary>
    public static string? NormalizeDigest(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var prefixed = PrefixedDigestRegex.Match(raw);
        if (prefixed.Success)
        {
            return $"sha256:{prefixed.Groups[1].Value.ToLowerInvariant()}";
        }

        var trimmed = raw.Trim();
        return BareDigestRegex.IsMatch(trimmed)
            ? $"sha256:{trimmed.ToLowerInvariant()}"
            : null;
    }

    private sealed record ManifestDto(
        [property: JsonPropertyName("layers")] IReadOnlyList<LayerDto>? Layers);

    private sealed record LayerDto(
        [property: JsonPropertyName("mediaType")] string? MediaType,
        [property: JsonPropertyName("digest")] string? Digest);
}
