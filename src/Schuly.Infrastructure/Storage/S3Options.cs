namespace Schuly.Infrastructure.Storage
{
    public class S3Options
    {
        public const string SectionName = "S3";

        public string Endpoint { get; set; } = "";
        public string Bucket { get; set; } = "";
        public string AccessKey { get; set; } = "";
        public string SecretKey { get; set; } = "";

        public bool UsePathStyle { get; set; } = true;
    }
}
