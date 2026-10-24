using Schuly.Infrastructure.Storage;
using Schuly.Plugin.Abstractions;

namespace Schuly.Infrastructure.Plugins
{
    public sealed class PluginFileStore(IDocumentStorage storage) : IPluginFileStore
    {
        public async Task<StoredFile> SaveAsync(Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default)
        {
            var blob = await storage.UploadAsync(content, fileName, contentType, cancellationToken);
            return new StoredFile(blob.Key, blob.SizeBytes);
        }
    }
}
