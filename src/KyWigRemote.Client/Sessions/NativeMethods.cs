using System.Runtime.InteropServices;

namespace KyWigRemote.Client.Sessions;

/// <summary>
/// Appels Win32 nécessaires au reparentage d'une fenêtre externe (PuTTY) dans un panneau
/// de l'application (ADR-004). Isolés ici pour garder le reste du code lisible.
/// </summary>
internal static class NativeMethods
{
    public const int GWL_STYLE = -16;
    public const long WS_CHILD = 0x40000000L;
    public const long WS_CAPTION = 0x00C00000L;
    public const long WS_THICKFRAME = 0x00040000L;
    public const long WS_BORDER = 0x00800000L;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>Retire la bordure et la barre de titre d'une fenêtre et la marque comme fille.</summary>
    public static void MakeChildWindow(IntPtr hWnd)
    {
        long style = GetWindowLong(hWnd, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_THICKFRAME | WS_BORDER);
        style |= WS_CHILD;
        SetWindowLong(hWnd, GWL_STYLE, (int)style);
    }
}
