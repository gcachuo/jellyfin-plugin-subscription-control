using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Tasks;

public sealed class SubscriptionCacheResetTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<SubscriptionCacheResetTask> _logger;

    public SubscriptionCacheResetTask(IMemoryCache cache, ILogger<SubscriptionCacheResetTask> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public string Name => "EasyMovie: Invalidar cache de suscripciones";

    public string Key => "EasyMovieSubscriptionCacheReset";

    public string Description => "Limpia el cache en memoria del plugin y del API.";

    public string Category => "EasyMovie Subscription";

    public bool IsHidden => false;

    public bool IsEnabled => true;

    public bool IsLogged => true;

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        progress.Report(10);

        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0);
            _logger.LogInformation("Cache local del plugin compactado.");
        }
        else
        {
            _logger.LogWarning("IMemoryCache no es MemoryCache; no se pudo compactar el cache.");
        }

        progress.Report(50);

        var config = Plugin.Instance?.Configuration;
        if (config is not null && !string.IsNullOrWhiteSpace(config.ApiUrl))
        {
            try
            {
                var baseUrl = config.ApiUrl.Split('?')[0];
                var clearCacheUrl = baseUrl.Replace("/subscription.php", "/clear-cache.php");

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await httpClient.GetAsync(clearCacheUrl, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Cache del API limpiado: {Response}", content);
                }
                else
                {
                    _logger.LogWarning("No se pudo limpiar cache del API. Status: {Status}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al llamar al endpoint de limpieza de cache del API");
            }
        }

        progress.Report(100);
        _logger.LogInformation("Tarea de invalidación de cache completada.");
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return Array.Empty<TaskTriggerInfo>();
    }
}
