#!/usr/bin/env pwsh
# refresh-report.ps1
# Re-aggregates the JSON/HTML report from the existing Roslyn, SARIF, and
# OpenCover artifacts without re-running build or test targets.
# Called by `metricsreporter read`/`test` before reading the report.

$ErrorActionPreference = 'Stop'

Write-Host "Re-aggregating metrics report from existing artifacts..."
$dotnetToolsPath = Join-Path $env:HOME ".dotnet/tools"
$env:PATH = "$dotnetToolsPath$([System.IO.Path]::PathSeparator)$env:PATH"

metricsreporter generate --run-scripts false
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}
Write-Host "Report refresh completed."
