using System.Windows.Forms;
using KyWigRemote.Shared.UI;
using KyWigRemote.Core.Model;
using WeifenLuo.WinFormsUI.Docking;

namespace KyWigRemote.Client.Forms;

/// <summary>
/// Panneau « Propriétés » : affiche en lecture seule les informations de l'élément
/// sélectionné dans l'arbre (ui-spec §1). L'édition est réservée à l'administration.
/// Aucun secret n'est jamais affiché ici.
/// </summary>
internal sealed class PropertiesPanel : DockContent
{
    private readonly ListView _list;

    public PropertiesPanel()
    {
        Text = "Propriétés";
        DockAreas = DockAreas.DockLeft | DockAreas.DockRight | DockAreas.Float;
        BackColor = DarkPalette.PanelBackground;

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BorderStyle = BorderStyle.None,
            BackColor = DarkPalette.PanelBackground,
            ForeColor = DarkPalette.Text,
            MultiSelect = false,
        };
        _list.Columns.Add("Propriété", 90);
        _list.Columns.Add("Valeur", 200);

        Controls.Add(_list);
    }

    /// <summary>Met à jour l'affichage selon l'élément sélectionné (dossier, connexion ou rien).</summary>
    public void ShowFor(object? item)
    {
        _list.BeginUpdate();
        _list.Items.Clear();

        switch (item)
        {
            case RemoteConnection c:
                AddRow("Nom", c.Name);
                AddRow("Hôte", c.Host);
                AddRow("Proto", $"{c.Protocol.ToString().ToUpperInvariant()}:{c.Port}");
                if (!string.IsNullOrWhiteSpace(c.Domain))
                {
                    AddRow("Domaine", c.Domain!);
                }
                AddRow("Ident.", DescribeMode(c.CredentialMode));
                if (!string.IsNullOrWhiteSpace(c.Description))
                {
                    AddRow("Descr.", c.Description!);
                }
                break;

            case ConnectionFolder f:
                AddRow("Dossier", f.Name);
                AddRow("Ident.", DescribeMode(f.CredentialMode));
                AddRow("Contenu", $"{f.SubFolders.Count} dossier(s), {f.Connections.Count} connexion(s)");
                break;
        }

        _list.EndUpdate();
    }

    private void AddRow(string property, string value)
    {
        var row = new ListViewItem(property);
        row.SubItems.Add(value);
        _list.Items.Add(row);
    }

    private static string DescribeMode(CredentialMode mode) => mode switch
    {
        CredentialMode.Personal => "Personnel",
        CredentialMode.Enforced => "Imposé",
        CredentialMode.Prompt => "À la demande",
        CredentialMode.Inherited => "Hérité",
        _ => mode.ToString(),
    };
}
