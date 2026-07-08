using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EasyMovie.Plugin.Api;
using EasyMovie.Plugin.Models;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Playback;

public sealed class LiveTvPolicySyncService : BackgroundService
{
    private readonly IUserManager _userManager;
    private readonly SubscriptionClient _subscriptionClient;
    private readonly ILogger<LiveTvPolicySyncService> _logger;

    public LiveTvPolicySyncService(
        IUserManager userManager,
        SubscriptionClient subscriptionClient,
        ILogger<LiveTvPolicySyncService> logger)
    {
        _userManager = userManager;
        _subscriptionClient = subscriptionClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncPoliciesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync Live TV policies");
            }

            var intervalMinutes = Plugin.Instance?.Configuration?.CacheDurationMinutes ?? 10;
            if (intervalMinutes <= 0)
            {
                intervalMinutes = 10;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }
    }

    private async Task SyncPoliciesAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return;
        }

        foreach (var user in _userManager.Users)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var status = await _subscriptionClient.GetStatusAsync(user, config, cancellationToken).ConfigureAwait(false);
            if (status.FailSafe)
            {
                _logger.LogWarning("Skipping Live TV policy update for {User} due to fail-safe status", user.Username);
                continue;
            }

            var allowLiveTv = GetLiveTvAccessDecision(status);
            if (!allowLiveTv.HasValue)
            {
                _logger.LogWarning("Unknown status '{Status}' for {User}; skipping Live TV policy update", status.Status, user.Username);
                continue;
            }

            if (!TrySetLiveTvAccess(user, allowLiveTv.Value, out var currentValue))
            {
                _logger.LogWarning("Live TV policy flag not found for {User}; skipping", user.Username);
                continue;
            }

            if (currentValue == allowLiveTv.Value)
            {
                continue;
            }

            await PersistUserPolicyAsync(user, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Live TV access for {User} set to {Enabled}", user.Username, allowLiveTv.Value);
        }
    }

    private static bool? GetLiveTvAccessDecision(SubscriptionStatus status)
    {
        var normalized = (status.Status ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "active" => true,
            "courtesy" => true,
            "expiring" => true,
            "expired" => false,
            _ => null
        };
    }

    private static bool TrySetLiveTvAccess(User user, bool allowLiveTv, out bool currentValue)
    {
        currentValue = allowLiveTv;
        var policy = GetUserPolicy(user);
        if (policy is null)
        {
            return false;
        }

        var policyType = policy.GetType();
        var property = policyType.GetProperty("EnableLiveTvAccess", BindingFlags.Instance | BindingFlags.Public)
            ?? policyType.GetProperty("EnableLiveTv", BindingFlags.Instance | BindingFlags.Public);
        if (property is null || property.PropertyType != typeof(bool))
        {
            return false;
        }

        currentValue = (bool) property.GetValue(policy)!;
        property.SetValue(policy, allowLiveTv);
        return true;
    }

    private async Task PersistUserPolicyAsync(User user, CancellationToken cancellationToken)
    {
        var managerType = _userManager.GetType();
        var policy = GetUserPolicy(user);
        var methods = managerType.GetMethods(BindingFlags.Instance | BindingFlags.Public);

        var candidates = new (string Name, int Params, bool IncludePolicy, bool IncludeToken)[]
        {
            ("UpdateUserPolicyAsync", 3, true, true),
            ("UpdateUserPolicyAsync", 2, true, false),
            ("UpdateUserPolicy", 2, true, false),
            ("UpdateUserAsync", 2, false, true),
            ("UpdateUserAsync", 1, false, false),
            ("UpdateUser", 1, false, false)
        };

        foreach (var candidate in candidates)
        {
            var method = methods.FirstOrDefault(m => m.Name == candidate.Name && m.GetParameters().Length == candidate.Params);
            if (method is null)
            {
                continue;
            }

            if (candidate.IncludePolicy && policy is null)
            {
                continue;
            }

            object?[] args = candidate switch
            {
                { IncludePolicy: true, IncludeToken: true } => new object?[] { user, policy, cancellationToken },
                { IncludePolicy: true, IncludeToken: false } => new object?[] { user, policy },
                { IncludePolicy: false, IncludeToken: true } => new object?[] { user, cancellationToken },
                _ => new object?[] { user }
            };

            var result = method.Invoke(_userManager, args);
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
            }

            return;
        }

        _logger.LogWarning("Unable to persist Live TV policy for {User}; no compatible method found", user.Username);
    }

    private static object? GetUserPolicy(User user)
    {
        var userType = user.GetType();
        var property = userType.GetProperty("Policy", BindingFlags.Instance | BindingFlags.Public)
            ?? userType.GetProperty("UserPolicy", BindingFlags.Instance | BindingFlags.Public);
        return property?.GetValue(user);
    }
}
