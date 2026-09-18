using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Remote;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin.Forms;

/// <summary>
/// Visionneuse du journal d'audit (E7.6 / E9) : consultation filtrable des événements
/// (connexions, ouvertures de session, révélations, modifications). Aucun secret n'y figure.
/// </summary>
internal sealed class AuditViewerForm : Form
{
    private readonly ServerClient _server;
    private readonly TextBox _userFilter;
    private readonly ComboBox _resultFilter;
    private readonly ListView _list;
    private readonly ToolStripStatusLabel _statusLabel;

    public AuditViewerForm(ServerClient server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));

        Text = "KyWigRemote — Journal d'audit";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(900, 560);
        BackColor = DarkPalette.Background;
        ForeColor = DarkPalette.Text;
        Font = new Font("Consolas", 9f);

        var filterBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = DarkPalette.PanelBackground };
        filterBar.Controls.Add(new Label { Text = "Utilisateur :", ForeColor = DarkPalette.Text, AutoSize = true, Location = new Point(10, 12) });
        _userFilter = new TextBox { Location = new Point(90, 9), Width = 140, BorderStyle = BorderStyle.FixedSingle, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text };
        filterBar.Controls.Add(_userFilter);
        filterBar.Controls.Add(new Label { Text = "Résultat :", ForeColor = DarkPalette.Text, AutoSize = true, Location = new Point(246, 12) });
        _resultFilter = new ComboBox { Location = new Point(316, 9), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text };
        _resultFilter.Items.AddRange(new object[] { "(tous)", "OK", "DENIED", "ERROR" });
        _resultFilter.SelectedIndex = 0;
        filterBar.Controls.Add(_resultFilter);
        var apply = new Button { Text = "Filtrer", Location = new Point(440, 8), Width = 90, FlatStyle = FlatStyle.Flat, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text };
        apply.FlatAppearance.BorderColor = DarkPalette.Border;
        apply.Click += async (_, _) => await LoadAsync();
        filterBar.Controls.Add(apply);

        _list = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, BorderStyle = BorderStyle.None,
            BackColor = DarkPalette.PanelBackground, ForeColor = DarkPalette.Text, HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _list.Columns.Add("Date (UTC)", 160);
        _list.Columns.Add("Utilisateur", 120);
        _list.Columns.Add("Action", 140);
        _list.Columns.Add("Cible", 130);
        _list.Columns.Add("Mode", 90);
        _list.Columns.Add("Résultat", 80);
        _list.Columns.Add("Détail", 150);

        var status = new StatusStrip { BackColor = DarkPalette.PanelBackground, ForeColor = DarkPalette.TextMuted, SizingGrip = false };
        _statusLabel = new ToolStripStatusLabel("Chargement…") { ForeColor = DarkPalette.TextMuted };
        status.Items.Add(_statusLabel);

        Controls.Add(_list);
        Controls.Add(filterBar);
        Controls.Add(status);

        Load += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        string? user = string.IsNullOrWhiteSpace(_userFilter.Text) ? null : _userFilter.Text.Trim();
        string? result = _resultFilter.SelectedIndex <= 0 ? null : (string)_resultFilter.SelectedItem!;

        try
        {
            IReadOnlyList<AuditEventSummary> events = await _server.ListAuditAsync(user, result);
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (AuditEventSummary e in events)
            {
                var item = new ListViewItem(e.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(e.UserName ?? string.Empty);
                item.SubItems.Add(e.Action);
                item.SubItems.Add(e.TargetId is null ? (e.TargetType ?? string.Empty) : $"{e.TargetType} #{e.TargetId}");
                item.SubItems.Add(e.CredentialMode ?? string.Empty);
                item.SubItems.Add(e.Result ?? string.Empty);
                item.SubItems.Add(e.Details ?? string.Empty);
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            _statusLabel.Text = $"{events.Count} événement(s) — du plus récent au plus ancien";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(this, "Impossible de charger le journal d'audit.",
                "KyWigRemote — Journal d'audit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
