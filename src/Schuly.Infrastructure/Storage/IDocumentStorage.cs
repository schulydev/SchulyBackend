namespace Schuly.Infrastructure.Storage
{
    public record UploadedBlob(string Key, long SizeBytes);

    public record DocumentStream(Stream Content, string? ContentType, long? ContentLength) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await Content.DisposeAsync();
    }

    public interface IDocumentStorage
    {
        Task<UploadedBlob> UploadAsync(Stream content, string fileName, string? contentType, CancellationToken ct);

        Task<DocumentStream> OpenReadAsync(string key, CancellationToken ct);

        Task DeleteAsync(string key, CancellationToken ct);
    }
}
