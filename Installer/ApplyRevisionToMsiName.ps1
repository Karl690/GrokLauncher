$ErrorActionPreference = "Stop"

$installerDir = $PSScriptRoot
$headerPath = Join-Path (Split-Path $installerDir -Parent) "revisionHistory.h"
if (-not (Test-Path $headerPath)) { throw "revisionHistory.h was not found at $headerPath" }

$revision = $null
foreach ($line in Get-Content -LiteralPath $headerPath)
{
    if ($line -match '^\s*#define\s+APP_REVISION\s+"([^"]+)"')
    {
        $revision = $Matches[1]
    }
}

if ([string]::IsNullOrWhiteSpace($revision)) { throw "APP_REVISION was not found in revisionHistory.h" }

$revisionNumber = $revision.TrimStart([char[]]@('V', 'v'))
$setupStem = "GrokLauncherSetup"
$desiredFileName = "${setupStem}_${revisionNumber}.msi"

foreach ($configName in @("Release", "Debug"))
{
    $configDir = Join-Path $installerDir $configName
    if (-not (Test-Path $configDir)) { continue }

    $desiredPath = Join-Path $configDir $desiredFileName
    $plainPath = Join-Path $configDir ($setupStem + ".msi")

    if (Test-Path $plainPath)
    {
        if (Test-Path $desiredPath) { Remove-Item -LiteralPath $desiredPath -Force }
        Move-Item -LiteralPath $plainPath -Destination $desiredPath
    }

    Get-ChildItem -LiteralPath $configDir -Filter ($setupStem + "_*.msi") | ForEach-Object {
        if ($_.Name -eq $desiredFileName) { return }
        if (Test-Path $desiredPath) { Remove-Item -LiteralPath $desiredPath -Force }
        Move-Item -LiteralPath $_.FullName -Destination $desiredPath
    }
}

Write-Output $desiredFileName
