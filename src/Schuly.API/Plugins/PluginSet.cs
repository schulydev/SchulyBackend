using System.Text;
using Microsoft.Extensions.Configuration;

namespace Schuly.API.Plugins
{
    public sealed record DesiredPlugin
    {
        public string Name { get; init; } = "";
        public string Version { get; init; } = "latest";
    }

    public sealed class PluginSet(string filePath)
    {
        public string FilePath { get; } = filePath;

        public IReadOnlyList<DesiredPlugin> Read()
        {
            if (!File.Exists(FilePath))
                return [];

            var config = new ConfigurationBuilder()
                .AddYamlFile(FilePath, optional: true)
                .Build();

            return config.GetSection("plugins").Get<List<DesiredPlugin>>() ?? [];
        }

        public void Write(IEnumerable<DesiredPlugin> plugins)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Desired Schuly plugin set. Reconciled on startup and by the");
            sb.AppendLine("# /api/plugins admin endpoints. version: a pinned semver or 'latest'.");
            sb.AppendLine("plugins:");
            foreach (var p in plugins.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"  - name: {p.Name}");
                sb.AppendLine($"    version: {(string.IsNullOrWhiteSpace(p.Version) ? "latest" : p.Version)}");
            }

            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, sb.ToString());
        }

        public void Upsert(string name, string version)
        {
            var set = Read().Where(p => !p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
            set.Add(new DesiredPlugin { Name = name, Version = string.IsNullOrWhiteSpace(version) ? "latest" : version });
            Write(set);
        }

        public void RemoveEntry(string name)
        {
            var set = Read().Where(p => !p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
            Write(set);
        }
    }
}
