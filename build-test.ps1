#!/usr/bin/env pwsh

Write-Host "Building BizConnect.Dal project..."
$dalResult = dotnet build "BizConnect.Dal\BizConnect.Dal.csproj" --verbosity normal --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "BizConnect.Dal build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Building BizConnect.Services project..."
$servicesResult = dotnet build "BizConnect.Services\BizConnect.Services.csproj" --verbosity normal --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "BizConnect.Services build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Building BizConnect main project..."
$mainResult = dotnet build "BizConnect\BizConnect.csproj" --verbosity normal --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "BizConnect main build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "All projects built successfully!" -ForegroundColor Green