using KyWigRemote.Core.Model;

namespace KyWigRemote.Admin;

/// <summary>Correspondance entre <see cref="CredentialMode"/> et les libellés affichés dans les combos.</summary>
internal static class CredentialModeChoices
{
    private static readonly CredentialMode[] Order =
    {
        CredentialMode.Personal, CredentialMode.Enforced, CredentialMode.Prompt, CredentialMode.Inherited,
    };

    /// <summary>Libellés dans l'ordre des combos.</summary>
    public static readonly object[] Labels = { "Personnel", "Imposé", "À la demande", "Hérité" };

    public static int IndexOf(CredentialMode mode) => Array.IndexOf(Order, mode);

    public static CredentialMode FromIndex(int index) =>
        index >= 0 && index < Order.Length ? Order[index] : CredentialMode.Inherited;
}

/// <summary>Correspondance entre <see cref="UserRole"/> et les libellés affichés (combos, listes).</summary>
internal static class UserRoleChoices
{
    private static readonly UserRole[] Order =
    {
        UserRole.User, UserRole.ConnectionAdmin, UserRole.GlobalAdmin,
    };

    /// <summary>Libellés dans l'ordre des combos.</summary>
    public static readonly object[] Labels =
    {
        "Utilisateur", "Administrateur des connexions", "Administrateur global",
    };

    public static int IndexOf(UserRole role) => Array.IndexOf(Order, role);

    public static UserRole FromIndex(int index) =>
        index >= 0 && index < Order.Length ? Order[index] : UserRole.User;

    /// <summary>Libellé lisible d'un profil.</summary>
    public static string Label(UserRole role)
    {
        int i = IndexOf(role);
        return i >= 0 ? (string)Labels[i] : role.ToString();
    }
}

/// <summary>Correspondance entre <see cref="RemoteProtocol"/> et les libellés affichés.</summary>
internal static class ProtocolChoices
{
    private static readonly RemoteProtocol[] Order = { RemoteProtocol.Rdp, RemoteProtocol.Ssh };

    public static readonly object[] Labels = { "RDP", "SSH" };

    public static int IndexOf(RemoteProtocol protocol) => Array.IndexOf(Order, protocol);

    public static RemoteProtocol FromIndex(int index) =>
        index >= 0 && index < Order.Length ? Order[index] : RemoteProtocol.Rdp;
}
