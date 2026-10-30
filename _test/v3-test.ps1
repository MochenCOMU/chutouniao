param(
    [string]$Root = "C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao",
    [string]$Out  = "C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao\_test"
)

Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class In {
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
  public static void RDown() { mouse_event(0x0008, 0, 0, 0, UIntPtr.Zero); }
  public static void RUp() { mouse_event(0x0010, 0, 0, 0, UIntPtr.Zero); }
  public static string Fg() { var sb = new StringBuilder(300); GetWindowText(GetForegroundWindow(), sb, 300); return sb.ToString(); }
}
"@

$log = Join-Path $env:TEMP "dragprobe.log"
$pv = Join-Path $Out "pv3"

function Clear-Log { Set-Content -Path $log -Value "" -Encoding UTF8 }
function Count-Of($pattern) { return (Get-Content $log -ErrorAction SilentlyContinue | Where-Object { $_ -like $pattern } | Measure-Object).Count }
function Check-Fg { return ([In]::Fg() -eq "DragProbe") }

function DragWith([byte[]]$mods, [string]$button) {
    if (-not (Check-Fg)) { Write-Output "  SKIP (foreground=$([In]::Fg()))"; return $false }
    [In]::SetCursorPos(430, 430) | Out-Null
    foreach ($m in $mods) { [In]::Key($m, $false) }
    Start-Sleep -Milliseconds 150
    if ($button -eq "right") { [In]::RDown() } else { [In]::MDown() }
    Start-Sleep -Milliseconds 80
    for ($i = 1; $i -le 10; $i++) { [In]::SetCursorPos((430 + $i * 15), (430 + $i * 10)) | Out-Null; Start-Sleep -Milliseconds 22 }
    if ($button -eq "right") { [In]::RUp() } else { [In]::MUp() }
    Start-Sleep -Milliseconds 80
    foreach ($m in $mods) { [In]::Key($m, $true) }
    Start-Sleep -Milliseconds 450
    return $true
}

function Focus-Probe {
    [In]::SetCursorPos(430, 430) | Out-Null
    Start-Sleep -Milliseconds 150
    [In]::MDown(); Start-Sleep -Milliseconds 60; [In]::MUp()
    Start-Sleep -Milliseconds 450
    Clear-Log
}

function PressKeys([byte[]]$mods, [byte]$key) {
    if (-not (Check-Fg)) { Write-Output "  SKIP (foreground=$([In]::Fg()))"; return $false }
    foreach ($m in $mods) { [In]::Key($m, $false) }
    Start-Sleep -Milliseconds 120
    [In]::Key($key, $false); Start-Sleep -Milliseconds 60; [In]::Key($key, $true)
    Start-Sleep -Milliseconds 100
    foreach ($m in $mods) { [In]::Key($m, $true) }
    Start-Sleep -Milliseconds 400
    return $true
}

Write-Output "===== setup ====="
Get-Process SongBang, DragProbe -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700

if (Test-Path $pv) { Remove-Item $pv -Recurse -Force }
New-Item -ItemType Directory -Path $pv | Out-Null
Copy-Item (Join-Path $Root "Chutouniao.exe") (Join-Path $pv "Chutouniao.exe")
@"
; v3 portable test config
[General]
StartWithWindows=0
Hotkey=
ReplayClick=1
ClickThreshold=5
Beep=0
Flash=0
TotalBlocked=0

[Profile]
Name=DragProbe
Mouse=Ctrl+Alt+Left;Alt+Right
Keys=Ctrl+Shift+K
"@ | Set-Content (Join-Path $pv "settings.ini") -Encoding UTF8

Start-Process -FilePath (Join-Path $pv "Chutouniao.exe") | Out-Null
Start-Sleep -Milliseconds 1500

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$probeExe = Join-Path $Out "DragProbe.exe"
& $csc /nologo /target:winexe /platform:anycpu "/out:$probeExe" "/r:$fw\System.dll" "/r:$fw\System.Drawing.dll" "/r:$fw\System.Windows.Forms.dll" (Join-Path $Out "DragProbe.cs") 2>&1 | Out-Null
if (-not (Test-Path $probeExe)) { Write-Output "ABORT: DragProbe 编译失败"; Get-Process SongBang | Stop-Process -Force; return }

$p = Start-Process -FilePath $probeExe -PassThru
Start-Sleep -Milliseconds 1600
[In]::SetCursorPos(430, 430) | Out-Null
Start-Sleep -Milliseconds 200
[In]::MDown(); Start-Sleep -Milliseconds 60; [In]::MUp()
Start-Sleep -Milliseconds 800
Write-Output "foreground = '$([In]::Fg())'"
if (-not (Check-Fg)) { Write-Output "ABORT: probe 没拿到前台"; Get-Process SongBang, DragProbe | Stop-Process -Force; return }

Clear-Log
$r1 = DragWith @() "left"
$c1 = Count-Of "DRAG*"

Clear-Log
$r2 = DragWith @([In]::CTRL) "left"
$c2 = Count-Of "DRAG*"

Clear-Log
$r3 = DragWith @([In]::CTRL, [In]::ALT) "left"
$c3 = Count-Of "DRAG*"
$d3 = Count-Of "DOWN *"

Clear-Log
$r4 = DragWith @([In]::ALT) "right"
$c4 = Count-Of "DOWN-Right*"

Clear-Log
Focus-Probe
$r5 = PressKeys @([In]::CTRL, [In]::SHIFT) 0x4B
$c5 = Count-Of "KEY*"
Write-Output "--- case5 log ---"; Get-Content $log -Encoding UTF8
Write-Output "fg=$([In]::Fg())"

Clear-Log
Focus-Probe
$r6 = PressKeys @([In]::CTRL) 0x4B
$c6 = Count-Of "KEY*"
Write-Output "--- case6 log ---"; Get-Content $log -Encoding UTF8
Write-Output "fg=$([In]::Fg())"

Start-Sleep -Milliseconds 300
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Get-Process SongBang -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Output "===== results ====="
Write-Output ("1 裸拖拽         期望 DRAG>0 : {0}  {1}" -f $c1, $(if ($c1 -gt 0) { "PASS" } else { "FAIL" }))
Write-Output ("2 Ctrl+拖拽      期望 DRAG>0 : {0}  {1}" -f $c2, $(if ($c2 -gt 0) { "PASS" } else { "FAIL" }))
Write-Output ("3 Ctrl+Alt+拖拽  期望 0 事件 : drag={0} down={1}  {2}" -f $c3, $d3, $(if ($c3 -eq 0 -and $d3 -eq 0) { "PASS" } else { "FAIL" }))
Write-Output ("4 Alt+右键拖拽   期望 0 事件 : {0}  {1}" -f $c4, $(if ($c4 -eq 0) { "PASS" } else { "FAIL" }))
Write-Output ("5 Ctrl+Shift+K   期望 0 KEY  : {0}  {1}" -f $c5, $(if ($c5 -eq 0) { "PASS" } else { "FAIL" }))
Write-Output ("6 Ctrl+K         期望 KEY>0  : {0}  {1}" -f $c6, $(if ($c6 -gt 0) { "PASS" } else { "FAIL" }))
