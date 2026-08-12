using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Schuly.API.Extensions
{
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
