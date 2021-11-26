using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Lightcode.AlertService
{
    public static class AlertServiceConfiguration
    {
        public static void Setup(this IServiceCollection services, IConfiguration configuration, string configurationSectionKey = "AlertService")
        {
            services.AddScoped<IAlertService, AlertService>();
            services.Configure<AlertServiceOptions>((o) => configuration.GetSection(configurationSectionKey));
        }
    }
}
