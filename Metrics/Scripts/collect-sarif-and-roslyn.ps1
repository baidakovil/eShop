#!/usr/bin/env pwsh
# collect-sarif-and-roslyn.ps1
# Builds the solution to refresh Roslyn metrics XML via the GenerateRoslynMetrics
# target and SARIF diagnostics via the compiler ErrorLog configured for builds.
# Called by `metricsreporter generate` before aggregation and by
# `metricsreporter read` when SARIF violation metrics are queried.

$ErrorActionPreference = 'Stop'

Write-Host "Building solution to collect Roslyn metrics and SARIF diagnostics..."
dotnet build eShop.slnx --no-incremental 2>&1
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}
Write-Host "Build completed."