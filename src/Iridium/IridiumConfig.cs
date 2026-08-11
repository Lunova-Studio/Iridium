using Flurl.Http;
using Flurl.Http.Configuration;
using Iridium.Models;

namespace Iridium;

public static class IridiumConfig {
    public static void Configure(IridiumContext context) {
        FlurlHttp.Clients.WithDefaults(x => {
            x.WithSettings(settings => {
                settings.Timeout = context.Timeout;

                settings.JsonSerializer = new DefaultJsonSerializer();
                
                settings.Redirects.MaxAutoRedirects = 3;
                settings.Redirects.Enabled = true;
            }).WithHeader("User-Agent", context.UserAgent);
        });
    } 
}