$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
$src = Join-Path $root 'src'
$build = Join-Path $root 'build'
$dist = Join-Path $root 'dist'

$csc = $null
$fwDir = $null
foreach ($cand in @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319'
)) {
    $exe = Join-Path $cand 'csc.exe'
    if (Test-Path $exe) { $csc = $exe; $fwDir = $cand; break }
}
if (-not $csc) { throw "找不到 .NET Framework 4.x 编译器 (csc.exe)。请安装 .NET Framework 4.x。" }

New-Item -ItemType Directory -Force -Path $dist | Out-Null

$icon = Join-Path $build 'app.ico'
if (-not (Test-Path $icon)) { throw "缺少图标: $icon" }
$manifest = Join-Path $build 'app.manifest'

$sources = Get-ChildItem -Path $src -Filter '*.cs' | ForEach-Object { $_.FullName }
$refs = @(
  "/r:$fwDir\System.dll",
  "/r:$fwDir\System.Core.dll",
  "/r:$fwDir\System.Drawing.dll",
  "/r:$fwDir\System.Windows.Forms.dll",
  "/r:$fwDir\Microsoft.CSharp.dll"
)

$outExe = Join-Path $dist 'SteamOfflineSwitch.exe'
$argsList = @(
  '/nologo',
  '/target:winexe',
  '/platform:anycpu',
  '/optimize+',
  "/out:$outExe",
  "/win32icon:$icon",
  "/win32manifest:$manifest"
) + $refs + $sources

Write-Host "编译 $outExe ..."
& $csc @argsList
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }
if (-not (Test-Path $outExe)) { throw "未生成 exe" }

$info = Get-Item $outExe
Write-Host ("编译成功: {0}  ({1:N1} KB)" -f $info.FullName, ($info.Length / 1KB))
