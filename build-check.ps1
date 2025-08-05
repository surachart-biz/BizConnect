#!/usr/bin/env pwsh
# Quick build check script

Write-Host "Building BizConnect solution..." -ForegroundColor Cyan
dotnet build --no-restore

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded!" -ForegroundColor Green
} else {
    Write-Host "Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
}