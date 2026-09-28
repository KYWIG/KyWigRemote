using KyWigRemote.Core.Data;
using KyWigRemote.Core.Directory;
using KyWigRemote.Core.Model;
using KyWigRemote.Server.Configuration;
using Serilog;

namespace KyWigRemote.Server;

/// <summary>
/// Service d'arrière-plan qui synchronise périodiquement les utilisateurs AD, si l'AD est activé
/// et la planification demandée (<c>Authentication:ActiveDirectory:Sync</c>). La synchronisation
/// manuelle reste possible via l'API, indépendamment de ce service.
/// </summary>
public sealed class AdSyncScheduler : BackgroundService
{
    private readonly AdSyncService _sync;
    private readonly KyWigRemoteOptions _options;
    private readonly IAuditRepository _audit;

    public AdSyncScheduler(AdSyncService sync, KyWigRemoteOptions options, IAuditRepository audit)
    {
        _sync = sync;
        _options = options;
        _audit = audit;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ActiveDirectoryOptions ad = _options.Authentication.ActiveDirectory;
        if (!_options.Authentication.IsEnabled("ActiveDirectory") || !ad.Sync.Enabled)
        {
            return; // planification désactivée
        }

        var interval = TimeSpan.FromHours(Math.Max(1, ad.Sync.IntervalHours));
        Log.Information("Synchronisation AD planifiée activée (toutes les {Hours} h).", interval.TotalHours);

        // Petit délai initial pour laisser le serveur finir de démarrer.
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                AdSyncOutcome outcome = _sync.Synchronize(ad.UserGroup, ad.ConnectionAdminGroup, ad.AdminGroup);
                Log.Information("Synchronisation AD : {Imported} import(s), {Updated} maj, {Deactivated} désactivé(s).",
                    outcome.Imported, outcome.Updated, outcome.Deactivated);
                Audit("OK", $"{outcome.Imported}/{outcome.Updated}/{outcome.Deactivated}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Warning(ex, "Synchronisation AD planifiée impossible.");
                Audit("ERROR", "annuaire injoignable");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void Audit(string result, string details)
    {
        try
        {
            _audit.Append(new AuditEvent
            {
                OccurredAt = DateTimeOffset.UtcNow,
                UserName = "SYSTEM",
                Action = "AD_SYNC",
                Result = result,
                Details = details,
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Écriture de l'audit de synchronisation AD impossible.");
        }
    }
}
