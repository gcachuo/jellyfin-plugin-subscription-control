using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Services;

public sealed class UserPolicyService
{
    private readonly IUserManager _userManager;
    private readonly ILogger<UserPolicyService> _logger;

    public UserPolicyService(IUserManager userManager, ILogger<UserPolicyService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public Task<UserPolicy?> GetUserPolicyAsync(User user)
    {
        try
        {
            // Build UserPolicy from User entity (Jellyfin stores policy data in User.Permissions and User.Preferences)
            var policy = new UserPolicy
            {
                EnableAllFolders = user.HasPermission(PermissionKind.EnableAllFolders),
                EnabledFolders = user.GetPreferenceValues<Guid>(PreferenceKind.EnabledFolders),
                // Include other common policy properties
                IsAdministrator = user.HasPermission(PermissionKind.IsAdministrator),
                IsHidden = user.HasPermission(PermissionKind.IsHidden),
                IsDisabled = user.HasPermission(PermissionKind.IsDisabled),
                EnableLiveTvAccess = user.HasPermission(PermissionKind.EnableLiveTvAccess),
                EnableMediaPlayback = user.HasPermission(PermissionKind.EnableMediaPlayback),
                EnableRemoteAccess = user.HasPermission(PermissionKind.EnableRemoteAccess),
                MaxParentalRating = user.MaxParentalRatingScore,
                MaxParentalSubRating = user.MaxParentalRatingSubScore,
                AuthenticationProviderId = user.AuthenticationProviderId,
                PasswordResetProviderId = user.PasswordResetProviderId,
                InvalidLoginAttemptCount = user.InvalidLoginAttemptCount,
                LoginAttemptsBeforeLockout = user.LoginAttemptsBeforeLockout ?? -1,
                MaxActiveSessions = user.MaxActiveSessions,
                SyncPlayAccess = user.SyncPlayAccess
            };

            _logger.LogDebug("Successfully built UserPolicy for user {UserId}", user.Id);
            return Task.FromResult<UserPolicy?>(policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build UserPolicy for user {UserId}", user.Id);
            return Task.FromResult<UserPolicy?>(null);
        }
    }

    public bool TrySetLibraryAccess(UserPolicy policy, bool enableAllFolders, string[] enabledFolderIds, out bool currentEnableAll, out string[]? currentFolders)
    {
        currentEnableAll = false;
        currentFolders = null;

        // Get current values
        currentEnableAll = policy.EnableAllFolders;
        currentFolders = policy.EnabledFolders?.Select(g => g.ToString()).ToArray();

        // Convert string IDs to Guids with logging for invalid IDs
        var folderGuids = new System.Collections.Generic.List<Guid>();
        foreach (var id in enabledFolderIds)
        {
            if (Guid.TryParse(id, out var guid))
            {
                folderGuids.Add(guid);
            }
            else
            {
                _logger.LogWarning("Invalid folder ID format: {FolderId}", id);
            }
        }

        // Set new values
        policy.EnableAllFolders = enableAllFolders;
        policy.EnabledFolders = folderGuids.ToArray();

        return true;
    }

    public async Task<bool> UpdateUserPolicyAsync(User user, UserPolicy policy)
    {
        try
        {
            await _userManager.UpdatePolicyAsync(user.Id, policy).ConfigureAwait(false);
            _logger.LogDebug("Successfully updated policy for user {UserId}", user.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update policy for user {UserId}", user.Id);
            return false;
        }
    }
}
