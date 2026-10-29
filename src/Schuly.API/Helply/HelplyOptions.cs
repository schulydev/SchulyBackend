namespace Schuly.API.Helply
{
    /// <summary>
    /// Helply's OpenAI-compatible chat-completions gateway, from <c>Helply__BaseUrl</c>, <c>Helply__ApiKey</c> and optional <c>Helply__Model</c>. None of these
    /// has a default and none belongs in appsettings. They are secrets of the deployment: no API response, error message or log line may contain the URL, key or model.
    /// </summary>
    public class HelplyOptions
    {
        public const string SectionName = "Helply";

        public string? BaseUrl { get; set; }
        public string? ApiKey { get; set; }
        public string? Model { get; set; }
    }

    /// <summary>Whether Helply can run. Decided once at startup; a missing setting only switches Helply off, it never stops the server.</summary>
    public sealed class HelplyAvailability
    {
        public bool IsEnabled { get; init; }
    }

    public static class HelplyExtensions
    {
        public static IServiceCollection AddSchulyHelply(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<HelplyOptions>(configuration.GetSection(HelplyOptions.SectionName));

            var options = configuration.GetSection(HelplyOptions.SectionName).Get<HelplyOptions>() ?? new HelplyOptions();
            var enabled = Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && !string.IsNullOrWhiteSpace(options.ApiKey);
            services.AddSingleton(new HelplyAvailability { IsEnabled = enabled });
            services.AddHostedService<HelplyStartupCheck>();
            return services;
        }
    }

    /// <summary>Says once whether Helply is on. It names the missing setting, never a value.</summary>
    internal sealed class HelplyStartupCheck(HelplyAvailability availability, ILogger<HelplyStartupCheck> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (availability.IsEnabled)
                logger.LogInformation("Helply is enabled");
            else
                logger.LogWarning("Helply is disabled: set Helply__BaseUrl (an absolute http or https URL) and Helply__ApiKey to enable it");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
