using System.Diagnostics;
using System.Text.RegularExpressions;

namespace KyWigRemote.ServerManager;

/// <summary>État d'un service Windows tel que rapporté par <c>sc query</c>.</summary>
internal enum ServiceState
{
    NotInstalled,
    Stopped,
    Running,
    Pending,
    Unknown,
}

/// <summary>Résultat d'une opération sur le service (pour affichage).</summary>
internal readonly record struct ServiceActionResult(bool Success, string Message);

/// <summary>
/// Pilote le service Windows du serveur KyWigRemote via <c>sc.exe</c> (aucune dépendance
/// supplémentaire). L'installation, la suppression, le démarrage et l'arrêt exigent des droits
/// administrateur : l'appelant vérifie l'élévation avant d'exposer ces actions.
/// </summary>
internal sealed class WindowsServiceManager
{
    public const string ServiceName = "KyWigRemoteServer";
    public const string DisplayName = "KyWigRemote Server";

    /// <summary>Interroge l'état du service (indépendant de la langue : on lit le code numérique).</summary>
    public ServiceState QueryState()
    {
        (int exitCode, string output) = RunSc("query", ServiceName);
        if (exitCode != 0 || output.Contains("1060"))
        {
            return ServiceState.NotInstalled; // 1060 = service inexistant
        }

        Match m = Regex.Match(output, @"STATE\s*:\s*(\d+)");
        if (!m.Success)
        {
            return ServiceState.Unknown;
        }
        return m.Groups[1].Value switch
        {
            "1" => ServiceState.Stopped,
            "4" => ServiceState.Running,
            "2" or "3" => ServiceState.Pending,
            _ => ServiceState.Unknown,
        };
    }

    /// <summary>Crée le service en démarrage automatique, pointant sur l'exécutable du serveur.</summary>
    public ServiceActionResult Install(string serverExePath)
    {
        (int code, string output) = RunSc(
            "create", ServiceName,
            "binPath=", serverExePath,
            "start=", "auto",
            "DisplayName=", DisplayName);
        if (code != 0)
        {
            return new ServiceActionResult(false, DescribeFailure(code, output));
        }
        RunSc("description", ServiceName, "Serveur de connexions distantes KyWigRemote.");
        return new ServiceActionResult(true, "Service installé (démarrage automatique).");
    }

    /// <summary>Arrête (si besoin) puis supprime le service.</summary>
    public ServiceActionResult Uninstall()
    {
        RunSc("stop", ServiceName);
        WaitForState(ServiceState.Stopped, TimeSpan.FromSeconds(10));
        (int code, string output) = RunSc("delete", ServiceName);
        return code == 0
            ? new ServiceActionResult(true, "Service supprimé.")
            : new ServiceActionResult(false, DescribeFailure(code, output));
    }

    public ServiceActionResult Start()
    {
        (int code, string output) = RunSc("start", ServiceName);
        return code == 0
            ? new ServiceActionResult(true, "Service démarré.")
            : new ServiceActionResult(false, DescribeFailure(code, output));
    }

    public ServiceActionResult Stop()
    {
        (int code, string output) = RunSc("stop", ServiceName);
        return code == 0
            ? new ServiceActionResult(true, "Service arrêté.")
            : new ServiceActionResult(false, DescribeFailure(code, output));
    }

    /// <summary>Arrête, attend l'arrêt effectif, puis redémarre.</summary>
    public ServiceActionResult Restart()
    {
        RunSc("stop", ServiceName);
        if (!WaitForState(ServiceState.Stopped, TimeSpan.FromSeconds(15)))
        {
            return new ServiceActionResult(false, "Le service ne s'est pas arrêté à temps.");
        }
        return Start();
    }

    private bool WaitForState(ServiceState target, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (QueryState() == target)
            {
                return true;
            }
            Thread.Sleep(400);
        }
        return QueryState() == target;
    }

    private static string DescribeFailure(int code, string output)
    {
        // 5 = accès refusé (droits admin manquants) ; sinon on remonte le code et la sortie brute.
        string reason = code == 5 ? "accès refusé (droits administrateur requis)" : $"code {code}";
        string detail = output.Trim();
        return string.IsNullOrEmpty(detail) ? $"Échec : {reason}." : $"Échec ({reason}) : {detail}";
    }

    private static (int ExitCode, string Output) RunSc(params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "sc.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string a in arguments)
        {
            info.ArgumentList.Add(a);
        }

        try
        {
            using Process? process = Process.Start(info);
            if (process is null)
            {
                return (-1, "Impossible de lancer sc.exe.");
            }
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(15000);
            return (process.ExitCode, stdout + stderr);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
