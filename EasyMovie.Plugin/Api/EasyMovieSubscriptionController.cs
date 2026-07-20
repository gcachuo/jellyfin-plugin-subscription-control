using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyMovie.Plugin.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Api;

[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("[controller]")]
public sealed class EasyMovieSubscriptionController : ControllerBase
{
    private readonly SubscriptionClient _subscriptionClient;
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly UserPolicyService _policyService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<EasyMovieSubscriptionController> _logger;

    public EasyMovieSubscriptionController(
        SubscriptionClient subscriptionClient,
        IUserManager userManager,
        ILibraryManager libraryManager,
        UserPolicyService policyService,
        ILoggerFactory loggerFactory)
    {
        _subscriptionClient = subscriptionClient;
        _userManager = userManager;
        _libraryManager = libraryManager;
        _policyService = policyService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _logger = loggerFactory.CreateLogger<EasyMovieSubscriptionController>();
    }

    [HttpGet("Clientes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClientes([FromQuery] string? ordenarPor = null, [FromQuery] bool includeInactive = false)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            _logger.LogWarning("Plugin configuration is not available while requesting clientes list");
            return NotFound();
        }

        var clientesUrl = BuildClientesUrl(
            config.ApiUrl,
            ordenarPor,
            includeInactive,
            config.ExpiringThresholdDays,
            config.TrialMaxDurationDays);
        if (string.IsNullOrWhiteSpace(clientesUrl))
        {
            return BadRequest(new { error = "API URL not configured" });
        }

        try
        {
            using var response = await _httpClient.GetAsync(clientesUrl).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Clientes endpoint returned {StatusCode}: {Body}", response.StatusCode, payload);
                return StatusCode((int)response.StatusCode, payload);
            }

            return Content(payload, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch clientes list from {Url}", clientesUrl);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatuses([FromQuery] bool includeFailsafe = false)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            _logger.LogWarning("Plugin configuration is not available while requesting status list");
            return NotFound();
        }

        var results = new List<UserStatusDto>();
        foreach (var user in UserManagerCompat.GetUsers(_userManager))
        {
            var status = await _subscriptionClient.GetStatusAsync(user, config, CancellationToken.None).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(status.Status))
            {
                continue;
            }

            if (!includeFailsafe && status.FailSafe)
            {
                continue;
            }

            results.Add(new UserStatusDto
            {
                UserId = user.Id.ToString("N"),
                Username = user.Username,
                Email = status.Email,
                Status = status.Status,
                ExpirationDate = status.ExpirationDate,
                IsTrial = status.IsTrial,
                Cached = status.Cached
            });
        }

        var ordered = results.OrderBy(r => r.Username).ToList();
        return Ok(ordered);
    }

    [HttpGet("Libraries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetLibraries()
    {
        var libraries = _libraryManager.GetVirtualFolders()
            .Select(folder => new LibraryDto
            {
                Id = folder.ItemId,
                Name = folder.Name,
                CollectionType = folder.CollectionType?.ToString()
            })
            .OrderBy(lib => lib.Name)
            .ToList();

        return Ok(libraries);
    }

    [HttpPost("TestLibraryAccess")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestLibraryAccess(
        [FromQuery] string username,
        [FromQuery] bool enableAll,
        [FromQuery] string? folderIds = null,
        [FromQuery] bool? allowLiveTv = null)
    {
        var user = UserManagerCompat.GetUsers(_userManager).FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            return NotFound(new { error = $"User '{username}' not found" });
        }

        try
        {
            var folderIdArray = string.IsNullOrWhiteSpace(folderIds)
                ? Array.Empty<string>()
                : folderIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Get current policy
            var policy = await _policyService.GetUserPolicyAsync(user).ConfigureAwait(false);
            if (policy is null)
            {
                return BadRequest(new { error = "Could not retrieve user policy" });
            }

            // Get current values
            var currentLiveTv = policy.EnableLiveTvAccess;

            // Update library access
            if (!_policyService.TrySetLibraryAccess(policy, enableAll, folderIdArray, out var currentEnableAll, out var currentFolders))
            {
                return BadRequest(new { error = "Failed to set library access properties" });
            }

            // Update Live TV access if specified
            if (allowLiveTv.HasValue)
            {
                _policyService.SetLiveTvAccess(policy, allowLiveTv.Value);
            }

            // Persist changes
            var updated = await _policyService.UpdateUserPolicyAsync(user, policy).ConfigureAwait(false);
            if (!updated)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to persist policy changes" });
            }

            return Ok(new
            {
                username = user.Username,
                userId = user.Id,
                previous = new
                {
                    enableAllFolders = currentEnableAll,
                    enabledFolders = currentFolders,
                    liveTvAccess = currentLiveTv
                },
                updated = new
                {
                    enableAllFolders = enableAll,
                    enabledFolders = folderIdArray,
                    liveTvAccess = allowLiveTv ?? currentLiveTv
                },
                message = "Access updated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update library access for {User}", username);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = ex.Message,
                stackTrace = ex.StackTrace,
                innerException = ex.InnerException?.Message
            });
        }
    }

    public sealed class LibraryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? CollectionType { get; set; }
    }

    public sealed class UserStatusDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ExpirationDate { get; set; }
        public bool IsTrial { get; set; }
        public bool Cached { get; set; }
    }

    private static string? BuildClientesUrl(
        string apiUrl,
        string? ordenarPor,
        bool includeInactive,
        int expiringDays,
        int trialMaxDays)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        var builder = new UriBuilder(baseUri);
        var path = builder.Path ?? string.Empty;
        if (path.EndsWith("/clientes.php", StringComparison.OrdinalIgnoreCase))
        {
            builder.Path = path;
        }
        else if (path.EndsWith("clientes.php", StringComparison.OrdinalIgnoreCase))
        {
            builder.Path = path;
        }
        else if (path.EndsWith("/subscription.php", StringComparison.OrdinalIgnoreCase))
        {
            builder.Path = path[..^"/subscription.php".Length] + "/clientes.php";
        }
        else if (path.EndsWith("subscription.php", StringComparison.OrdinalIgnoreCase))
        {
            builder.Path = path[..^"subscription.php".Length] + "clientes.php";
        }
        else
        {
            builder.Path = path.TrimEnd('/') + "/clientes.php";
        }

        var queryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(builder.Query))
        {
            queryParts.Add(builder.Query.TrimStart('?'));
        }

        var ordenarValue = string.IsNullOrWhiteSpace(ordenarPor) ? "fechaActivo" : ordenarPor;
        queryParts.Add($"ordenarPor={Uri.EscapeDataString(ordenarValue)}");
        queryParts.Add($"includeInactive={(includeInactive ? "true" : "false")}");
        queryParts.Add($"expiringDays={expiringDays}");
        queryParts.Add($"trialMaxDays={trialMaxDays}");
        builder.Query = string.Join("&", queryParts.Where(part => !string.IsNullOrWhiteSpace(part)));

        return builder.Uri.ToString();
    }
}
