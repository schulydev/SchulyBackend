using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Schuly.API.Extensions
{
    /// <summary>
    /// Keeps plugin endpoints out of the published OpenAPI document. The CRM's
    /// generated contract (and the app's generated client) describe only the core
    /// CRM surface; plugin routes under <c>api/plugins/*</c> are a runtime concern
    /// the app reaches via catalog-advertised paths, not the generated client.
    /// Hiding them here also drops their request/response schemas, so no
    /// provider-specific models leak into the client.
    /// </summary>
    public class HidePluginEndpointsConvention : IApplicationModelConvention
    {
        private const string PluginRoutePrefix = "api/plugins";

        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                if (IsPluginController(controller))
                    controller.ApiExplorer.IsVisible = false;
            }
        }

        private static bool IsPluginController(ControllerModel controller)
        {
            foreach (var selector in controller.Selectors)
            {
                var template = selector.AttributeRouteModel?.Template;
                if (template is not null &&
                    template.TrimStart('/').StartsWith(PluginRoutePrefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
