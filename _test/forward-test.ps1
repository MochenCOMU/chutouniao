param(
    [string]$Root = "C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao",
    [string]$Out  = "C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao\_test"
)

Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class Fwd {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  public const byte CTRL = 0x11, ALT = 0x12, SHIFT = 0x10;
  public const uint UP = 0x0002;
  public static void Key(byte vk, bool up) { keybd_event(vk, 0, up ? UP : 0, UIntPtr.Zero); }
  public static void MDown() { mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); }
  public static void MUp() { mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); }
  public static string Fg() { var sb = new StringBuilder(300); GetWindowText(GetForegroundWindow(), sb, 300); return sb.ToString(); }
}
"@

$log = Join-Path $env:TEMP "dragprobe.log"
$pv  = Join-Path $Out "pv6"

function Clear-Log { Set-Content -Path $log -Value "" -Encoding UTF8 }
function Count-Of($p) { return (Get-Content $log -ErrorAction SilentlyContinue | Where-Object { $_ -like $p } | Measure-Object).Count }
function Focus-Probe {
    [Fwd]::SetCursorPos(430, 430) | Out-Null
    Start-Sleep -Milliseconds 150
    [Fwd]::MDown(); Start-Sleep -Milliseconds 60; [Fwd]::MUp()
    Start-Sleep -Milliseconds 450
    Clear-Log
}
function PressCombo([byte[]]$mods, [byte]$key, [int]$holdMs = 60) {
    if ([Fwd]::Fg() -ne "DragProbe") { Write-Output "  SKIP (fg=$([Fwd]::Fg()))"; return }
    foreach ($m in $mods) { [Fwd]::Key($m, $false) }
    Start-Sleep -Milliseconds 130
    [Fwd]::Key($key, $false)
    Start-Sleep -Milliseconds $holdMs
    [Fwd]::Key($key, $true)
    Start-Sleep -Milliseconds 120
    foreach ($m in $mods) { [Fwd]::Key($m, $true) }
    Start-Sleep -Milliseconds 500
}

Write-Output "===== setup ====="
Get-Process SongBang, DragProbe -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700

if (Test-Path $pv) { Remove-Item $pv -Recurse -Force }
New-Item -ItemType Directory -Path $pv | Out-Null
Copy-Item (Join-Path $Root "Chutouniao.exe") (Join-Path $pv "Chutouniao.exe")
@"
; forwarding test
[General]
StartWithWindows=0
Hotkey=
[Profile]
Name=DragProbe
Keys=Ctrl+Shift+K>Alt+J;Ctrl+Alt+M>Ctrl+M
"@ | Set-Content (Join-Path $pv "settings.ini") -Encoding UTF8

Start-Process (Join-Path $pv "Chutouniao.exe") | Out-Null
Start-Sleep -Milliseconds 1500

$probeExe = Join-Path $Out "DragProbe.exe"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
& $csc /nologo /target:winexe /platform:anycpu "/out:$probeExe" "/r:$fw\System.dll" "/r:$fw\System.Drawing.dll" "/r:$fw\System.Windows.Forms.dll" (Join-Path $Out "DragProbe.cs") 2>&1 | Out-Null
if (-not (Test-Path $probeExe)) { Write-Output "ABORT: 测试窗口没编译出来"; return }

$p = Start-Process -FilePath $probeExe -PassThru
Start-Sleep -Milliseconds 1600
Focus-Probe
Write-Output "foreground = '$([Fwd]::Fg())'"

Write-Output "===== 1) Ctrl+Shift+K  ->  期望变成 Alt+J ====="
Focus-Probe
PressCombo @([Fwd]::CTRL, [Fwd]::SHIFT) 0x4B
Get-Content $log -Encoding UTF8
$kJ = Count-Of "KEY J*"
$kK = Count-Of "KEY K*"

Write-Output "===== 2) 按住不放（自动重复）-> 只应转发一次 ====="
Focus-Probe
PressCombo @([Fwd]::CTRL, [Fwd]::SHIFT) 0x4B 600
$kJ2 = Count-Of "KEY J*"

Write-Output "===== 3) Ctrl+Alt+M  ->  期望变成 Ctrl+M（要替用户松开 Alt） ====="
Focus-Probe
PressCombo @([Fwd]::CTRL, [Fwd]::ALT) 0x4D
Get-Content $log -Encoding UTF8
$kM = (Get-Content $log | Where-Object { $_ -like "KEY M*" })
$kMctrl = (Get-Content $log | Where-Object { $_ -like "KEY M, Control*" } | Measure-Object).Count
$kMalt = (Get-Content $log | Where-Object { $_ -like "*Alt*" -and $_ -like "KEY M*" } | Measure-Object).Count

Start-Sleep -Milliseconds 300
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Get-Process SongBang -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Output "===== results ====="
Write-Output ("1 Ctrl+Shift+K -> Alt+J    : J={0} K={1}  {2}" -f $kJ, $kK, $(if ($kJ -eq 1 -and $kK -eq 0) { "PASS" } else { "FAIL" }))
Write-Output ("2 按住自动重复只转一次     : J={0}        {1}" -f $kJ2, $(if ($kJ2 -eq 1) { "PASS" } else { "FAIL" }))
Write-Output ("3 Ctrl+Alt+M -> Ctrl+M     : Ctrl+M={0} Alt+M={1}  {2}" -f $kMctrl, $kMalt, $(if ($kMctrl -eq 1 -and $kMalt -eq 0) { "PASS" } else { "FAIL" }))
