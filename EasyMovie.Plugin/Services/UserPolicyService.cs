using System;
using System.Linq;
using System.Reflection;
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

    public Task<object?> GetUserPolicyAsync(User user)
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
            return Task.FromResult<object?>(policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build UserPolicy for user {UserId}", user.Id);
            return Task.FromResult<object?>(null);
        }
    }

    public bool TrySetLibraryAccess(object policy, bool enableAllFolders, string[] enabledFolderIds, out bool currentEnableAll, out string[]? currentFolders)
    {
        currentEnableAll = false;
        currentFolders = null;

        if (policy is not UserPolicy userPolicy)
        {
            _logger.LogWarning("Policy is not a UserPolicy instance");
            return false;
        }

        // Get current values
        currentEnableAll = userPolicy.EnableAllFolders;
        currentFolders = userPolicy.EnabledFolders?.Select(g => g.ToString()).ToArray();

        // Convert string IDs to Guids
        var folderGuids = enabledFolderIds
            .Select(id => Guid.TryParse(id, out var guid) ? guid : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToArray();

        // Set new values
        userPolicy.EnableAllFolders = enableAllFolders;
        userPolicy.EnabledFolders = folderGuids;

        return true;
    }

    public async Task<bool> UpdateUserPolicyAsync(User user, object policy)
    {
        if (policy is not UserPolicy userPolicy)
        {
            _logger.LogWarning("Policy is not a UserPolicy instance");
            return false;
        }

        try
        {
            await _userManager.UpdatePolicyAsync(user.Id, userPolicy).ConfigureAwait(false);
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
