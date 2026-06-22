namespace Schuly.Domain
{
    /// <summary>
    /// A login provider the app can offer. The backend owns this catalog so the app
    /// can render the system picker and each system's login form dynamically, instead
    /// of hardcoding providers. The CRM stays provider-agnostic: every concrete system
    /// is operator-supplied catalog data, not baked into the code.
    /// </summary>
    public class SchoolSystem : Base
    {
        /// <summary>Stable identifier the app branches its login flow on.</summary>
        public required string Key { get; set; }

        public required string DisplayName { get; set; }
        public string? LogoUrl { get; set; }

        /// <summary>
        /// How the app's private mode authenticates and fetches data for this system:
        /// <c>"token"</c> (a headless login mints a bearer token + refreshable session,
        /// then batched data endpoints) or <c>"scrape"</c> (credentials are replayed on
        /// each fetch). Lets the client pick a fetch strategy without knowing the provider.
        /// </summary>
        public string? PrivateAuthStrategy { get; set; }

        /// <summary>
        /// Base path of this system's stateless plugin endpoints used by the app's
        /// private mode. Lets the client reach the right proxy without hardcoding
        /// plugin routes.
        /// </summary>
        public string? StatelessBasePath { get; set; }

        /// <summary>
        /// Base path of this system's plugin endpoints used by account mode for
        /// accounts/sync/status. The system key differs from the plugin name, so the
        /// app can't derive this — it must be advertised.
        /// </summary>
        public string? PluginBasePath { get; set; }

        /// <summary>How the app should drive the login: "oauth-webview" or "credentials".</summary>
        public required string LoginMethod { get; set; }

        public bool Enabled { get; set; } = true;

        /// <summary>Display order in the picker (ascending).</summary>
        public int SortOrder { get; set; }

        /// <summary>Inputs the app must render to collect what the login needs.</summary>
        public List<SchoolSystemLoginField> LoginFields { get; set; } = [];
    }

    /// <summary>One input the app renders on a system's login form. Persisted as JSON on the owning system.</summary>
    public class SchoolSystemLoginField
    {
        /// <summary>Field identifier sent back with the collected value, e.g. "baseUrl".</summary>
        public required string Key { get; set; }

        public required string Label { get; set; }

        /// <summary>Input type hint: "url", "text" or "password".</summary>
        public required string Type { get; set; }

        public string? Placeholder { get; set; }
        public string? DefaultValue { get; set; }
        public bool Required { get; set; } = true;
    }
}
