namespace Schuly.Infrastructure.Storage
{
    public record UploadedBlob(string Key, long SizeBytes);

    public record DocumentStream(Stream Content, string? ContentType, long? ContentLength) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await Content.DisposeAsync();
    }

    /// <summary>
    /// Document blob storage. Implementations should be S3-compatible (SeaweedFS,
    /// AWS S3, Cloudflare R2) so the backend can swap providers without code
    /// change. All access goes through this interface — clients never talk to
    /// the storage directly; the backend proxies bytes.
    /// </summary>
    public interface IDocumentStorage
    {
        Task<UploadedBlob> UploadAsync(Stream content, string fileName, string? contentType, CancellationToken ct);

        Task<DocumentStream> OpenReadAsync(string key, CancellationToken ct);

        Task DeleteAsync(string key, CancellationToken ct);
    }
}
