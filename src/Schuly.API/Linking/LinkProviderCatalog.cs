using Schuly.Plugin.Abstractions.Linking;

namespace Schuly.API.Linking
{
    /// <summary>A validated provider descriptor plus its sanitized icon (null when there is none or it was rejected).</summary>
    public sealed record LinkProviderEntry(LinkProviderDescriptor Descriptor, string? Svg, string? SvgVersion);

    /// <summary>A provider resolved from a fresh plugin scope; dispose it when the call is done.</summary>
    public interface ILinkProviderLease : IDisposable
    {
        ILinkProvider Provider { get; }
    }

    /// <summary>Where the linking service finds the link providers of the loaded plugins.</summary>
    public interface ILinkProviderCatalog
    {
        IReadOnlyList<LinkProviderEntry> Providers { get; }
        LinkProviderEntry? Find(string providerId);
        ILinkProviderLease? Open(string providerId);
    }
}
