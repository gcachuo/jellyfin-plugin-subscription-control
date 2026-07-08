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

            var policy = await GetUserPolicyAsync(user, cancellationToken).ConfigureAwait(false);
            if (policy is null)
            {
                _logger.LogWarning("Live TV policy not found for {User}; skipping", user.Username);
                continue;
            }

            if (!TrySetLiveTvAccess(policy, allowLiveTv.Value, out var currentValue))
            {
                _logger.LogWarning("Live TV policy flag not found for {User}; skipping", user.Username);
                continue;
            }

            if (currentValue == allowLiveTv.Value)
            {
                continue;
            }

            await PersistUserPolicyAsync(user, policy, cancellationToken).ConfigureAwait(false);
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

    private static bool TrySetLiveTvAccess(object policy, bool allowLiveTv, out bool currentValue)
    {
        currentValue = allowLiveTv;
        var property = FindLiveTvAccessProperty(policy);
        if (property is null)
        {
            return false;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType != typeof(bool))
        {
            return false;
        }

        var current = property.GetValue(policy);
        currentValue = current is bool currentBool && currentBool;
        property.SetValue(policy, allowLiveTv);
        return true;
    }

    private async Task PersistUserPolicyAsync(User user, object policy, CancellationToken cancellationToken)
    {
        var managerType = _userManager.GetType();
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

    private async Task<object?> GetUserPolicyAsync(User user, CancellationToken cancellationToken)
    {
        var policy = GetUserPolicyFromUser(user);
        if (policy is not null)
        {
            return policy;
        }

        return await GetUserPolicyFromManagerAsync(user, cancellationToken).ConfigureAwait(false);
    }

    private static object? GetUserPolicyFromUser(User user)
    {
        var userType = user.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var property = userType.GetProperty("Policy", flags)
            ?? userType.GetProperty("UserPolicy", flags);
        if (property is not null)
        {
            var policy = property.GetValue(user);
            return policy is not null && HasLiveTvAccessProperty(policy) ? policy : null;
        }

        var field = userType.GetField("Policy", flags)
            ?? userType.GetField("UserPolicy", flags);
        var fieldPolicy = field?.GetValue(user);
        return fieldPolicy is not null && HasLiveTvAccessProperty(fieldPolicy) ? fieldPolicy : null;
    }

    private async Task<object?> GetUserPolicyFromManagerAsync(User user, CancellationToken cancellationToken)
    {
        var managerType = _userManager.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var candidates = BuildPolicyLookupCandidates(user).ToArray();

        var policyFromUser = await TryGetPolicyFromUserManagerAsync(managerType, flags, candidates).ConfigureAwait(false);
        if (policyFromUser is not null)
        {
            return policyFromUser;
        }

        var methods = managerType.GetMethods(flags)
            .Where(method => method.Name.Contains("Policy", StringComparison.OrdinalIgnoreCase)
                && method.Name.Contains("Get", StringComparison.OrdinalIgnoreCase)
                && method.GetParameters().Length >= 1
                && method.GetParameters().Length <= 2)
            .OrderBy(method => method.Name.Contains("GetUserPolicy", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            var paramType = parameters[0].ParameterType;
            var argument = candidates.FirstOrDefault(candidate => candidate is not null && paramType.IsInstanceOfType(candidate));
            if (argument is null)
            {
                continue;
            }

            object?[] args;
            if (parameters.Length == 2 && parameters[1].ParameterType == typeof(CancellationToken))
            {
                args = new object?[] { argument, cancellationToken };
            }
            else
            {
                args = new object?[] { argument };
            }

            var result = method.Invoke(_userManager, args);
            var unwrapped = await UnwrapAsyncResult(result).ConfigureAwait(false);
            if (unwrapped is null)
            {
                continue;
            }

            var policy = TryGetPolicyFromUserObject(unwrapped) ?? unwrapped;
            if (policy is not null && HasLiveTvAccessProperty(policy))
            {
                return policy;
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        return null;
    }

    private async Task<object?> TryGetPolicyFromUserManagerAsync(
        Type managerType,
        BindingFlags flags,
        object?[] candidates)
    {
        var userMethods = managerType.GetMethods(flags)
            .Where(method => method.Name.Contains("GetUserBy", StringComparison.OrdinalIgnoreCase)
                && method.GetParameters().Length == 1)
            .OrderBy(method => method.Name.Contains("GetUserById", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();

        foreach (var method in userMethods)
        {
            var paramType = method.GetParameters()[0].ParameterType;
            var argument = candidates.FirstOrDefault(candidate => candidate is not null && paramType.IsInstanceOfType(candidate));
            if (argument is null)
            {
                continue;
            }

            var result = method.Invoke(_userManager, new[] { argument });
            var userResult = await UnwrapAsyncResult(result).ConfigureAwait(false);
            if (userResult is null)
            {
                continue;
            }

            var policy = TryGetPolicyFromUserObject(userResult);
            if (policy is not null)
            {
                return policy;
            }
        }

        return null;
    }

    private static object? TryGetPolicyFromUserObject(object userObject)
    {
        var userType = userObject.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var property = userType.GetProperty("Policy", flags)
            ?? userType.GetProperty("UserPolicy", flags);
        if (property is not null)
        {
            var policy = property.GetValue(userObject);
            return policy is not null && HasLiveTvAccessProperty(policy) ? policy : null;
        }

        var field = userType.GetField("Policy", flags)
            ?? userType.GetField("UserPolicy", flags);
        var fieldPolicy = field?.GetValue(userObject);
        return fieldPolicy is not null && HasLiveTvAccessProperty(fieldPolicy) ? fieldPolicy : null;
    }

    private static bool HasLiveTvAccessProperty(object policy)
    {
        return FindLiveTvAccessProperty(policy) is not null;
    }

    private static PropertyInfo? FindLiveTvAccessProperty(object policy)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var policyType = policy.GetType();
        var property = policyType.GetProperty("EnableLiveTvAccess", flags)
            ?? policyType.GetProperty("EnableLiveTv", flags)
            ?? policyType.GetProperty("LiveTvEnabled", flags);
        if (property is not null)
        {
            return property;
        }

        return policyType
            .GetProperties(flags)
            .FirstOrDefault(candidate =>
            {
                var candidateType = Nullable.GetUnderlyingType(candidate.PropertyType) ?? candidate.PropertyType;
                if (candidateType != typeof(bool))
                {
                    return false;
                }

                var name = candidate.Name;
                return name.Contains("LiveTv", StringComparison.OrdinalIgnoreCase)
                    && name.Contains("Access", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static async Task<object?> UnwrapAsyncResult(object? result)
    {
        if (result is null)
        {
            return null;
        }

        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty?.GetValue(task);
        }

        if (result is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
            return null;
        }

        var resultType = result.GetType();
        if (resultType.IsValueType && resultType.FullName?.StartsWith("System.Threading.Tasks.ValueTask", StringComparison.Ordinal) == true)
        {
            var asTask = resultType.GetMethod("AsTask", BindingFlags.Instance | BindingFlags.Public);
            if (asTask is not null)
            {
                var awaited = (Task)asTask.Invoke(result, null)!;
                await awaited.ConfigureAwait(false);
                var resultProperty = awaited.GetType().GetProperty("Result");
                return resultProperty?.GetValue(awaited);
            }
        }

        return result;
    }

    private static object?[] BuildPolicyLookupCandidates(User user)
    {
        var results = new object?[]
        {
            user,
            TryGetUserId(user),
            TryGetUserIdString(user),
            TryGetUsername(user)
        };

        return results.Where(item => item is not null).ToArray();
    }

    private static Guid? TryGetUserId(User user)
    {
        var property = user.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.GetValue(user) is Guid guid)
        {
            return guid;
        }

        if (property?.GetValue(user) is string idString && Guid.TryParse(idString, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? TryGetUserIdString(User user)
    {
        var id = TryGetUserId(user);
        if (id.HasValue)
        {
            return id.Value.ToString("N");
        }

        return null;
    }

    private static string? TryGetUsername(User user)
    {
        var property = user.GetType().GetProperty("Username", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? user.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property?.GetValue(user)?.ToString();
    }
}
