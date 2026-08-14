using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Tasks;

public sealed class FixEndDateTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<FixEndDateTask> _logger;

    public FixEndDateTask(ILibraryManager libraryManager, ILogger<FixEndDateTask> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public string Name => "EasyMovie: Fix EndDate en items sin fecha";

    public string Key => "EasyMovieFixEndDate";

    public string Description => "Setea EndDate=DateCreated para items con EndDate=NULL (grabaciones de TV, películas locales sin metadata). Necesario porque el plugin Gelato filtra items con EndDate=NULL cuando FilterUnreleased está activo.";

    public string Category => "EasyMovie Subscription";

    public bool IsHidden => false;

    public bool IsEnabled => true;

    public bool IsLogged => true;

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting FixEndDate task");
        progress.Report(0);

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Video, BaseItemKind.Movie }
        };

        var items = _libraryManager.GetItemList(query, allowExternalContent: false);
        var needsFix = items.Where(i => i.EndDate is null).ToList();

        _logger.LogInformation("Found {Total} items, {NeedsFix} with EndDate=NULL", items.Count, needsFix.Count);

        if (needsFix.Count == 0)
        {
            progress.Report(100);
            _logger.LogInformation("FixEndDate completed. No items needed fixing.");
            return;
        }

        var fixedCount = 0;
        foreach (var item in needsFix)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            item.EndDate = item.DateCreated;
            fixedCount++;
        }

        if (fixedCount > 0)
        {
            await _libraryManager
                .UpdateItemsAsync(needsFix, null!, ItemUpdateType.MetadataEdit, cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogInformation("FixEndDate completed. Fixed {Count} item(s)", fixedCount);
        progress.Report(100);
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Run every 6 hours
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(6).Ticks
        };
    }
}
