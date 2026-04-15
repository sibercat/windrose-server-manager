using System.Runtime.InteropServices;

namespace WindroseServerManager.Helpers;

public static class ThemeManager
{
    // ── Shared accent / state colors (theme-independent) ────────────────
    public static readonly Color Accent        = Color.FromArgb(0,   122, 204);
    public static readonly Color AccentHover   = Color.FromArgb(14,  99,  156);
    public static readonly Color StateRunning  = Color.FromArgb(78,  201, 176);
    public static readonly Color StateStopped  = Color.FromArgb(244, 67,  54);
    public static readonly Color StateStarting = Color.FromArgb(255, 152, 0);
    public static readonly Color StateCrashed  = Color.FromArgb(232, 17,  35);
    public static readonly Color StateInstalling = Color.FromArgb(156, 110, 201);

    public static bool IsDark => true;

    public static void Apply(Form form)
    {
        Application.SetColorMode(SystemColorMode.Dark);
        SetTitleBarTheme(form.Handle, dark: true);
        ApplyCustom(form, dark: true);
        form.Refresh();
    }

    public static void ReapplyConsoleThemeOverrides(Control root)
    {
        foreach (Control ctrl in AllControls(root))
        {
            if (ctrl is TextBox tb && tb.Tag?.ToString() == "console")
            {
                tb.BackColor = Color.FromArgb(12, 12, 12);
                tb.ForeColor = Color.FromArgb(204, 204, 204);
                tb.Refresh();
            }
        }
    }

    private static IEnumerable<Control> AllControls(Control root)
    {
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (Control cc in AllControls(c))
                yield return cc;
        }
    }

    public static void SetTitleBarTheme(IntPtr handle, bool dark)
    {
        try
        {
            int v = dark ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(handle,
                NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int));
        }
        catch { }
    }

    // ── Color constants ──────────────────────────────────────────────────
    private static Color DarkBg      => Color.FromArgb(30, 30, 30);
    private static Color DarkBgInput => Color.FromArgb(37, 37, 38);
    private static Color DarkFg      => Color.FromArgb(204, 204, 204);

    private static void ApplyCustom(Control ctrl, bool dark)
    {
        switch (ctrl)
        {
            case TextBox tb when tb.Tag?.ToString() == "console":
                tb.BackColor = Color.FromArgb(12, 12, 12);
                tb.ForeColor = Color.FromArgb(204, 204, 204);
                break;

            case Button btn when btn.Tag?.ToString() == "accent":
                btn.BackColor = Accent;
                btn.ForeColor = Color.White;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = AccentHover;
                btn.FlatAppearance.MouseOverBackColor = AccentHover;
                btn.UseVisualStyleBackColor = false;
                break;

            case Button btn when btn.Tag?.ToString() == "danger":
                btn.BackColor = dark ? Color.FromArgb(110, 20, 20) : Color.FromArgb(200, 48, 48);
                btn.ForeColor = Color.White;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = StateStopped;
                btn.FlatAppearance.MouseOverBackColor = dark
                    ? Color.FromArgb(140, 30, 30) : Color.FromArgb(220, 60, 60);
                btn.UseVisualStyleBackColor = false;
                break;

            case Button btn:
                btn.BackColor = dark ? Color.FromArgb(51, 51, 55) : SystemColors.Control;
                btn.ForeColor = dark ? DarkFg : SystemColors.ControlText;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = dark ? Color.FromArgb(70, 70, 75) : SystemColors.ControlDark;
                btn.UseVisualStyleBackColor = false;
                break;

            case DataGridView dgv:
                StyleDataGridView(dgv, dark);
                break;

            case ListView lv:
                StyleListView(lv, dark);
                break;

            case StatusStrip ss:
                ss.BackColor = Accent;
                ss.ForeColor = Color.White;
                ss.Renderer  = new ToolStripProfessionalRenderer(new StatusColorTable());
                foreach (ToolStripItem i in ss.Items)
                { i.BackColor = Accent; i.ForeColor = Color.White; }
                break;

            case MenuStrip ms:
                ms.Renderer = new ToolStripProfessionalRenderer(new MenuColorTable(dark));
                ApplyMenuItems(ms.Items, dark);
                break;

            case TextBox tb:
                tb.BackColor = dark ? DarkBgInput : SystemColors.Window;
                tb.ForeColor = dark ? DarkFg : SystemColors.WindowText;
                tb.BorderStyle = BorderStyle.FixedSingle;
                break;

            case RichTextBox rtb:
                rtb.BackColor = dark ? DarkBgInput : SystemColors.Window;
                rtb.ForeColor = dark ? DarkFg : SystemColors.WindowText;
                break;

            case NumericUpDown nud:
                nud.BackColor = dark ? DarkBgInput : SystemColors.Window;
                nud.ForeColor = dark ? DarkFg : SystemColors.WindowText;
                break;

            case ComboBox cmb:
                cmb.BackColor = dark ? DarkBgInput : SystemColors.Window;
                cmb.ForeColor = dark ? DarkFg : SystemColors.WindowText;
                break;

            case ListBox lb:
                lb.BackColor = dark ? DarkBgInput : SystemColors.Window;
                lb.ForeColor = dark ? DarkFg : SystemColors.WindowText;
                break;

            case CheckBox chk:
                chk.ForeColor = dark ? DarkFg : SystemColors.ControlText;
                chk.BackColor = Color.Transparent;
                break;

            case RadioButton rdo:
                rdo.ForeColor = dark ? DarkFg : SystemColors.ControlText;
                rdo.BackColor = Color.Transparent;
                break;

            case Label lbl when lbl is not LinkLabel:
                lbl.ForeColor = dark ? DarkFg : SystemColors.ControlText;
                if (lbl.BackColor != Color.Transparent)
                    lbl.BackColor = Color.Transparent;
                break;

            case GroupBox grp:
                grp.ForeColor = dark ? DarkFg : SystemColors.ControlText;
                grp.BackColor = dark ? DarkBg : SystemColors.Control;
                break;

            case TabControl tc:
                StyleTabControl(tc, dark);
                break;

            case TabPage tp:
                tp.BackColor = dark ? DarkBg : SystemColors.Control;
                tp.ForeColor = dark ? DarkFg : SystemColors.ControlText;
                break;

            case FlowLayoutPanel flp:
                flp.BackColor = dark ? DarkBg : SystemColors.Control;
                break;

            case Panel pnl:
                pnl.BackColor = dark ? DarkBg : SystemColors.Control;
                break;

            case SplitContainer sc:
                sc.BackColor = dark ? DarkBg : SystemColors.Control;
                sc.Panel1.BackColor = dark ? DarkBg : SystemColors.Control;
                sc.Panel2.BackColor = dark ? DarkBg : SystemColors.Control;
                break;
        }

        if (ctrl is Form frm)
            frm.BackColor = dark ? DarkBg : SystemColors.Control;

        foreach (Control child in ctrl.Controls)
            ApplyCustom(child, dark);
    }

    private static void StyleTabControl(TabControl tc, bool dark)
    {
        tc.BackColor = DarkBg;
        tc.DrawMode  = TabDrawMode.OwnerDrawFixed;
        tc.DrawItem -= OnDrawTabItem;
        tc.DrawItem += OnDrawTabItem;
        tc.Paint    -= OnPaintTabStrip;
        tc.Paint    += OnPaintTabStrip;
    }

    private static void OnPaintTabStrip(object? sender, PaintEventArgs e)
    {
        if (sender is not TabControl tc) return;
        bool dark = IsDark;
        Color stripBg = dark ? Color.FromArgb(22, 22, 22) : Color.FromArgb(210, 210, 210);
        if (tc.TabPages.Count > 0)
        {
            var stripRect = new Rectangle(0, 0, tc.Width, tc.GetTabRect(0).Bottom + 2);
            using var br = new SolidBrush(stripBg);
            e.Graphics.FillRectangle(br, stripRect);
        }
    }

    private static void OnDrawTabItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tc) return;
        bool dark    = IsDark;
        bool selected = e.Index == tc.SelectedIndex;

        Color bg = selected
            ? (dark ? Color.FromArgb(37, 37, 38)  : SystemColors.Control)
            : (dark ? Color.FromArgb(22, 22, 22)  : Color.FromArgb(210, 210, 210));
        Color fg = dark ? Color.FromArgb(204, 204, 204) : SystemColors.ControlText;

        using var bgBrush = new SolidBrush(bg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        if (selected)
        {
            using var accentPen = new Pen(Color.FromArgb(0, 122, 204), 2);
            e.Graphics.DrawLine(accentPen, e.Bounds.Left, e.Bounds.Top,
                                           e.Bounds.Right, e.Bounds.Top);
        }

        string text = tc.TabPages[e.Index].Text;
        TextRenderer.DrawText(e.Graphics, text, tc.Font, e.Bounds, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);
    }

    private static void StyleDataGridView(DataGridView dgv, bool dark)
    {
        dgv.EnableHeadersVisualStyles = false;
        dgv.BorderStyle = BorderStyle.None;

        if (dark)
        {
            dgv.BackgroundColor = Color.FromArgb(30, 30, 30);
            dgv.GridColor       = Color.FromArgb(55, 55, 55);
            dgv.DefaultCellStyle.BackColor          = Color.FromArgb(37, 37, 38);
            dgv.DefaultCellStyle.ForeColor          = Color.FromArgb(204, 204, 204);
            dgv.DefaultCellStyle.SelectionBackColor = Accent;
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(44, 44, 46);
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = Color.FromArgb(204, 204, 204);
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = Accent;
            dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.BackColor          = Color.FromArgb(50, 50, 52);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor          = Color.FromArgb(204, 204, 204);
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 50, 52);
            dgv.RowHeadersDefaultCellStyle.BackColor          = Color.FromArgb(45, 45, 48);
            dgv.RowHeadersDefaultCellStyle.ForeColor          = Color.FromArgb(204, 204, 204);
            dgv.RowHeadersDefaultCellStyle.SelectionBackColor = Accent;
        }
        else
        {
            dgv.BackgroundColor = SystemColors.Window;
            dgv.GridColor       = Color.FromArgb(210, 210, 210);
            dgv.DefaultCellStyle.BackColor          = SystemColors.Window;
            dgv.DefaultCellStyle.ForeColor          = SystemColors.WindowText;
            dgv.DefaultCellStyle.SelectionBackColor = Accent;
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 250);
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = SystemColors.WindowText;
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = Accent;
            dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.BackColor          = Color.FromArgb(230, 230, 232);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor          = SystemColors.WindowText;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 230, 232);
            dgv.RowHeadersDefaultCellStyle.BackColor          = Color.FromArgb(240, 240, 242);
            dgv.RowHeadersDefaultCellStyle.ForeColor          = SystemColors.WindowText;
            dgv.RowHeadersDefaultCellStyle.SelectionBackColor = Accent;
        }
    }

    private static void StyleListView(ListView lv, bool dark)
    {
        lv.BackColor = dark ? Color.FromArgb(30, 30, 30) : SystemColors.Window;
        lv.ForeColor = dark ? Color.FromArgb(204, 204, 204) : SystemColors.WindowText;

        if (lv.OwnerDraw) return;
        lv.OwnerDraw = true;

        lv.DrawColumnHeader += (_, e) =>
        {
            bool d = IsDark;
            Color hdrBg  = d ? Color.FromArgb(45, 45, 48)    : Color.FromArgb(230, 230, 232);
            Color hdrFg  = d ? Color.FromArgb(204, 204, 204) : SystemColors.WindowText;
            Color border = d ? Color.FromArgb(60, 60, 63)    : Color.FromArgb(200, 200, 202);
            e.Graphics.FillRectangle(new SolidBrush(hdrBg), e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header!.Text,
                new Font("Segoe UI", 9f),
                new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
                hdrFg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            using var pen = new Pen(border);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
        };

        lv.DrawItem += (_, e) => { };

        lv.DrawSubItem += (_, e) =>
        {
            bool d = IsDark;
            Color rowBg    = d ? Color.FromArgb(37, 37, 38)    : SystemColors.Window;
            Color altRowBg = d ? Color.FromArgb(44, 44, 46)    : Color.FromArgb(248, 248, 250);
            Color fg       = d ? Color.FromArgb(204, 204, 204) : SystemColors.WindowText;
            Color line     = d ? Color.FromArgb(55, 55, 55)    : Color.FromArgb(210, 210, 212);

            bool isAlt = e.ItemIndex % 2 == 1;
            Color bg   = e.Item!.Selected ? Accent : (isAlt ? altRowBg : rowBg);
            Color text = e.Item.Selected ? Color.White : fg;

            e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "",
                new Font("Segoe UI", 9f),
                new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height),
                text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            using var pen = new Pen(line);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        };
    }

    private static void ApplyMenuItems(ToolStripItemCollection items, bool dark)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = dark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(240, 240, 240);
            item.ForeColor = dark ? Color.FromArgb(204, 204, 204) : Color.FromArgb(30, 30, 30);
            if (item is ToolStripMenuItem mi)
                ApplyMenuItems(mi.DropDownItems, dark);
        }
    }
}

internal class MenuColorTable : ProfessionalColorTable
{
    private readonly bool _dark;
    public MenuColorTable(bool dark) { _dark = dark; UseSystemColors = false; }

    public override Color MenuItemSelected              => _dark ? Color.FromArgb(60,60,65) : Color.FromArgb(210,210,215);
    public override Color MenuItemSelectedGradientBegin => MenuItemSelected;
    public override Color MenuItemSelectedGradientEnd   => MenuItemSelected;
    public override Color MenuItemPressedGradientBegin  => Color.FromArgb(0,122,204);
    public override Color MenuItemPressedGradientEnd    => Color.FromArgb(0,122,204);
    public override Color MenuBorder                    => _dark ? Color.FromArgb(63,63,70) : Color.FromArgb(180,180,180);
    public override Color MenuStripGradientBegin        => _dark ? Color.FromArgb(45,45,48) : Color.FromArgb(235,235,235);
    public override Color MenuStripGradientEnd          => MenuStripGradientBegin;
    public override Color ToolStripDropDownBackground   => _dark ? Color.FromArgb(45,45,48) : Color.FromArgb(245,245,245);
    public override Color ImageMarginGradientBegin      => ToolStripDropDownBackground;
    public override Color ImageMarginGradientMiddle     => ToolStripDropDownBackground;
    public override Color ImageMarginGradientEnd        => ToolStripDropDownBackground;
    public override Color SeparatorDark                 => _dark ? Color.FromArgb(63,63,70) : Color.FromArgb(180,180,180);
    public override Color SeparatorLight                => SeparatorDark;
}

internal class StatusColorTable : ProfessionalColorTable
{
    public StatusColorTable() { UseSystemColors = false; }
    public override Color StatusStripGradientBegin => Color.FromArgb(0,122,204);
    public override Color StatusStripGradientEnd   => Color.FromArgb(0,122,204);
}
