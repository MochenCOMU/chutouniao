param([string]$OutDir = "C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao\_test")

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class Ui {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();

    public static string Title(IntPtr h) {
        var sb = new StringBuilder(300); GetWindowText(h, sb, 300); return sb.ToString();
    }

    public static IntPtr Find(int pid, string prefix) {
        IntPtr result = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == (uint)pid) {
                string t = Title(h);
                if (t.StartsWith(prefix)) { result = h; return false; }
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static void Focus(IntPtr h) {
        IntPtr fg = GetForegroundWindow();
        uint pid; uint ft = GetWindowThreadProcessId(fg, out pid);
        uint mt = GetCurrentThreadId();
        bool at = AttachThreadInput(mt, ft, true);
        SetForegroundWindow(h); BringWindowToTop(h);
        if (at) AttachThreadInput(mt, ft, false);
        System.Threading.Thread.Sleep(350);
    }

    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(150);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(80);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(650);
    }
}
"@

function Save-Shot($name) {
    $b = New-Object System.Drawing.Bitmap([System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width, [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height)
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.CopyFromScreen(0, 0, 0, 0, $b.Size)
    $b.Save((Join-Path $OutDir $name))
    $g.Dispose(); $b.Dispose()
    Write-Output "saved $name"
}

$proc = Get-Process SongBang -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "app not running"; exit 1 }

$h = [Ui]::Find($proc.Id, "SongBang")
Write-Output ("window hwnd={0} title='{1}'" -f $h, [Ui]::Title($h))
if ($h -eq [IntPtr]::Zero) { Write-Output "settings window not open"; exit 1 }

[Ui]::Focus($h)
$fgTitle = [Ui]::Title([Ui]::GetForegroundWindow())
Write-Output "foreground='$fgTitle'"
if ($fgTitle -notlike "SongBang*") { Write-Output "REFUSE: cannot bring it to front"; exit 1 }

$win = [System.Windows.Automation.AutomationElement]::FromHandle($h)
$wr = $win.Current.BoundingRectangle
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Tab)
$tab = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
if ($null -eq $tab) { Write-Output "no tab control in settings window"; exit 1 }
$r = $tab.Current.BoundingRectangle
Write-Output ("window {0},{1} {2}x{3}" -f $wr.X, $wr.Y, $wr.Width, $wr.Height)
Write-Output ("tab    {0},{1} {2}x{3}" -f $r.X, $r.Y, $r.Width, $r.Height)
if ($r.X -lt $wr.X -or $r.Y -lt $wr.Y -or ($r.X + $r.Width) -gt ($wr.X + $wr.Width)) {
    Write-Output "REFUSE: tab rect not inside settings window"; exit 1
}

$y = [int]($r.Y + 12)
[Ui]::Click([int]($r.X + 84), $y)
Save-Shot "tab-targets.png"
[Ui]::Click([int]($r.X + 128), $y)
Save-Shot "tab-general.png"
