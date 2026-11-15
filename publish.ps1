# 發佈 MopsDownloader 到 dist\<版本> 資料夾
# 版本號取自 src\MopsDownloader\MopsDownloader.csproj 的 <Version>
# 用法：.\publish.ps1          (版本資料夾已存在時中止)
#       .\publish.ps1 -Force   (覆寫已存在的版本資料夾)
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'src\MopsDownloader\MopsDownloader.csproj'

[xml]$csproj = Get-Content $project -Raw -Encoding UTF8
$version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) {
    throw "找不到 csproj 中的 <Version>"
}

$outDir = Join-Path $root "dist\$version"
if (Test-Path $outDir) {
    if (-not $Force) {
        throw "版本資料夾已存在：$outDir`n請先調整 csproj 的 <Version>，或加上 -Force 覆寫"
    }
    Remove-Item $outDir -Recurse -Force
}

Write-Host "發佈 MopsDownloader $version -> $outDir"
dotnet publish $project -c Release -o $outDir --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失敗 (exit $LASTEXITCODE)"
}

Write-Host ''
Get-ChildItem $outDir | Format-Table Name, Length -AutoSize
