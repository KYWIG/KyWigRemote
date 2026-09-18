namespace KyWigRemote.Core.Model;

/// <summary>
/// Protocole d'accès distant supporté en v1. Nommé <c>RemoteProtocol</c> pour éviter
/// toute collision avec les types <c>Protocol</c> du framework.
/// </summary>
public enum RemoteProtocol
{
    /// <summary>Bureau à distance (port par défaut 3389).</summary>
    Rdp,

    /// <summary>Secure Shell (port par défaut 22).</summary>
    Ssh,
}

/// <summary>
/// Ports par défaut déduits du protocole (FR-05).
/// </summary>
public static class RemoteProtocolDefaults
{
    /// <summary>Retourne le port d'écoute par défaut du protocole indiqué.</summary>
    public static int DefaultPort(RemoteProtocol protocol) => protocol switch
    {
        RemoteProtocol.Rdp => 3389,
        RemoteProtocol.Ssh => 22,
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Protocole inconnu."),
    };
}
