# 批次下載財報與股東會年報 (Windows PowerShell 5.1 可執行)
#
# - 財報：西元 2026 ~ 2021 年 (民國 115 ~ 110)
#         範圍內最新年度下載全部季別 (加 -l)，其餘年度只下載第四季
# - 年報：西元 2025 ~ 2023 年 (民國 114 ~ 112)
# - 輸出目錄：本腳本所在目錄
#
# 用法：
#   powershell -ExecutionPolicy Bypass -File .\download.ps1 2330
#   powershell -ExecutionPolicy Bypass -File .\download.ps1 2330,8436
#   powershell -ExecutionPolicy Bypass -File .\download.ps1 2330 8436
#
# 參數：
#   股票代號，可多檔，以逗號或空白分隔 (必填，未指定時會提示輸入)

param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$StockIds
)

# ===== 設定 =====

# MopsDownloader.exe 路徑 (相對路徑以本腳本所在目錄為準)
$ExePath = 'D:\Lab\MopsDownloader\dist\1.0.1\MopsDownloader.exe'

# 檔案已存在時是否覆寫 ($true 覆寫 / $false 略過)
$Force = $false

# 年度範圍 (西元)
$FinancialYears = 2026..2021
$AnnualYears    = 2025..2023

# ================

$ErrorActionPreference = 'Stop'

# PowerShell 3.0 以上皆支援 $PSScriptRoot
$ScriptDir = $PSScriptRoot
$OutputDir = $ScriptDir

if (-not [System.IO.Path]::IsPathRooted($ExePath)) {
    $ExePath = Join-Path $ScriptDir $ExePath
}
if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) {
    Write-Host "找不到 MopsDownloader.exe：$ExePath" -ForegroundColor Red
    exit 2
}
$ExePath = (Resolve-Path -LiteralPath $ExePath).Path

# 允許 -StockIds "2330,8436" 或 -StockIds 2330,8436 兩種寫法
$ids = @($StockIds | ForEach-Object { $_ -split '[,\s]+' } | Where-Object { $_ } | Select-Object -Unique)
if ($ids.Count -eq 0) {
    Write-Host '請指定股票代號' -ForegroundColor Red
    exit 2
}
$idArg = $ids -join ','

# 讓 exe 輸出的 UTF-8 中文正確顯示
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
} catch {
    # 部分主控台環境不支援變更編碼，忽略
}

function ConvertTo-RocYear([int]$year) {
    return $year - 1911
}

$failed = New-Object System.Collections.Generic.List[string]

function Invoke-Downloader([string]$command, [int]$year, [switch]$Latest) {
    $roc = ConvertTo-RocYear $year
    $exeArgs = @($command, $idArg, $roc, '-o', $OutputDir)
    if ($Latest) {
        $exeArgs += '-l'
    }
    if ($Force) {
        $exeArgs += '-f'
    }

    & $ExePath $exeArgs
    if ($LASTEXITCODE -ne 0) {
        $failed.Add(("{0} {1} (民國 {2})" -f $command, $year, $roc))
    }
}

Write-Host "執行檔　：$ExePath"
Write-Host "股票代號：$idArg"
Write-Host "輸出目錄：$OutputDir"
Write-Host ''

# 範圍內的最新年度下載全部季別，其餘年度只下載第四季
$LatestFinancialYear = ($FinancialYears | Measure-Object -Maximum).Maximum

Write-Host "===== 財報 $($FinancialYears[0]) ~ $($FinancialYears[-1]) =====" -ForegroundColor Cyan
foreach ($y in $FinancialYears) {
    Invoke-Downloader 'fin' $y -Latest:($y -eq $LatestFinancialYear)
}

Write-Host ''
Write-Host "===== 年報 $($AnnualYears[0]) ~ $($AnnualYears[-1]) =====" -ForegroundColor Cyan
foreach ($y in $AnnualYears) {
    Invoke-Downloader 'annual' $y
}

Write-Host ''
if ($failed.Count -gt 0) {
    Write-Host '以下項目有查無資料或下載失敗 (例如尚未公告)：' -ForegroundColor Yellow
    $failed | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    exit 1
}

Write-Host '全部完成' -ForegroundColor Green
exit 0
