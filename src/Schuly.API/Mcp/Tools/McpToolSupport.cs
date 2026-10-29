using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using Schuly.Application.Models;
using Schuly.Infrastructure.Mcp;

namespace Schuly.API.Mcp.Tools
{
    /// <summary>Shared plumbing for the tools: compact JSON out, HTML to text, length caps with paging, and errors an LLM can act on.</summary>
    public static partial class McpToolSupport
    {
        public const int DefaultMaxChars = 8000;
        public const int MaxStringLength = 2000;

        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
        };

        public static string Serialize(object value) => JsonSerializer.Serialize(value, Json);

        public static McpCaller Caller(IHttpContextAccessor accessor) =>
            accessor.HttpContext?.Items[typeof(McpCaller)] as McpCaller ?? throw new McpException("This MCP request is not authenticated.");

        public static T Unwrap<T>(Result<T> result) => result.IsSuccess ? result.Value! : throw new McpException(result.Error ?? "The request failed.");

        /// <summary>A list capped to <paramref name="limit"/> items after <paramref name="offset"/>, with what the model needs to ask for the next page.</summary>
        public static object Page<T>(IEnumerable<T> items, int offset, int limit, string itemsName = "items")
        {
            var all = items.ToList();
            offset = Math.Max(0, offset);
            limit = Math.Clamp(limit, 1, 200);
            var page = all.Skip(offset).Take(limit).ToList();
            var next = offset + page.Count < all.Count ? offset + page.Count : (int?)null;
            return new Dictionary<string, object?> { ["total"] = all.Count, ["offset"] = offset, [itemsName] = page, ["nextOffset"] = next };
        }

        /// <summary>A long text cut into pages of <paramref name="maxChars"/>, so a model can read a long mail in pieces instead of getting it truncated without warning.</summary>
        public static object TextPage(string text, int offset, int maxChars)
        {
            offset = Math.Clamp(offset, 0, text.Length);
            maxChars = Math.Clamp(maxChars, 200, 50000);
            var slice = text.Substring(offset, Math.Min(maxChars, text.Length - offset));
            var next = offset + slice.Length < text.Length ? offset + slice.Length : (int?)null;
            return new { text = slice, offset, totalLength = text.Length, nextOffset = next };
        }

        public static string HtmlToText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;
            var text = ScriptOrStyle().Replace(html, " ");
            text = BlockBreak().Replace(text, "\n");
            text = Tag().Replace(text, string.Empty);
            text = WebUtility.HtmlDecode(text).Replace(' ', ' ');
            text = SpaceRun().Replace(text, " ");
            text = BlankLines().Replace(text, "\n\n");
            return text.Trim();
        }

        public static string BodyText(JsonElement? body)
        {
            if (body is not { ValueKind: JsonValueKind.Object } b)
                return string.Empty;
            var content = Str(b, "content") ?? string.Empty;
            return string.Equals(Str(b, "contentType"), "html", StringComparison.OrdinalIgnoreCase) || content.Contains('<') ? HtmlToText(content) : content.Trim();
        }

        public static string? Str(JsonElement element, string property) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        public static JsonElement? Prop(JsonElement element, string property) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) ? value : null;

        public static bool? Bool(JsonElement element, string property) => Prop(element, property) is { ValueKind: JsonValueKind.True or JsonValueKind.False } v ? v.GetBoolean() : null;

        public static long? Long(JsonElement element, string property) => Prop(element, property) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt64(out var n) ? n : null;

        /// <summary>The items of a Graph-style answer, whether it is a bare array or an object with <c>value</c>.</summary>
        public static IEnumerable<JsonElement> Items(JsonElement? data)
        {
            if (data is not { } d)
                return [];
            if (d.ValueKind == JsonValueKind.Array)
                return d.EnumerateArray().ToList();
            if (d.ValueKind == JsonValueKind.Object && d.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray().ToList();
            return [];
        }

        public static string? Address(JsonElement? recipient) =>
            recipient is { } r && Prop(r, "emailAddress") is { } e ? (Str(e, "name") is { Length: > 0 } name && Str(e, "address") is { } address ? $"{name} <{address}>" : Str(e, "address") ?? Str(e, "name")) : null;

        public static List<string> Addresses(JsonElement element, string property) =>
            Prop(element, property) is { ValueKind: JsonValueKind.Array } list ? list.EnumerateArray().Select(r => Address(r)).OfType<string>().ToList() : [];

        /// <summary>
        /// Drops what a model has no use for or must not see (OData annotations, pre-authenticated download links), turns HTML bodies into text and caps long strings.
        /// Used where the plugin answer has no fixed projection.
        /// </summary>
        public static JsonNode? Clean(JsonElement element, int depth = 0)
        {
            if (depth > 8)
                return null;
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new JsonObject();
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.Name.StartsWith('@') || property.Name.Contains("@odata", StringComparison.Ordinal))
                            continue;
                        if (property.Name == "body" && property.Value.ValueKind == JsonValueKind.Object)
                        {
                            obj["body"] = Cap(BodyText(property.Value));
                            continue;
                        }
                        var value = Clean(property.Value, depth + 1);
                        if (value is not null)
                            obj[property.Name] = value;
                    }
                    return obj;
                case JsonValueKind.Array:
                    var array = new JsonArray();
                    foreach (var item in element.EnumerateArray().Take(200))
                        array.Add(Clean(item, depth + 1));
                    return array;
                case JsonValueKind.String:
                    return JsonValue.Create(Cap(element.GetString() ?? string.Empty));
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                default:
                    return JsonNode.Parse(element.GetRawText());
            }
        }

        public static string Cap(string text, int max = MaxStringLength) => text.Length <= max ? text : text[..max] + "...";

        public static bool LooksLikeText(string? contentType, string? fileName)
        {
            if (contentType is { } type && (type.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || type.Contains("json", StringComparison.OrdinalIgnoreCase) || type.Contains("xml", StringComparison.OrdinalIgnoreCase) || type.Contains("csv", StringComparison.OrdinalIgnoreCase)))
                return true;
            var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            return extension is ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".html" or ".htm" or ".ics" or ".log" or ".yml" or ".yaml";
        }

        /// <summary>File content for a model: text when it is text, base64 when it is small enough, otherwise only the facts about it.</summary>
        public static object FileContent(byte[] bytes, string? contentType, string? fileName, int maxBase64Bytes, int offset, int maxChars)
        {
            if (LooksLikeText(contentType, fileName))
            {
                var text = Encoding.UTF8.GetString(bytes);
                if (contentType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true || fileName?.EndsWith(".html", StringComparison.OrdinalIgnoreCase) == true)
                    text = HtmlToText(text);
                return new { fileName, contentType, encoding = "text", content = TextPage(text, offset, maxChars) };
            }
            if (bytes.Length <= maxBase64Bytes)
                return new { fileName, contentType, encoding = "base64", sizeBytes = bytes.Length, contentBase64 = Convert.ToBase64String(bytes) };
            return new { fileName, contentType, encoding = "none", sizeBytes = bytes.Length, note = $"The file is larger than {maxBase64Bytes / 1024} KB and is not text, so its content is not returned. Open it in Schuly instead." };
        }

        [GeneratedRegex(@"<(script|style|head)[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex ScriptOrStyle();

        [GeneratedRegex(@"<\s*(br|/p|/div|/li|/tr|/h[1-6]|/table|hr)[^>]*>", RegexOptions.IgnoreCase)]
        private static partial Regex BlockBreak();

        [GeneratedRegex(@"<[^>]+>")]
        private static partial Regex Tag();

        [GeneratedRegex(@"[ \t\r\f\v]+")]
        private static partial Regex SpaceRun();

        [GeneratedRegex(@"\n\s*\n\s*(\n\s*)+")]
        private static partial Regex BlankLines();
    }
}
