using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Schuly.API.Linking
{
    /// <summary>Strict allowlist sanitizer for plugin-provided provider icons. Returns null when the markup is unusable.</summary>
    public static partial class SvgSanitizer
    {
        public const int MaxBytes = 32 * 1024;
        private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
        private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

        private static readonly HashSet<string> Elements = new(StringComparer.Ordinal)
        {
            "svg", "g", "path", "circle", "ellipse", "rect", "line", "polyline", "polygon", "defs", "linearGradient", "radialGradient", "stop", "clipPath", "mask", "title", "desc", "use", "symbol",
        };

        private static readonly HashSet<string> Attributes = new(StringComparer.Ordinal)
        {
            "viewBox", "width", "height", "xmlns", "id", "class", "transform", "d", "x", "y", "cx", "cy", "r", "rx", "ry", "x1", "y1", "x2", "y2", "fx", "fy", "points", "fill", "stroke", "stroke-width", "stroke-linecap",
            "stroke-linejoin", "stroke-miterlimit", "stroke-dasharray", "stroke-dashoffset", "stroke-opacity", "fill-opacity", "fill-rule", "clip-rule", "clip-path", "mask", "opacity", "offset", "stop-color",
            "stop-opacity", "gradientUnits", "gradientTransform", "spreadMethod", "clipPathUnits", "maskUnits", "preserveAspectRatio", "role", "aria-hidden", "aria-label", "style", "version",
        };

        [GeneratedRegex(@"url\(\s*(?!['""]?#)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 200)]
        private static partial Regex ExternalUrl();

        [GeneratedRegex(@"javascript:|data:|expression\(|@import|<|&#", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 200)]
        private static partial Regex Dangerous();

        public static string? Sanitize(string? markup)
        {
            if (string.IsNullOrWhiteSpace(markup) || Encoding.UTF8.GetByteCount(markup) > MaxBytes)
                return null;

            XDocument document;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxBytes * 2 };
                using var reader = XmlReader.Create(new StringReader(markup), settings);
                document = XDocument.Load(reader);
            }
            catch (Exception ex) when (ex is XmlException or InvalidOperationException)
            {
                return null;
            }

            if (document.Root is not { } root || root.Name != Svg + "svg")
                return null;

            var clean = CleanElement(root);
            if (clean is null)
                return null;

            clean.SetAttributeValue("xmlns", null);
            var output = new XElement(clean);
            return output.ToString(SaveOptions.DisableFormatting);
        }

        public static string Version(string svg) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(svg)))[..16].ToLowerInvariant();

        private static XElement? CleanElement(XElement source)
        {
            if (source.Name.Namespace != Svg || !Elements.Contains(source.Name.LocalName))
                return null;

            var element = new XElement(source.Name);
            foreach (var attribute in source.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                    continue;

                var name = attribute.Name;
                var value = attribute.Value;
                if (Dangerous().IsMatch(value) || ExternalUrl().IsMatch(value))
                    continue;

                if (name.Namespace == XNamespace.None)
                {
                    var local = name.LocalName;
                    if (local.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (local == "href")
                    {
                        if (IsFragment(value))
                            element.SetAttributeValue(name, value);
                        continue;
                    }

                    if (Attributes.Contains(local))
                        element.SetAttributeValue(name, value);
                }
                else if (name == XLink + "href" && IsFragment(value))
                {
                    element.SetAttributeValue(name, value);
                }
            }

            foreach (var node in source.Nodes())
            {
                switch (node)
                {
                    case XElement child:
                        if (CleanElement(child) is { } cleanChild)
                            element.Add(cleanChild);
                        break;
                    case XText text when source.Name.LocalName is "title" or "desc":
                        element.Add(new XText(text.Value));
                        break;
                }
            }

            return element;
        }

        private static bool IsFragment(string value) => value.Length > 1 && value[0] == '#';
    }
}
