param(
    [string]$Root = "C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao",
    [string]$Out  = "C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao\_test"
)

Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class IN2 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern IntPtr GetFocus();
  public static void Key(byte vk, bool up) { keybd_event(vk, 0, up ? 0x0002u : 0u, UIntPtr.Zero); }
  public static string Fg() { var sb = new StringBuilder(300); GetWindowText(GetForegroundWindow(), sb, 300); return sb.ToString(); }
}
"@

$log = Join-Path $env:TEMP "dragprobe.log"
$pv = Join-Path $Out "pv5"
Get-Process SongBang, DragProbe -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

Remove-Item $pv -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $pv | Out-Null
Copy-Item (Join-Path $Root "Chutouniao.exe") (Join-Path $pv "Chutouniao.exe")
@"
[General]
StartWithWindows=0
Hotkey=
[Profile]
Name=DragProbe
Keys=Ctrl+Shift+K
"@ | Set-Content (Join-Path $pv "settings.ini") -Encoding UTF8
Start-Process (Join-Path $pv "Chutouniao.exe") | Out-Null
Start-Sleep -Milliseconds 1500

$probeExe = Join-Path $Out "DragProbe.exe"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
& $csc /nologo /target:winexe /platform:anycpu "/out:$probeExe" "/r:$fw\System.dll" "/r:$fw\System.Drawing.dll" "/r:$fw\System.Windows.Forms.dll" (Join-Path $Out "DragProbe.cs") 2>&1 | Out-Null

$p = Start-Process -FilePath $probeExe -PassThru
Start-Sleep -Milliseconds 1600
[IN2]::SetCursorPos(430, 430) | Out-Null
Start-Sleep -Milliseconds 200
[IN2]::mouse_event(0x0002,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 60; [IN2]::mouse_event(0x0004,0,0,0,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 800
Write-Output "foreground = '$([IN2]::Fg())'"
Set-Content $log "" -Encoding UTF8

Write-Output "--- 先按 Ctrl+Shift+K（应被拦） ---"
[IN2]::Key(0x11,$false); [IN2]::Key(0x10,$false)
Start-Sleep -Milliseconds 120
[IN2]::Key(0x4B,$false); Start-Sleep -Milliseconds 60; [IN2]::Key(0x4B,$true)
Start-Sleep -Milliseconds 100
[IN2]::Key(0x11,$true); [IN2]::Key(0x10,$true)
Start-Sleep -Milliseconds 600
Get-Content $log -Encoding UTF8
Write-Output "foreground = '$([IN2]::Fg())'"

Set-Content $log "" -Encoding UTF8
Write-Output "--- 再按 Ctrl+K（应放行） ---"
[IN2]::Key(0x11,$false)
Start-Sleep -Milliseconds 120
[IN2]::Key(0x4B,$false); Start-Sleep -Milliseconds 60; [IN2]::Key(0x4B,$true)
Start-Sleep -Milliseconds 100
[IN2]::Key(0x11,$true)
Start-Sleep -Milliseconds 600
Get-Content $log -Encoding UTF8
Write-Output "foreground = '$([IN2]::Fg())'"

Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Get-Process SongBang | Stop-Process -Force -ErrorAction SilentlyContinue
