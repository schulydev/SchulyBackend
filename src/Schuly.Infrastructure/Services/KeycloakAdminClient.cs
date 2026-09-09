using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Schuly.Infrastructure.Services
{
    public sealed class KeycloakAdminClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<KeycloakAdminClient> logger) : IIdentityProviderAdmin
    {
        private const string RealmsSegment = "/realms/";

        public async Task<bool> DeleteUserAsync(string externalId, CancellationToken cancellationToken = default)
        {
            var authority = configuration["Oidc:Authority"];
            if (string.IsNullOrEmpty(authority) || !TryParseAuthority(authority, out var baseUrl, out var realm))
            {
                logger.LogWarning("Oidc:Authority ({Authority}) is missing or does not contain a /realms/ segment; the identity-provider user was not deleted. Remove it manually in the admin console.", authority);
                return false;
            }

            var clientId = configuration["Oidc:AdminClientId"];
            var clientSecret = configuration["Oidc:AdminClientSecret"];
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                logger.LogWarning("Oidc:AdminClientId and Oidc:AdminClientSecret are not configured; the identity-provider user was not deleted. Remove it manually in the admin console.");
                return false;
            }

            try
            {
                var client = httpClientFactory.CreateClient();

                var token = await FetchAccessTokenAsync(client, baseUrl, realm, clientId, clientSecret, cancellationToken);
                if (string.IsNullOrEmpty(token))
                {
                    logger.LogWarning("Failed to obtain a Keycloak admin access token; the identity-provider user was not deleted. Remove it manually in the admin console.");
                    return false;
                }

                using var request = new HttpRequestMessage(HttpMethod.Delete, $"{baseUrl}/admin/realms/{realm}/users/{Uri.EscapeDataString(externalId)}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                    return true;

                logger.LogWarning("Failed to delete the identity-provider user; Keycloak responded with {StatusCode}. Remove it manually in the admin console.", response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete the identity-provider user. Remove it manually in the admin console.");
                return false;
            }
        }

        private static async Task<string?> FetchAccessTokenAsync(HttpClient client, string baseUrl, string realm, string clientId, string clientSecret, CancellationToken cancellationToken)
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
            });

            using var response = await client.PostAsync($"{baseUrl}/realms/{realm}/protocol/openid-connect/token", form, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return doc.RootElement.TryGetProperty("access_token", out var value) ? value.GetString() : null;
        }

        private static bool TryParseAuthority(string authority, out string baseUrl, out string realm)
        {
            baseUrl = string.Empty;
            realm = string.Empty;

            var index = authority.IndexOf(RealmsSegment, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return false;

            baseUrl = authority[..index];
            var rest = authority[(index + RealmsSegment.Length)..];
            realm = rest.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

            return !string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(realm);
        }
    }
}
