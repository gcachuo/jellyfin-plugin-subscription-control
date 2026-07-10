using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyMovie.Plugin.Api;
using EasyMovie.Plugin.Models;
using EasyMovie.Plugin.Services;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Tasks;

public sealed class LibraryAccessSyncTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly IUserManager _userManager;
    private readonly UserPolicyService _policyService;
    private readonly SubscriptionClient _subscriptionClient;
    private readonly ILogger<LibraryAccessSyncTask> _logger;

    public LibraryAccessSyncTask(
        IUserManager userManager,
        UserPolicyService policyService,
        SubscriptionClient subscriptionClient,
        ILogger<LibraryAccessSyncTask> logger)
    {
        _userManager = userManager;
        _policyService = policyService;
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
        // Run every 12 hours by default
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(12).Ticks
        };
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
        _logger.LogDebug("Processing user {User} (ID: {UserId})", user.Username, user.Id);
        
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

        if (status.TestMode)
        {
            _logger.LogInformation("Syncing library access for {User} (test mode active)", user.Username);
        }
        else
        {
            _logger.LogDebug("Syncing library access for {User}", user.Username);
        }

        var policy = await _policyService.GetUserPolicyAsync(user).ConfigureAwait(false);
        if (policy is null)
        {
            _logger.LogWarning("User policy not found for {User}; skipping library access sync", user.Username);
            return;
        }

        var planInfo = status.Plan;
        
        // Get current Live TV access
        var currentLiveTvAccess = policy.EnableLiveTvAccess;
        
        bool targetEnableAll;
        string[] targetFolders;
        bool targetLiveTv;
        
        if (planInfo is null)
        {
            // No plan = Trial/New user = Full access to everything
            _logger.LogDebug("No plan assigned to {User}; granting full access (trial mode)", user.Username);
            targetEnableAll = true;
            targetFolders = Array.Empty<string>();
            targetLiveTv = true;
        }
        else
        {
            // Has plan = Apply plan restrictions
            targetEnableAll = planInfo.EnableAllFolders;
            targetFolders = planInfo.EnabledFolderIds ?? Array.Empty<string>();
            targetLiveTv = planInfo.AllowLiveTv;
        }

        // Get current access settings
        var currentEnableAll = policy.EnableAllFolders;
        var currentFolderGuids = policy.EnabledFolders ?? Array.Empty<Guid>();
        var currentFolders = currentFolderGuids.Select(g => g.ToString()).ToArray();
        
        // Check if changes are needed
        var changed = currentEnableAll != targetEnableAll || 
                      !AreEqual(currentFolders, targetFolders) ||
                      currentLiveTvAccess != targetLiveTv;

        if (!changed)
        {
            _logger.LogDebug("Library and Live TV access for {User} is already up to date", user.Username);
            return;
        }

        // Apply library access changes
        if (!_policyService.TrySetLibraryAccess(policy, targetEnableAll, targetFolders, out _, out _))
        {
            _logger.LogWarning("Failed to set library access for {User}", user.Username);
            return;
        }

        // Apply Live TV access changes
        _policyService.SetLiveTvAccess(policy, targetLiveTv);

        var updated = await _policyService.UpdateUserPolicyAsync(user, policy).ConfigureAwait(false);
        if (updated)
        {
            _logger.LogInformation(
                "Access updated for {User}: EnableAll={EnableAll}, Folders={Folders}, LiveTV={LiveTV}",
                user.Username,
                targetEnableAll,
                string.Join(", ", targetFolders),
                targetLiveTv);
        }
        else
        {
            _logger.LogWarning("Failed to persist access changes for {User}", user.Username);
        }
    }

    private static bool AreEqual(string[]? current, string[]? target)
    {
        if (current is null && target is null) return true;
        if (current is null || target is null) return false;
        if (current.Length != target.Length) return false;
        
        var currentSet = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
        return target.All(id => currentSet.Contains(id));
    }
}
