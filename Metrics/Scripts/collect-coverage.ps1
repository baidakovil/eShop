#!/usr/bin/env pwsh
# collect-coverage.ps1
# Runs the repository's MSBuild coverage target to collect OpenCover XML and,
# unless disabled, generate the aggregated HTML report.
#
# Called by `metricsreporter read` when coverage metrics are queried.

param(
    [bool]$GenerateHtml = $true
)

$ErrorActionPreference = 'Stop'

$Root = (Resolve-Path "$PSScriptRoot/../..").Path

# CollectMetricsCoverage owns the canonical list of test projects. The Basket
# unit test project is only used as the MSBuild entry point for that target.
Write-Host "Running CollectMetricsCoverage target..."
Push-Location $Root
try {
    & dotnet msbuild "tests/Basket.UnitTests/Basket.UnitTests.csproj" -t:CollectMetricsCoverage "-p:GenerateMetricsHtmlReportEnabled=$GenerateHtml"
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

Write-Host "Coverage collection completed."
