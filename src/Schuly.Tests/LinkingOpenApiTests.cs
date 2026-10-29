using System.Text.Json;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class LinkingOpenApiTests
    {
        private static async Task<JsonElement> LoadSchemas()
        {
            await using var host = await LinkingTestHost.StartAsync(new ScriptedLinkProvider());
            var text = await host.Client.GetStringAsync("/openapi/v1.json");
            if (Environment.GetEnvironmentVariable("DUMP_OPENAPI") is { Length: > 0 } path)
                { try { await File.WriteAllTextAsync(path, text); } catch (IOException) { } }
            return JsonDocument.Parse(text).RootElement.Clone();
        }

        private static string[] EnumValues(JsonElement doc, string schema, string? property = null)
        {
            var node = doc.GetProperty("components").GetProperty("schemas").GetProperty(schema);
            if (property is not null)
                node = node.GetProperty("properties").GetProperty(property);
            if (node.TryGetProperty("allOf", out var allOf))
                node = doc.GetProperty("components").GetProperty("schemas").GetProperty(allOf[0].GetProperty("$ref").GetString()!.Split('/').Last());
            else if (node.TryGetProperty("$ref", out var r))
                node = doc.GetProperty("components").GetProperty("schemas").GetProperty(r.GetString()!.Split('/').Last());
            return node.GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).ToArray();
        }

        [Test]
        public async Task Linking_routes_and_dtos_are_in_the_openapi_document()
        {
            var doc = await LoadSchemas();
            var paths = doc.GetProperty("paths");
            foreach (var p in new[] { "/api/linking/providers", "/api/linking/providers/{providerId}/icon.svg", "/api/linking/providers/{providerId}/start", "/api/linking/sessions/{sessionId}/submit", "/api/linking/sessions/{sessionId}", "/api/linking/callback", "/api/linking/accounts", "/api/linking/accounts/{providerId}/{accountId}" })
                await Assert.That(paths.TryGetProperty(p, out _)).IsTrue();

            await Assert.That(doc.GetProperty("components").GetProperty("schemas").TryGetProperty("LinkSessionDto", out _)).IsTrue();
            await Assert.That(doc.GetProperty("components").GetProperty("schemas").TryGetProperty("LinkProviderAccountDto", out _)).IsTrue();
            await Assert.That(doc.GetProperty("components").GetProperty("schemas").TryGetProperty("LinkedAccountDto", out _)).IsTrue();
        }

        [Test]
        public async Task Linking_enums_are_camel_case_strings()
        {
            var doc = await LoadSchemas();
            await Assert.That(EnumValues(doc, "LinkStepDto", "kind")).IsEquivalentTo(new[] { "form", "otp", "pushCode", "deviceCode", "redirect", "select", "done", "error" });
            await Assert.That(EnumValues(doc, "LinkFieldDto", "type")).IsEquivalentTo(new[] { "text", "email", "password", "select", "url", "totpSecret" });
            await Assert.That(EnumValues(doc, "LinkSessionDto", "status")).IsEquivalentTo(new[] { "active", "done", "failed", "expired" });
            await Assert.That(EnumValues(doc, "LinkProviderAccountDto", "status")).IsEquivalentTo(new[] { "ok", "syncing", "expired", "error" });
        }

        [Test]
        public async Task Optional_linking_fields_are_nullable()
        {
            var doc = await LoadSchemas();
            var step = doc.GetProperty("components").GetProperty("schemas").GetProperty("LinkStepDto").GetProperty("properties");
            await Assert.That(step.GetProperty("title").GetProperty("nullable").GetBoolean()).IsTrue();
            await Assert.That(step.GetProperty("id").TryGetProperty("nullable", out _)).IsFalse();
        }
    }
}
