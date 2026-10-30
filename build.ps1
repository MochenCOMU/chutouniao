# 编译 出头鸟 Chutouniao，并生成单文件安装包
# 用法： .\build.ps1              编译主程序 + 安装包
#        .\build.ps1 -Run         编译完顺便跑一下主程序
#        .\build.ps1 -NoSetup     只编译主程序
param(
    [string]$Version = "4.1.0",
    [switch]$NoSetup,
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root "src"
$exe  = Join-Path $root "Chutouniao.exe"
$dist = Join-Path $root "dist"
$setup = Join-Path $dist ("Chutouniao-Setup-" + $Version + ".exe")

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path $csc)) { throw "找不到 .NET Framework 编译器：$csc" }

# 正在运行的实例会锁住 exe
Get-Process Chutouniao, SongBang, PsCtrlGuard -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

Write-Host "编译主程序 ..."
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 /out:"$exe" `
    "/r:$fw\System.dll" "/r:$fw\System.Drawing.dll" "/r:$fw\System.Windows.Forms.dll" "/r:$fw\System.Core.dll" `
    /win32icon:"$src\icon.ico" /win32manifest:"$src\app.manifest" `
    "/resource:$src\avatar.png,avatar.png" `
    "$src\Program.cs" "$src\App.cs" "$src\Config.cs" "$src\Rules.cs" "$src\Interceptor.cs" `
    "$src\HotkeyBox.cs" "$src\SettingsForm.cs" "$src\ProcessPickerForm.cs" "$src\AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0) { throw "编译失败" }
Write-Host ("  OK  {0:N0} bytes" -f (Get-Item $exe).Length)

if (-not $NoSetup) {
    Write-Host "生成安装包 ..."
    if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }

    $readme = Join-Path $root "dist-src\使用说明.txt"
    & $csc /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 /out:"$setup" `
        "/r:$fw\System.dll" "/r:$fw\System.Drawing.dll" "/r:$fw\System.Windows.Forms.dll" `
        /win32icon:"$src\icon.ico" /win32manifest:"$src\app.manifest" `
        "/resource:$exe,Chutouniao.exe" "/resource:$readme,Readme.txt" `
        (Join-Path $root "installer\Setup.cs")
    if ($LASTEXITCODE -ne 0) { throw "安装包编译失败" }
    Write-Host ("  OK  {0}  ({1:N0} bytes)" -f (Split-Path -Leaf $setup), (Get-Item $setup).Length)
}

if ($Run) {
    Write-Host "启动 ..."
    Start-Process -FilePath $exe
}

Write-Host ""
Write-Host "完成。"
if (-not $NoSetup) { Write-Host ("安装包：" + $setup) }
