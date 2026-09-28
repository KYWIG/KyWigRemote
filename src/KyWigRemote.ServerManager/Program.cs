using System.Windows.Forms;
using KyWigRemote.ServerManager.Forms;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.ServerManager;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        ThemeManager.Load();
        Application.Run(new ManagerForm());
    }
}
