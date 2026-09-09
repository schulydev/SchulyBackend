using System.Text;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Schuly.API.Services.Notifications;

namespace Schuly.API.Extensions
{
    public static class NotificationExtensions
    {
        public static IServiceCollection AddSchulyPushNotifications(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<NotificationDispatcher>();
            services.AddHostedService<NotificationDispatcherService>();

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger(nameof(NotificationExtensions));

            var json = ReadCredentialJson(configuration);

            if (string.IsNullOrWhiteSpace(json))
            {
                logger.LogInformation("Push notifications disabled: Firebase is not configured (Firebase:ServiceAccountJson / Firebase:ServiceAccountPath are unset)");
                services.AddSingleton<IPushSender, NoOpPushSender>();
                return services;
            }

            try
            {
                var credential = CredentialFactory.FromJson<ServiceAccountCredential>(json).ToGoogleCredential();
                var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions { Credential = credential });
                services.AddSingleton(app);
                services.AddSingleton<IPushSender, FirebasePushSender>();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize the Firebase credential; push notifications are disabled");
                services.AddSingleton<IPushSender, NoOpPushSender>();
            }

            return services;
        }

        private static string? ReadCredentialJson(IConfiguration configuration)
        {
            var base64Json = configuration["Firebase:ServiceAccountJson"];
            if (!string.IsNullOrWhiteSpace(base64Json))
                return Encoding.UTF8.GetString(Convert.FromBase64String(base64Json));

            var path = configuration["Firebase:ServiceAccountPath"];
            if (!string.IsNullOrWhiteSpace(path))
                return File.ReadAllText(path);

            return null;
        }
    }
}
