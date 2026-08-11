using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Web;

public sealed class JavaScriptInjectorRegistrationService : IHostedService
{
    private const string ScriptId = "easymovie-preroll-overlay";
    private const string InjectorInterfaceName = "Jellyfin.Plugin.JavaScriptInjector.PluginInterface";
    private readonly ILogger<JavaScriptInjectorRegistrationService> _logger;

    public JavaScriptInjectorRegistrationService(ILogger<JavaScriptInjectorRegistrationService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var injectorInterface = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .Select(assembly => assembly.GetType(InjectorInterfaceName, throwOnError: false))
                .FirstOrDefault(type => type is not null);
            var registerScript = injectorInterface?.GetMethod("RegisterScript", BindingFlags.Public | BindingFlags.Static);
            var payloadType = registerScript?.GetParameters().SingleOrDefault()?.ParameterType;
            var parsePayload = payloadType?.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
            if (registerScript is null || parsePayload is null)
            {
                _logger.LogWarning("EasyMovie: JavaScript Injector was not found; web overlay remains unavailable");
                return Task.CompletedTask;
            }

            var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["id"] = ScriptId,
                ["name"] = "EasyMovie preroll overlay",
                ["script"] = BuildLoaderScript(),
                ["enabled"] = true,
                ["requiresAuthentication"] = true,
                ["pluginId"] = Plugin.Instance?.Id.ToString(),
                ["pluginName"] = Plugin.Instance?.Name,
                ["pluginVersion"] = Plugin.Instance?.Version.ToString()
            });
            var parsedPayload = parsePayload.Invoke(null, [payload]);
            var registered = registerScript.Invoke(null, [parsedPayload]) as bool?;
            if (registered == true)
            {
                _logger.LogInformation("EasyMovie: Registered web preroll overlay with JavaScript Injector");
            }
            else
            {
                _logger.LogWarning("EasyMovie: JavaScript Injector did not register the web preroll overlay");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EasyMovie: Failed to register the web preroll overlay");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string BuildLoaderScript()
    {
        return "(() => { const load = () => { const api = window.ApiClient; if (document.querySelector('script[data-easymovie-preroll]')) { console.info('[EasyMovie] loader: already loaded'); return true; } if (!api || !api.getUrl) { console.info('[EasyMovie] loader: ApiClient not ready yet'); return false; } const script = document.createElement('script'); script.src = api.getUrl('EasyMoviePreroll/overlay.js'); script.dataset.easymoviePreroll = 'true'; script.onload = () => console.info('[EasyMovie] loader: overlay.js loaded'); script.onerror = (e) => console.warn('[EasyMovie] loader: overlay.js failed to load', e); document.head.appendChild(script); console.info('[EasyMovie] loader: injected overlay.js script tag, src=', script.src); return true; }; const start = () => { console.info('[EasyMovie] loader: starting, document.readyState=', document.readyState); if (load()) return; let attempts = 0; const retry = window.setInterval(() => { attempts++; if (load()) { window.clearInterval(retry); } else if (attempts >= 40) { console.warn('[EasyMovie] loader: gave up after 40 attempts (10s)'); window.clearInterval(retry); } }, 250); }; if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true }); else start(); })();";
    }
}
