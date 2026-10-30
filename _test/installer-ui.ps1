param(
    [string]$Root = "C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao",
    [string]$Shot = "C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao\docs\installer-done.png"
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @"
using System; using System.Text; using System.Runtime.InteropServices;
public static class Wn3 {
  public delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(P cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
  public static IntPtr Find(int pid, string prefix) {
    IntPtr r = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == (uint)pid) { var sb = new StringBuilder(300); GetWindowText(h, sb, 300);
        if (sb.ToString().StartsWith(prefix)) { r = h; return false; } }
      return true; }, IntPtr.Zero);
    return r;
  }
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(150);
    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
  }
}
"@

function Save-Shot([string]$path) {
    $b = New-Object System.Drawing.Bitmap([System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width, [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height)
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.CopyFromScreen(0, 0, 0, 0, $b.Size)
    $b.Save($path)
    $g.Dispose(); $b.Dispose()
}

function Click-Element($win, [string]$name) {
    $all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) {
        if ($e.Current.Name -eq $name) {
            $r = $e.Current.BoundingRectangle
            if ($r.Width -le 0) { continue }
            $x = [int]($r.X + $r.Width / 2)
            $y = [int]($r.Y + $r.Height / 2)
            Write-Output "click $name at $x,$y"
            [Wn3]::Click($x, $y)
            return $true
        }
    }
    Write-Output "NOT FOUND: $name"
    return $false
}

$exe = Join-Path $Root "dist\SongBang-Setup-4.0.0.exe"
Start-Process $exe | Out-Null
Start-Sleep -Seconds 2

$proc = Get-Process -Name "SongBang-Setup-4.0.0" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "installer not running"; return }
$h = [Wn3]::Find($proc.Id, "安装")
if ($h -eq [IntPtr]::Zero) { Write-Output "window not found"; return }

$win = [System.Windows.Automation.AutomationElement]::FromHandle($h)
Click-Element $win "安装" | Out-Null
Start-Sleep -Seconds 3
Save-Shot $Shot
Write-Output "saved $Shot"

$win = [System.Windows.Automation.AutomationElement]::FromHandle($h)
Click-Element $win "完成" | Out-Null
Start-Sleep -Seconds 3
Write-Output "done"