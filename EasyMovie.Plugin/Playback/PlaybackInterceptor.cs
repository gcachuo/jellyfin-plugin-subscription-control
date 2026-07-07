using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EasyMovie.Plugin.Api;
using EasyMovie.Plugin.Configuration;
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Playback;

public sealed class PlaybackInterceptor : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly SubscriptionClient _subscriptionClient;
    private readonly ILogger<PlaybackInterceptor> _logger;

    public PlaybackInterceptor(
        ISessionManager sessionManager,
        ILibraryManager libraryManager,
        SubscriptionClient subscriptionClient,
        ILogger<PlaybackInterceptor> logger)
    {
        _sessionManager = sessionManager;
        _libraryManager = libraryManager;
        _subscriptionClient = subscriptionClient;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStart;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
    }

    private async void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        if (e.Item is null || e.Users.Count == 0)
        {
            return;
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return;
        }

        if (IsPluginVideo(e.Item.Path, config))
        {
            return;
        }

        var user = e.Users[0];
        var status = await _subscriptionClient.GetStatusAsync(user, config, CancellationToken.None).ConfigureAwait(false);
        if (!status.IsExpired)
        {
            return;
        }

        var expiredPath = config.Videos.Expired;
        if (string.IsNullOrWhiteSpace(expiredPath) || !File.Exists(expiredPath))
        {
            _logger.LogWarning("Expired video path not configured or missing. Stopping playback only.");
            await StopPlaybackAsync(e).ConfigureAwait(false);
            return;
        }

        var expiredItem = _libraryManager.FindByPath(expiredPath, isFolder: false);
        if (expiredItem is null)
        {
            _logger.LogWarning("Expired video is not indexed in Jellyfin library: {Path}", expiredPath);
            await StopPlaybackAsync(e).ConfigureAwait(false);
            return;
        }

        try
        {
            await StopPlaybackAsync(e).ConfigureAwait(false);

            var playRequest = new PlayRequest
            {
                ItemIds = new[] { expiredItem.Id },
                PlayCommand = PlayCommand.PlayNow,
                StartPositionTicks = 0,
                ControllingUserId = user.Id
            };

            var sessionId = GetSessionId(e);
            await _sessionManager.SendPlayCommand(sessionId, sessionId, playRequest, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replace playback with expired video");
        }
    }

    private async Task StopPlaybackAsync(PlaybackProgressEventArgs e)
    {
        try
        {
            var sessionId = GetSessionId(e);
            var request = new PlaystateRequest
            {
                Command = PlaystateCommand.Stop
            };
            await _sessionManager.SendPlaystateCommand(sessionId, sessionId, request, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop playback for expired user");
        }
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

    private string GetSessionId(PlaybackProgressEventArgs e)
    {
        var deviceId = e.DeviceId ?? string.Empty;
        var clientName = e.ClientName ?? string.Empty;
        var session = _sessionManager.GetSession(deviceId, clientName, string.Empty);
        return session.Id;
    }
}
