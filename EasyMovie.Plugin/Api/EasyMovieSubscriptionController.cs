using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly HttpClient _httpClient;
    private readonly ILogger<EasyMovieSubscriptionController> _logger;

    public EasyMovieSubscriptionController(
        SubscriptionClient subscriptionClient,
        IUserManager userManager,
        ILoggerFactory loggerFactory)
    {
        _subscriptionClient = subscriptionClient;
        _userManager = userManager;
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
        foreach (var user in _userManager.Users)
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
