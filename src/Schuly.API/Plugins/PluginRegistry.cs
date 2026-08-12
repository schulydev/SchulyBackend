using System.Text.Json;
using System.Text.Json.Serialization;

namespace Schuly.API.Plugins
{
    public sealed record RegistryPlugin
    {
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("pkg")] public string Pkg { get; init; } = "";
        [JsonPropertyName("dll")] public string Dll { get; init; } = "";
        [JsonPropertyName("deps")] public string? Deps { get; init; }
        [JsonPropertyName("version")] public string Version { get; init; } = "";
        [JsonPropertyName("description")] public string? Description { get; init; }
        [JsonPropertyName("authors")] public string? Authors { get; init; }

        public RegistryPlugin WithPinnedVersion(string? version)
        {
            if (string.IsNullOrWhiteSpace(version) ||
                version.Equals("latest", StringComparison.OrdinalIgnoreCase) ||
                version.Equals(Version, StringComparison.OrdinalIgnoreCase))
                return this;

            return this with
            {
                Version = version,
                Dll = $"{Name}-v{version}.dll",
                Deps = $"{Name}-v{version}-deps.zip",
            };
        }
    }

    public sealed class PluginRegistryClient(HttpClient http, string baseUrl)
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly string _baseUrl = baseUrl.TrimEnd('/') + "/";

        public async Task<IReadOnlyList<RegistryPlugin>> FetchIndexAsync(CancellationToken ct = default)
        {
            await using var stream = await http.GetStreamAsync($"{_baseUrl}index.min.json", ct);
            var entries = await JsonSerializer.DeserializeAsync<List<RegistryPlugin>>(stream, JsonOptions, ct);
            return entries ?? [];
        }

        public async Task<RegistryPlugin?> ResolveAsync(string name, string? version, CancellationToken ct = default)
        {
            var index = await FetchIndexAsync(ct);
            var entry = index.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return entry?.WithPinnedVersion(version);
        }

        public Task<byte[]> DownloadArtifactAsync(string file, CancellationToken ct = default) =>
            http.GetByteArrayAsync($"{_baseUrl}dll/{file}", ct);
    }
}
