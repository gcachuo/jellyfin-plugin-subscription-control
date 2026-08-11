using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EasyMovie.Plugin.Configuration;
using EasyMovie.Plugin.Playback;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Api;

[ApiController]
[Route("EasyMoviePreroll")]
public sealed class EasyMoviePrerollController : ControllerBase
{
    private readonly SubscriptionClient _subscriptionClient;
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IPrerollConfigurationProvider _configurationProvider;
    private readonly ILogger<EasyMoviePrerollController> _logger;

    public EasyMoviePrerollController(
        SubscriptionClient subscriptionClient,
        IUserManager userManager,
        ILibraryManager libraryManager,
        IPrerollConfigurationProvider configurationProvider,
        ILogger<EasyMoviePrerollController> logger)
    {
        _subscriptionClient = subscriptionClient;
        _userManager = userManager;
        _libraryManager = libraryManager;
        _configurationProvider = configurationProvider;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("overlay.js")]
    public IActionResult GetOverlayScript()
    {
        using var stream = typeof(EasyMoviePrerollController).Assembly
            .GetManifestResourceStream("EasyMovie.Plugin.Web.overlay.js");
        if (stream is null)
        {
            return NotFound();
        }

        using var reader = new StreamReader(stream);
        return Content(reader.ReadToEnd(), "application/javascript");
    }

    [Authorize]
    [HttpGet("Decision")]
    public async Task<ActionResult<PrerollDecisionDto>> GetDecision(
        [FromQuery] Guid itemId,
        CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirst("Jellyfin-UserId")?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var user = _userManager.GetUserById(userId);
        var config = _configurationProvider.GetConfiguration();
        var item = _libraryManager.GetItemById(itemId);
        if (user is null || config is null || item is null)
        {
            _logger.LogInformation("Decision: none (user={User}, config={Config}, item={Item}) for {ItemId}", user?.Username, config is not null ? "ok" : "null", item?.Name, itemId);
            return Ok(PrerollDecisionDto.None);
        }

        if (IsPluginVideo(item.Path, config))
        {
            _logger.LogInformation("Decision: none (plugin video) for {ItemId}", itemId);
            return Ok(PrerollDecisionDto.None);
        }

        var status = await _subscriptionClient.GetStatusAsync(user, config, cancellationToken).ConfigureAwait(false);
        if (status.IsExpired)
        {
            _logger.LogInformation("Decision: none (expired) for {ItemId}, user={User}", itemId, user.Username);
            return Ok(PrerollDecisionDto.None);
        }

        var path = new PrerollVideoSelector().Select(status, config.Videos);
        var introItem = string.IsNullOrWhiteSpace(path)
            ? null
            : GetOrCreateLibraryItem(path);

        var strategy = introItem is null ? "none" : "overlay";
        _logger.LogInformation("Decision: {Strategy} for {ItemId}, user={User}, introItemId={IntroItemId}", strategy, itemId, user.Username, introItem?.Id);

        return Ok(introItem is null
            ? PrerollDecisionDto.None
            : new PrerollDecisionDto("overlay", introItem.Id));
    }

    private BaseItem? GetOrCreateLibraryItem(string path)
    {
        var existing = _libraryManager.FindByPath(path, isFolder: false);
        if (existing is not null)
        {
            return existing;
        }

        if (!System.IO.File.Exists(path))
        {
            return null;
        }

        try
        {
            var video = new Video
            {
                Id = Guid.NewGuid(),
                Path = path,
                Name = Path.GetFileNameWithoutExtension(path),
                ProviderIds = new Dictionary<string, string>
                {
                    { "easymovie.preroll", path }
                }
            };
            _libraryManager.CreateItem(video, null);
            _logger.LogInformation("Indexed overlay intro video in Jellyfin library: {Path}", path);
            return video;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index overlay intro video in Jellyfin library: {Path}", path);
            return null;
        }
    }

    private static bool IsPluginVideo(string? path, PluginConfiguration config)
    {
        return !string.IsNullOrWhiteSpace(path)
            && (string.Equals(path, config.Videos.Active, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, config.Videos.Expiring, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, config.Videos.Expired, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, config.Videos.Courtesy, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, config.Videos.Trial, StringComparison.OrdinalIgnoreCase));
    }

    public sealed record PrerollDecisionDto(string Strategy, Guid? IntroItemId)
    {
        public static PrerollDecisionDto None { get; } = new("none", null);
    }
}
