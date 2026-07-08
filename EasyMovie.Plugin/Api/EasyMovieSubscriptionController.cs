using System.Collections.Generic;
using System.Linq;
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
    private readonly ILogger<EasyMovieSubscriptionController> _logger;

    public EasyMovieSubscriptionController(
        SubscriptionClient subscriptionClient,
        IUserManager userManager,
        ILoggerFactory loggerFactory)
    {
        _subscriptionClient = subscriptionClient;
        _userManager = userManager;
        _logger = loggerFactory.CreateLogger<EasyMovieSubscriptionController>();
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
}
