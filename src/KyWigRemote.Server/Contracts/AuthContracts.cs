namespace KyWigRemote.Server.Contracts;

/// <summary>Demande de connexion par compte local.</summary>
public sealed record LoginRequest(string Username, string Password);

/// <summary>Demande d'amorçage du premier compte administrateur local (première configuration).</summary>
public sealed record BootstrapAdminRequest(string Username, string Password, string? DisplayName);
