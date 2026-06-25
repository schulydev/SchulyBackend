using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Schuly.Infrastructure.Storage
{
    public static class StorageServiceCollectionExtensions
    {
        public static IServiceCollection AddSchulyDocumentStorage(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<S3Options>(configuration.GetSection(S3Options.SectionName));

            services.AddSingleton<IAmazonS3>(sp =>
            {
                var o = sp.GetRequiredService<IOptions<S3Options>>().Value;
                var creds = new BasicAWSCredentials(o.AccessKey, o.SecretKey);
                var config = new AmazonS3Config
                {
                    ServiceURL = o.Endpoint,
                    ForcePathStyle = o.UsePathStyle,
                };
                return new AmazonS3Client(creds, config);
            });

            services.AddScoped<IDocumentStorage, S3DocumentStorage>();
            return services;
        }
    }
}
