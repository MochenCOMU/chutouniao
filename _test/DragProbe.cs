// DragProbe —— 测试窗口。记录 LButtonDown / 带按键的 MouseMove，供 PsCtrlGuard 验证用。
// 启动后会强制把自己弄成前台窗口（AttachThreadInput 技巧），并把窗口矩形写进日志，
// 这样测试脚本不用猜坐标。
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal static class DragProbe
{
    static readonly string LogPath = Path.Combine(Path.GetTempPath(), "dragprobe.log");
    static int _downs, _ups, _drags, _keys;

    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    static void Log(string s)
    {
        try { File.AppendAllText(LogPath, s + Environment.NewLine); } catch { }
    }

    static string FgTitle()
    {
        var sb = new StringBuilder(300);
        GetWindowText(GetForegroundWindow(), sb, 300);
        return sb.ToString();
    }

    static void ForceForeground(IntPtr h)
    {
        IntPtr fg = GetForegroundWindow();
        uint fgPid;
        uint fgThread = GetWindowThreadProcessId(fg, out fgPid);
        uint myThread = GetCurrentThreadId();
        bool attached = AttachThreadInput(myThread, fgThread, true);
        SetForegroundWindow(h);
        BringWindowToTop(h);
        if (attached) AttachThreadInput(myThread, fgThread, false);
    }

    [STAThread]
    static void Main()
    {
        File.WriteAllText(LogPath, "");
        var form = new Form
        {
            Text = "DragProbe",
            ClientSize = new Size(900, 640),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(120, 120),
            BackColor = Color.FromArgb(30, 30, 36),
            TopMost = true
        };

        form.Shown += (s, e) =>
        {
            ForceForeground(form.Handle);
            RECT r;
            GetWindowRect(form.Handle, out r);
            Log("RECT " + r.Left + " " + r.Top + " " + r.Right + " " + r.Bottom);
            Log("FG " + FgTitle());
        };

        var t = new Timer { Interval = 700 };
        t.Tick += (s, e) =>
        {
            if (FgTitle() != "DragProbe") ForceForeground(form.Handle);
        };
        form.Shown += (s, e) => t.Start();

        form.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left) { _downs++; Log("DOWN " + _downs + " @" + e.X + "," + e.Y); }
            else Log("DOWN-" + e.Button + " @" + e.X + "," + e.Y);
        };
        form.KeyPreview = true;
        form.KeyDown += (s, e) =>
        {
            _keys++;
            bool mod = (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey ||
                        e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin);
            Log((mod ? "MODKEY " : "KEY ") + e.KeyData + " active=" + (Form.ActiveForm != null) + " focus=" + form.ContainsFocus);
        };
        form.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Left) { _ups++; Log("UP " + _ups); }
        };
        form.MouseMove += (s, e) =>
        {
            if (e.Button == MouseButtons.Left) { _drags++; Log("DRAG @" + e.X + "," + e.Y); }
        };
        form.FormClosed += (s, e) => Log("SUMMARY downs=" + _downs + " ups=" + _ups + " drags=" + _drags + " keys=" + _keys);

        Application.Run(form);
    }
}
