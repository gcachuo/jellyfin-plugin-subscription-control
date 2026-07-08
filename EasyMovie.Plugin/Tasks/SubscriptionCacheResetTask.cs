using System;
using System.Collections.Generic;
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

    public string Description => "Limpia el cache en memoria usado para los estatus de suscripción.";

    public string Category => "EasyMovie Subscription";

    public bool IsHidden => false;

    public bool IsEnabled => true;

    public bool IsLogged => true;

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0);
        }
        else
        {
            _logger.LogWarning("IMemoryCache no es MemoryCache; no se pudo compactar el cache.");
        }

        progress.Report(100);
        _logger.LogInformation("Cache de suscripciones invalidado manualmente.");
        return Task.CompletedTask;
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return Array.Empty<TaskTriggerInfo>();
    }
}
