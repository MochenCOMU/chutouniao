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
  public static string Fg() { var sb = new StringBuilder(300); GetWindowText(GetForegroundWindow(), sb, 300); return sb.ToString(); }
}
"@

$log  = Join-Path $env:TEMP "dragprobe.log"
$pv   = Join-Path $Out "pv"
$probe = Join-Path $Out "DragProbe.exe"

function Clear-Log { Set-Content -Path $log -Value "" -Encoding UTF8 }

function Combo-Drag([byte[]]$mods, [string]$label) {
    if ([In]::Fg() -ne "DragProbe") { Write-Output "  SKIP $label (foreground=$([In]::Fg()))"; return }
    [In]::SetCursorPos(420, 420) | Out-Null
    foreach ($m in $mods) { [In]::Key($m, $false) }
    Start-Sleep -Milliseconds 150
    [In]::MDown()
    Start-Sleep -Milliseconds 80
    for ($i = 1; $i -le 10; $i++) {
        [In]::SetCursorPos((420 + $i * 16), (420 + $i * 10)) | Out-Null
        Start-Sleep -Milliseconds 25
    }
    [In]::MUp()
    Start-Sleep -Milliseconds 80
    foreach ($m in $mods) { [In]::Key($m, $true) }
    Start-Sleep -Milliseconds 500
}

Write-Output "===== setup ====="
Get-Process SongBang, DragProbe -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700

# 便携版配置：只拦 Ctrl+Alt，进程名 DragProbe
if (Test-Path $pv) { Remove-Item $pv -Recurse -Force }
New-Item -ItemType Directory -Path $pv | Out-Null
Copy-Item (Join-Path $Root "Chutouniao.exe") (Join-Path $pv "Chutouniao.exe")
@"
; portable test config
Targets=DragProbe
BlockCtrl=0
BlockCtrlAlt=1
BlockCtrlShift=0
BlockAlt=0
BlockShift=0
ReplayClick=1
ClickThreshold=5
Beep=0
Flash=0
StartWithWindows=0
Hotkey=无
TotalBlocked=0
"@ | Set-Content (Join-Path $pv "settings.ini") -Encoding UTF8

Start-Process -FilePath (Join-Path $pv "Chutouniao.exe") | Out-Null
Start-Sleep -Milliseconds 1500

# 重新编译测试窗口
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
& $csc /nologo /target:winexe /platform:anycpu /out:$probe /r:"$fw\System.dll","$fw\System.Drawing.dll","$fw\System.Windows.Forms.dll" (Join-Path $Out "DragProbe.cs") 2>&1 | Out-Null

$p = Start-Process -FilePath $probe -PassThru
Start-Sleep -Milliseconds 1600
[In]::SetCursorPos(420, 420) | Out-Null
Start-Sleep -Milliseconds 200
[In]::MDown(); Start-Sleep -Milliseconds 60; [In]::MUp()
Start-Sleep -Milliseconds 700
Write-Output "foreground = '$([In]::Fg())'"

Write-Output "===== 1) Ctrl + drag  (规则里=0，应该不拦) ====="
Clear-Log
Combo-Drag @([In]::CTRL) "ctrl"
$c1 = (Get-Content $log | Where-Object { $_ -like "DRAG*" } | Measure-Object).Count
Write-Output "DRAG 事件数: $c1"

Write-Output "===== 2) Ctrl+Alt + drag (规则里=1，应该拦) ====="
Clear-Log
Combo-Drag @([In]::CTRL, [In]::ALT) "ctrl+alt"
$c2 = (Get-Content $log | Where-Object { $_ -like "DRAG*" } | Measure-Object).Count
$d2 = (Get-Content $log | Where-Object { $_ -like "DOWN*" } | Measure-Object).Count
Write-Output "DRAG 事件数: $c2   DOWN 事件数: $d2"

Write-Output "===== 3) Ctrl+Shift + drag (规则里=0，应该不拦) ====="
Clear-Log
Combo-Drag @([In]::CTRL, [In]::SHIFT) "ctrl+shift"
$c3 = (Get-Content $log | Where-Object { $_ -like "DRAG*" } | Measure-Object).Count
Write-Output "DRAG 事件数: $c3"

Write-Output "===== 4) Alt + drag  (规则里=0，应该不拦) ====="
Clear-Log
Combo-Drag @([In]::ALT) "alt"
$c4 = (Get-Content $log | Where-Object { $_ -like "DRAG*" } | Measure-Object).Count
Write-Output "DRAG 事件数: $c4"

Start-Sleep -Milliseconds 300
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Get-Process SongBang -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Output "===== 结论 ====="
Write-Output ("1 Ctrl(允许)  期望>0 : {0}  {1}" -f $c1, $(if ($c1 -gt 0) { "PASS" } else { "FAIL" }))
Write-Output ("2 Ctrl+Alt(拦) 期望=0 : {0}  {1}" -f $c2, $(if ($c2 -eq 0 -and $d2 -eq 0) { "PASS" } else { "FAIL" }))
Write-Output ("3 Ctrl+Shift(允许) 期望>0 : {0}  {1}" -f $c3, $(if ($c3 -gt 0) { "PASS" } else { "FAIL" }))
Write-Output ("4 Alt(允许) 期望>0 : {0}  {1}" -f $c4, $(if ($c4 -gt 0) { "PASS" } else { "FAIL" }))
