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
        ILogger<LibraryAccessSyncTask> _logger)
    {
        _userManager = userManager;
        _policyService = policyService;
        _subscriptionClient = subscriptionClient;
        this._logger = _logger;
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

        _logger.LogInformation("Syncing library access for {User} (test mode: {TestMode})", user.Username, status.TestMode);

        var policy = await _policyService.GetUserPolicyAsync(user).ConfigureAwait(false);
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
        if (!_policyService.TrySetLibraryAccess(policy, planInfo.EnableAllFolders, planInfo.EnabledFolderIds ?? Array.Empty<string>(), out var currentEnableAll, out var currentFolders))
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

        var updated = await _policyService.UpdateUserPolicyAsync(user, policy).ConfigureAwait(false);
        if (updated)
        {
            _logger.LogInformation(
                "Library access updated for {User}: EnableAll={EnableAll}, Folders={Folders}",
                user.Username,
                planInfo.EnableAllFolders,
                string.Join(", ", planInfo.EnabledFolderIds ?? Array.Empty<string>()));
        }
        else
        {
            _logger.LogWarning("Failed to persist library access changes for {User}", user.Username);
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
