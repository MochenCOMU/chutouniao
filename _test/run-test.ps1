Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class Inp {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  public const byte VK_CTRL = 0x11;
  public const uint KEYUP = 0x0002;
  public const uint F_LDOWN = 0x0002, F_LUP = 0x0004;
  public static void CtrlDown(){ keybd_event(VK_CTRL,0,0,UIntPtr.Zero); }
  public static void CtrlUp(){ keybd_event(VK_CTRL,0,KEYUP,UIntPtr.Zero); }
  public static void MDown(){ mouse_event(F_LDOWN,0,0,0,UIntPtr.Zero); }
  public static void MUp(){ mouse_event(F_LUP,0,0,0,UIntPtr.Zero); }
  public static string Fg(){ var sb=new StringBuilder(300); GetWindowText(GetForegroundWindow(), sb, 300); return sb.ToString(); }
}
"@

$root = "C:\Users\shiqi\Documents\deepseek-harness\default-workspace\chutouniao"
$log  = Join-Path $env:TEMP "dragprobe.log"
$glog = Join-Path $env:TEMP "psctrlguard.log"

function Get-Rect {
    for ($i=0; $i -lt 40; $i++) {
        if (Test-Path $log) {
            $line = Get-Content $log | Where-Object { $_ -like "RECT *" } | Select-Object -First 1
            if ($line) { return ($line -split ' ')[1..4] | ForEach-Object { [int]$_ } }
        }
        Start-Sleep -Milliseconds 100
    }
    return $null
}

function ClickAt($x, $y) {
    [Inp]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 150
    [Inp]::MDown(); Start-Sleep -Milliseconds 60; [Inp]::MUp()
    Start-Sleep -Milliseconds 500
}

function DragCtrl($x, $y) {
    if ([Inp]::Fg() -ne "DragProbe") { Write-Output "  SKIP: foreground is '$([Inp]::Fg())'"; return }
    [Inp]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 200
    [Inp]::CtrlDown()
    Start-Sleep -Milliseconds 150
    [Inp]::MDown()
    Start-Sleep -Milliseconds 80
    for ($i=1; $i -le 10; $i++) {
        [Inp]::SetCursorPos(($x + $i*18), ($y + $i*12)) | Out-Null
        Start-Sleep -Milliseconds 25
    }
    [Inp]::MUp()
    Start-Sleep -Milliseconds 80
    [Inp]::CtrlUp()
    Start-Sleep -Milliseconds 600
}

function ClickCtrl($x, $y) {
    if ([Inp]::Fg() -ne "DragProbe") { Write-Output "  SKIP: foreground is '$([Inp]::Fg())'"; return }
    [Inp]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 200
    [Inp]::CtrlDown()
    Start-Sleep -Milliseconds 150
    [Inp]::MDown(); Start-Sleep -Milliseconds 60; [Inp]::MUp()
    Start-Sleep -Milliseconds 150
    [Inp]::CtrlUp()
    Start-Sleep -Milliseconds 600
}

Stop-Process -Name SongBang,DragProbe -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600

Write-Output "===== A: guard ON ====="
Remove-Item $glog -ErrorAction SilentlyContinue
$env:PSCG_TARGET = "DragProbe"
$env:PSCG_DEBUG  = "1"
$guard = Start-Process -FilePath "$root\Chutouniao.exe" -PassThru
Start-Sleep -Milliseconds 1500
Write-Output "guard alive = $(-not $guard.HasExited)"
$probe = Start-Process -FilePath "$root\_test\DragProbe.exe" -PassThru
$r = Get-Rect
if (-not $r) { Write-Output "FATAL: no RECT"; exit 1 }
$cx = [int](($r[0] + $r[2]) / 2); $cy = [int](($r[1] + $r[3]) / 2)
Write-Output "probe rect = $($r -join ',')  center = $cx,$cy"
ClickAt $cx $cy
Write-Output "foreground now = '$([Inp]::Fg())'"
Set-Content -Path $log -Value "" -Encoding UTF8
DragCtrl $cx $cy
ClickCtrl ($cx - 40) ($cy - 40)
Start-Sleep -Milliseconds 400
Stop-Process -Id $probe.Id -Force
Start-Sleep -Milliseconds 700
Write-Output "--- probe log (A) ---"
Get-Content $log -Encoding UTF8
Write-Output "--- guard log (A) ---"
if (Test-Path $glog) { Get-Content $glog -Encoding UTF8 } else { Write-Output "(missing)" }

Write-Output "===== B: guard OFF (control) ====="
Stop-Process -Id $guard.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 900
$probe2 = Start-Process -FilePath "$root\_test\DragProbe.exe" -PassThru
$r2 = Get-Rect
$cx2 = [int](($r2[0] + $r2[2]) / 2); $cy2 = [int](($r2[1] + $r2[3]) / 2)
ClickAt $cx2 $cy2
Write-Output "foreground now = '$([Inp]::Fg())'"
Set-Content -Path $log -Value "" -Encoding UTF8
DragCtrl $cx2 $cy2
Start-Sleep -Milliseconds 400
Stop-Process -Id $probe2.Id -Force
Start-Sleep -Milliseconds 700
Write-Output "--- probe log (B) ---"
Get-Content $log -Encoding UTF8
