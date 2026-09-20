namespace KyWigRemote.Client.Sessions;

/// <summary>
/// Identifiant résolu pour ouvrir une session (utilisateur + éventuel secret).
/// Le secret reste une <c>string</c> car il transite déjà ainsi depuis le serveur (JSON) et
/// l'API cible (ClearTextPassword du contrôle RDP) exige une chaîne : c'est le « dernier appel »
/// toléré par la règle 3 de CLAUDE.md. Il n'est jamais journalisé.
/// </summary>
internal sealed record SessionCredential(string Username, string? Domain, string? Password);
