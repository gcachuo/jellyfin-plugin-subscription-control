using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EasyMovie.Plugin.Api;
using EasyMovie.Plugin.Models;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Tasks;

public sealed class LibraryAccessSyncTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly SubscriptionClient _subscriptionClient;
    private readonly ILogger<LibraryAccessSyncTask> _logger;

    public LibraryAccessSyncTask(
        IUserManager userManager,
        ILibraryManager libraryManager,
        SubscriptionClient subscriptionClient,
        ILogger<LibraryAccessSyncTask> logger)
    {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _subscriptionClient = subscriptionClient;
        _logger = logger;
    }

    public string Name => "EasyMovie: Sincronizar acceso a bibliotecas";

    public string Key => "EasyMovieLibraryAccessSync";

    public string Description => "Sincroniza los permisos de acceso a bibliotecas según el plan de suscripción del usuario.";

    public string Category => "EasyMovie Subscription";

    public bool IsHidden => false;

    public bool IsEnabled => true;

    public bool IsLogged => true;

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // User can configure trigger from Jellyfin UI (Dashboard → Scheduled Tasks)
        // Recommended: Run every 6-12 hours or daily
        return Array.Empty<TaskTriggerInfo>();
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            _logger.LogWarning("Plugin configuration not available; skipping library access sync");
            return;
        }

        _logger.LogInformation("Starting library access sync");
        progress.Report(0);

        var users = _userManager.Users.ToList();
        var totalUsers = users.Count;
        var processedUsers = 0;

        foreach (var user in users)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await SyncUserLibraryAccessAsync(user, config, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync library access for {User}", user.Username);
            }

            processedUsers++;
            progress.Report((double)processedUsers / totalUsers * 100);
        }

        _logger.LogInformation("Library access sync completed");
        progress.Report(100);
    }

    private async Task SyncUserLibraryAccessAsync(User user, Configuration.PluginConfiguration config, CancellationToken cancellationToken)
    {
        var status = await _subscriptionClient.GetStatusAsync(user, config, cancellationToken).ConfigureAwait(false);
        if (status.FailSafe)
        {
            _logger.LogWarning("Skipping library access update for {User} due to fail-safe status", user.Username);
            return;
        }

        // Check test mode
        if (status.TestMode && !status.TestUsers.Contains(user.Username, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Skipping {User} - not in test users list (test mode active)", user.Username);
            return;
        }

        var policy = await GetUserPolicyAsync(user, cancellationToken).ConfigureAwait(false);
        if (policy is null)
        {
            _logger.LogWarning("User policy not found for {User}; skipping library access sync", user.Username);
            return;
        }

        var planInfo = status.Plan;
        if (planInfo is null)
        {
            _logger.LogWarning("Plan info not found for {User}; skipping library access sync", user.Username);
            return;
        }

        // Apply library access
        if (!TrySetLibraryAccess(policy, planInfo, out var currentEnableAll, out var currentFolders))
        {
            _logger.LogWarning("Failed to set library access for {User}", user.Username);
            return;
        }

        var changed = currentEnableAll != planInfo.EnableAllFolders || 
                      !AreEqual(currentFolders, planInfo.EnabledFolderIds);

        if (!changed)
        {
            _logger.LogDebug("Library access for {User} is already up to date", user.Username);
            return;
        }

        await PersistUserPolicyAsync(user, policy, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Library access updated for {User}: EnableAll={EnableAll}, Folders={Folders}",
            user.Username,
            planInfo.EnableAllFolders,
            string.Join(", ", planInfo.EnabledFolderIds ?? Array.Empty<string>()));
    }

    private static bool AreEqual(string[]? current, string[]? target)
    {
        if (current is null && target is null) return true;
        if (current is null || target is null) return false;
        if (current.Length != target.Length) return false;
        
        var currentSet = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
        return target.All(id => currentSet.Contains(id));
    }

    private bool TrySetLibraryAccess(object policy, PlanInfo planInfo, out bool currentEnableAll, out string[]? currentFolders)
    {
        currentEnableAll = false;
        currentFolders = null;

        var policyType = policy.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Get EnableAllFolders property
        var enableAllProp = policyType.GetProperty("EnableAllFolders", flags);
        if (enableAllProp is null)
        {
            _logger.LogWarning("EnableAllFolders property not found on policy");
            return false;
        }

        // Get EnabledFolders property
        var enabledFoldersProp = policyType.GetProperty("EnabledFolders", flags);
        if (enabledFoldersProp is null)
        {
            _logger.LogWarning("EnabledFolders property not found on policy");
            return false;
        }

        // Read current values
        currentEnableAll = enableAllProp.GetValue(policy) is bool currentBool && currentBool;
        currentFolders = enabledFoldersProp.GetValue(policy) as string[];

        // Set new values
        enableAllProp.SetValue(policy, planInfo.EnableAllFolders);
        enabledFoldersProp.SetValue(policy, planInfo.EnabledFolderIds ?? Array.Empty<string>());

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
            var method = methods.FirstOrDefault(m =>
                m.Name == candidate.Name && m.GetParameters().Length == candidate.Params);
            if (method is null) continue;

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

        _logger.LogWarning("Unable to persist library access policy for {User}; no compatible method found", user.Username);
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
            return policy is not null && HasLibraryAccessProperties(policy) ? policy : null;
        }

        var field = userType.GetField("Policy", flags)
            ?? userType.GetField("UserPolicy", flags);
        var fieldPolicy = field?.GetValue(user);
        return fieldPolicy is not null && HasLibraryAccessProperties(fieldPolicy) ? fieldPolicy : null;
    }

    private async Task<object?> GetUserPolicyFromManagerAsync(User user, CancellationToken cancellationToken)
    {
        var managerType = _userManager.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var methods = managerType.GetMethods(flags)
            .Where(method => method.Name.Contains("Policy", StringComparison.OrdinalIgnoreCase)
                && method.Name.Contains("Get", StringComparison.OrdinalIgnoreCase)
                && method.GetParameters().Length >= 1
                && method.GetParameters().Length <= 2)
            .OrderBy(method => method.Name.Contains("GetUserPolicy", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();

        foreach (var method in methods)
        {
            try
            {
                var parameters = method.GetParameters();
                if (parameters.Length > 2)
                {
                    continue;
                }

                object?[] args = parameters.Length switch
                {
                    1 => new object?[] { user },
                    2 => new object?[] { user, cancellationToken },
                    _ => Array.Empty<object?>()
                };

                if (args.Length == 0)
                {
                    continue;
                }

                var result = method.Invoke(_userManager, args);
                var unwrapped = await UnwrapAsyncResult(result).ConfigureAwait(false);
                if (unwrapped is null)
                {
                    continue;
                }

                var policy = TryGetPolicyFromUserObject(unwrapped) ?? unwrapped;
                if (policy is not null && HasLibraryAccessProperties(policy))
                {
                    return policy;
                }
            }
            catch
            {
                continue;
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
            return policy is not null && HasLibraryAccessProperties(policy) ? policy : null;
        }

        var field = userType.GetField("Policy", flags)
            ?? userType.GetField("UserPolicy", flags);
        var fieldPolicy = field?.GetValue(userObject);
        return fieldPolicy is not null && HasLibraryAccessProperties(fieldPolicy) ? fieldPolicy : null;
    }

    private static bool HasLibraryAccessProperties(object policy)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var policyType = policy.GetType();
        
        var hasEnableAll = policyType.GetProperty("EnableAllFolders", flags) is not null;
        var hasEnabledFolders = policyType.GetProperty("EnabledFolders", flags) is not null;
        
        return hasEnableAll && hasEnabledFolders;
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

        return result;
    }
}
