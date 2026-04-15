namespace WindroseServerManager.Helpers;

public static class WinFormsExtensions
{
    /// <summary>Invoke on UI thread if required, otherwise call directly.</summary>
    public static void InvokeIfRequired(this Control control, Action action)
    {
        if (control.IsDisposed || control.Disposing) return;
        if (control.InvokeRequired)
        {
            try { control.Invoke(action); }
            catch (ObjectDisposedException) { }
        }
        else
        {
            action();
        }
    }

    /// <summary>Append a timestamped line to the console TextBox.</summary>
    public static void AppendConsoleLine(this TextBoxBase tb, string message,
        Color? color = null, bool autoScroll = true)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        tb.AppendText($"[{timestamp}] {message}{Environment.NewLine}");

        const int maxLines = 2000;
        if (tb.Lines.Length > maxLines)
        {
            tb.ReadOnly = false;
            var lines = tb.Lines;
            tb.Lines = lines[^maxLines..];
            tb.ReadOnly = true;
        }

        if (autoScroll && tb.IsHandleCreated)
        {
            NativeMethods.SendMessage(tb.Handle, NativeMethods.WM_VSCROLL, NativeMethods.SB_BOTTOM, 0);
        }
    }
}
