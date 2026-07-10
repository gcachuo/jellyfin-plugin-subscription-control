using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyMovie.Plugin.Configuration;
using EasyMovie.Plugin.Models;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Api;

public sealed class SubscriptionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SubscriptionClient> _logger;

    public SubscriptionClient(IMemoryCache cache, IHttpClientFactory httpClientFactory, ILogger<SubscriptionClient> logger)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<SubscriptionStatus> GetStatusAsync(User user, PluginConfiguration config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.ApiUrl))
        {
            return CreateFailSafe("API URL not configured");
        }

        var cacheKey = $"subscription:{user.Id:N}:{config.ExpiringThresholdDays}:{config.TrialMaxDurationDays}";
        if (_cache.TryGetValue(cacheKey, out SubscriptionStatus? cached) && cached is not null)
        {
            cached.Cached = true;
            return cached;
        }

        try
        {
            var url = BuildUrl(config, user.Id.ToString("N"), user.Username);
            
            using var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
            httpClient.Timeout = TimeSpan.FromSeconds(config.ApiTimeoutSeconds);
            
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return CreateFailSafe($"API status {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var status = await JsonSerializer.DeserializeAsync<SubscriptionStatus>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (status is null)
            {
                return CreateFailSafe("Empty response");
            }

            if (status.FailSafe)
            {
                _logger.LogWarning("EasyMovie API returned fail-safe mode for user {UserId}", user.Id);
            }

            if (config.CacheDurationMinutes > 0)
            {
                _cache.Set(cacheKey, status, TimeSpan.FromMinutes(config.CacheDurationMinutes));
            }

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch subscription status for user {UserId}", user.Id);
            return CreateFailSafe(ex.Message);
        }
    }

    private static string BuildUrl(PluginConfiguration config, string userId, string? username)
    {
        var separator = config.ApiUrl.Contains("?") ? "&" : "?";
        var url = $"{config.ApiUrl}{separator}userId={Uri.EscapeDataString(userId)}&expiringDays={config.ExpiringThresholdDays}&trialMaxDays={config.TrialMaxDurationDays}&cacheMinutes={config.CacheDurationMinutes}";
        if (!string.IsNullOrWhiteSpace(username))
        {
            url += $"&username={Uri.EscapeDataString(username)}";
        }
        return url;
    }

    private static SubscriptionStatus CreateFailSafe(string message) => new()
    {
        Status = "active",
        FailSafe = true,
        Error = message
    };
}
