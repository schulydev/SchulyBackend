namespace Schuly.Infrastructure.Storage
{
    public record ReencryptedDocument(bool WasAlreadyEncrypted, string Key);

    /// <summary>
    /// Admin-only migration path: re-encrypts a single stored object in place under a
    /// new key, without deleting the original. Callers are responsible for updating
    /// whatever references the old key and deleting it once that update is durable -
    /// this only ever adds an object, it never removes one.
    /// </summary>
    public interface IDocumentEncryptionMaintenance
    {
        Task<ReencryptedDocument> ReencryptAsync(string key, CancellationToken ct);
    }
}
