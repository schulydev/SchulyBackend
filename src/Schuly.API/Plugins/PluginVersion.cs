using System.Text.RegularExpressions;

namespace Schuly.API.Plugins
{
    /// <summary>
    /// Guards the admin-supplied plugin version: it becomes part of a download URL, a
    /// file name under the plugins directory and a value in plugins.yml, so only a
    /// strict semver (or the literal "latest") is allowed through.
    /// </summary>
    public static partial class PluginVersion
    {
        public const string Latest = "latest";

        private static bool IsLatest(string? version) => string.IsNullOrWhiteSpace(version) || version.Equals(Latest, StringComparison.OrdinalIgnoreCase);

        private static bool IsValid(string version) => SemVer().IsMatch(version);

        public static string Normalize(string? version)
        {
            if (IsLatest(version))
                return Latest;
            if (!IsValid(version!))
                throw new InvalidOperationException($"Invalid plugin version '{version}'. Expected a semver like 1.2.3 or 'latest'.");
            return version!;
        }

        [GeneratedRegex(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant)]
        private static partial Regex SemVer();
    }
}
