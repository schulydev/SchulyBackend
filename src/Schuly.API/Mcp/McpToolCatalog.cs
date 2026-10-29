using System.Reflection;
using ModelContextProtocol.Server;
using Schuly.API.Mcp.Tools;

namespace Schuly.API.Mcp
{
    /// <summary>The permission attributes of every tool by name, read once by reflection, for checks that run before the SDK has matched the tool.</summary>
    public static class McpToolCatalog
    {
        private static readonly Dictionary<string, IReadOnlyList<object>> Tools = new[] { typeof(SchulyTools), typeof(SchulnetzTools), typeof(MicrosoftTools) }
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Select(m => (Tool: m.GetCustomAttribute<McpServerToolAttribute>(), Method: m))
            .Where(x => x.Tool?.Name is not null)
            .ToDictionary(x => x.Tool!.Name!, x => (IReadOnlyList<object>)x.Method.GetCustomAttributes().Where(a => a is McpAccessAttribute or McpDestructiveAttribute).Cast<object>().ToList(), StringComparer.Ordinal);

        public static IReadOnlyCollection<string> Names => Tools.Keys;

        public static IReadOnlyList<object>? Find(string name) => Tools.GetValueOrDefault(name);
    }
}
