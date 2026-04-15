using System.Runtime.InteropServices;

namespace WindroseServerManager.Helpers;

internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [DllImport("kernel32.dll")]
    internal static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? handler, bool add);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    internal static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    internal static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string? pszSubIdList);

    [DllImport("user32.dll")]
    internal static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    internal const int WM_VSCROLL  = 0x0115;
    internal const int SB_BOTTOM   = 7;

    internal delegate bool ConsoleCtrlDelegate(uint ctrlType);

    internal const uint CTRL_C_EVENT = 0;
    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
}
