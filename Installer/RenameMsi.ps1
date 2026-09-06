param(
    [string]$MsiPath = ""
)

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$headerPath = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory "..\revisionHistory.h"))
if (-not (Test-Path -LiteralPath $headerPath)) { throw "revisionHistory.h was not found: $headerPath" }

$revision = $null
foreach ($line in Get-Content -LiteralPath $headerPath) {
    if ($line -match '^\s*#define\s+APP_REVISION\s+"([^"]+)"') {
        $revision = $Matches[1]
        break
    }
}
if ([string]::IsNullOrWhiteSpace($revision)) { throw "APP_REVISION was not found in $headerPath" }

if ([string]::IsNullOrWhiteSpace($MsiPath)) {
    $releaseMsi = Join-Path $scriptDirectory "Release\GrokLauncherSetup.msi"
    $debugMsi = Join-Path $scriptDirectory "Debug\GrokLauncherSetup.msi"
    if (Test-Path -LiteralPath $releaseMsi) { $MsiPath = $releaseMsi }
    elseif (Test-Path -LiteralPath $debugMsi) { $MsiPath = $debugMsi }
    else { throw "GrokLauncherSetup.msi was not found in Release or Debug." }
}

if (-not (Test-Path -LiteralPath $MsiPath)) { throw "MSI was not found: $MsiPath" }

$outputDirectory = Split-Path -Parent $MsiPath
$baseName = [System.IO.Path]::GetFileNameWithoutExtension($MsiPath)
$destinationPath = Join-Path $outputDirectory ($baseName + "_" + $revision + ".msi")

Get-ChildItem -LiteralPath $outputDirectory -Filter ($baseName + "_V*.msi") -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -ne $MsiPath } |
    Remove-Item -Force

if (Test-Path -LiteralPath $destinationPath) { Remove-Item -LiteralPath $destinationPath -Force }
Move-Item -LiteralPath $MsiPath -Destination $destinationPath
Write-Host "Renamed installer to $destinationPath"
