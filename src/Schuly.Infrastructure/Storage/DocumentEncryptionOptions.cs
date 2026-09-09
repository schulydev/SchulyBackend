namespace Schuly.Infrastructure.Storage
{
    public class DocumentEncryptionOptions
    {
        public const string SectionName = "Documents";

        public string? EncryptionKey { get; set; }
        public int KeyVersion { get; set; } = 1;
        public Dictionary<int, string> PreviousKeys { get; set; } = new();
        public int ChunkSizeBytes { get; set; } = DocumentCryptoFormat.DefaultChunkSizeBytes;
    }
}
