namespace Schuly.Infrastructure.Storage
{
    public class S3Options
    {
        public const string SectionName = "S3";

        public string Endpoint { get; set; } = "";
        public string Bucket { get; set; } = "";
        public string AccessKey { get; set; } = "";
        public string SecretKey { get; set; } = "";

        // SeaweedFS and many on-prem S3 implementations only accept path-style
        // URLs (bucket-as-path). AWS itself accepts both; flip to false there.
        public bool UsePathStyle { get; set; } = true;
    }
}
