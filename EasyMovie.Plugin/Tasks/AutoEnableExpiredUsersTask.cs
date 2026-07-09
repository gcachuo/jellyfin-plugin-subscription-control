using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace EasyMovie.Plugin.Tasks;

public sealed class AutoEnableExpiredUsersTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly ILogger<AutoEnableExpiredUsersTask> _logger;

    public AutoEnableExpiredUsersTask(ILogger<AutoEnableExpiredUsersTask> logger)
    {
        _logger = logger;
    }

    public string Name => "EasyMovie: Reactivar usuarios expirados";

    public string Key => "EasyMovieAutoEnableExpiredUsers";

    public string Description => "Reactiva automáticamente usuarios deshabilitados por JFA-Go para que puedan entrar y ver el video de expiración.";

    public string Category => "EasyMovie Subscription";

    public bool IsHidden => false;

    public bool IsEnabled => true;

    public bool IsLogged => true;

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        progress.Report(10);

        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.ApiUrl))
        {
            _logger.LogWarning("API URL no configurada");
            progress.Report(100);
            return;
        }

        try
        {
            var baseUrl = config.ApiUrl.Split('?')[0];
            var autoEnableUrl = baseUrl.Replace("/subscription.php", "/auto-enable-expired.php");

            progress.Report(50);

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var response = await httpClient.GetAsync(autoEnableUrl, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Usuarios expirados reactivados: {Response}", content);
            }
            else
            {
                _logger.LogWarning("No se pudo ejecutar auto-enable. Status: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar auto-enable de usuarios expirados");
        }

        progress.Report(100);
        _logger.LogInformation("Tarea de reactivación de usuarios expirados completada.");
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return Array.Empty<TaskTriggerInfo>();
    }
}
