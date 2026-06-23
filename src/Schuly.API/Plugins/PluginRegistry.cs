using System.Text.Json;
using System.Text.Json.Serialization;

namespace Schuly.API.Plugins
{
    /// <summary>One entry in the registry's <c>index.min.json</c> (Aniyomi-style).</summary>
    public sealed record RegistryPlugin
    {
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("pkg")] public string Pkg { get; init; } = "";
        [JsonPropertyName("dll")] public string Dll { get; init; } = "";
        [JsonPropertyName("deps")] public string? Deps { get; init; }
        [JsonPropertyName("version")] public string Version { get; init; } = "";
        [JsonPropertyName("description")] public string? Description { get; init; }
        [JsonPropertyName("authors")] public string? Authors { get; init; }
    }

    /// <summary>
    /// Reads the plugin registry index and downloads plugin artifacts. The registry
    /// is a static file tree (e.g. the SchulyPlugins <c>repo</c> branch): an
    /// <c>index.min.json</c> at the root and artifacts under <c>dll/</c>.
    /// </summary>
    public sealed class PluginRegistryClient(HttpClient http, string baseUrl)
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        // Normalized to end with a single slash so relative paths append cleanly.
        private readonly string _baseUrl = baseUrl.TrimEnd('/') + "/";

        public async Task<IReadOnlyList<RegistryPlugin>> FetchIndexAsync(CancellationToken ct = default)
        {
            await using var stream = await http.GetStreamAsync($"{_baseUrl}index.min.json", ct);
            var entries = await JsonSerializer.DeserializeAsync<List<RegistryPlugin>>(stream, JsonOptions, ct);
            return entries ?? [];
        }

        /// <summary>Resolves a registry entry by name, optionally pinning a version.</summary>
        public async Task<RegistryPlugin?> ResolveAsync(string name, string? version, CancellationToken ct = default)
        {
            var index = await FetchIndexAsync(ct);
            var entry = index.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
                return null;

            // A pinned version that differs from the index's current one: the registry
            // keeps every build under dll/<name>-v<ver>.dll, so synthesize the filenames.
            if (!string.IsNullOrWhiteSpace(version) &&
                !version.Equals("latest", StringComparison.OrdinalIgnoreCase) &&
                !version.Equals(entry.Version, StringComparison.OrdinalIgnoreCase))
            {
                return entry with
                {
                    Version = version,
                    Dll = $"{entry.Name}-v{version}.dll",
                    Deps = $"{entry.Name}-v{version}-deps.zip",
                };
            }

            return entry;
        }

        public Task<byte[]> DownloadArtifactAsync(string file, CancellationToken ct = default) =>
            http.GetByteArrayAsync($"{_baseUrl}dll/{file}", ct);
    }
}
