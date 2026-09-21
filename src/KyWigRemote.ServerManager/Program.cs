using System.Windows.Forms;
using KyWigRemote.ServerManager.Forms;

namespace KyWigRemote.ServerManager;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new ManagerForm());
    }
}
