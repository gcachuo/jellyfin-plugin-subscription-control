using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EasyMovie.Plugin.Api;
using EasyMovie.Plugin.Configuration;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Providers;

public sealed class SubscriptionIntroProvider : IIntroProvider
{
    private readonly SubscriptionClient _subscriptionClient;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<SubscriptionIntroProvider> _logger;

    public SubscriptionIntroProvider(
        SubscriptionClient subscriptionClient,
        ILibraryManager libraryManager,
        ILogger<SubscriptionIntroProvider> logger)
    {
        _subscriptionClient = subscriptionClient;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public string Name => "EasyMovie Subscription";

    public async Task<IEnumerable<IntroInfo>> GetIntros(BaseItem item, User user)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return Enumerable.Empty<IntroInfo>();
        }

        if (IsPluginVideo(item.Path, config))
        {
            return Enumerable.Empty<IntroInfo>();
        }

        var status = await _subscriptionClient.GetStatusAsync(user, config, default).ConfigureAwait(false);
        if (status.IsExpired)
        {
            return Enumerable.Empty<IntroInfo>();
        }

        var path = status.IsCourtesy
            ? config.Videos.Courtesy
            : status.IsExpiring
                ? config.Videos.Expiring
                : config.Videos.Active;

        if (string.IsNullOrWhiteSpace(path))
        {
            return Enumerable.Empty<IntroInfo>();
        }

        var introInfo = BuildIntroInfo(path);
        if (introInfo is null)
        {
            return Enumerable.Empty<IntroInfo>();
        }

        _logger.LogInformation("Queued subscription intro ({Status}) for {Item}", status.Status, item.Name);
        return new[] { introInfo };
    }

    private IntroInfo? BuildIntroInfo(string path)
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("Intro video not found at {Path}", path);
            return null;
        }

        try
        {
            var libraryItem = _libraryManager.FindByPath(path, isFolder: false);
            if (libraryItem is not null)
            {
                return new IntroInfo
                {
                    Path = libraryItem.Path,
                    ItemId = libraryItem.Id
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve intro path {Path}", path);
        }

        return new IntroInfo
        {
            Path = path,
            ItemId = null
        };
    }

    private static bool IsPluginVideo(string? path, PluginConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return string.Equals(path, config.Videos.Active, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, config.Videos.Expiring, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, config.Videos.Expired, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, config.Videos.Courtesy, StringComparison.OrdinalIgnoreCase);
    }
}
