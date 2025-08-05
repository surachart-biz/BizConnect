#!/usr/bin/env pwsh

Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore "BizConnect.sln" --verbosity minimal

Write-Host "`nBuilding BizConnect.Dal..." -ForegroundColor Yellow
$dalResult = dotnet build "BizConnect.Dal\BizConnect.Dal.csproj" --no-restore --verbosity minimal
Write-Host "Dal build result: $LASTEXITCODE" -ForegroundColor ($LASTEXITCODE -eq 0 ? "Green" : "Red")

if ($LASTEXITCODE -ne 0) {
    Write-Host "Dal build failed, stopping..." -ForegroundColor Red
    exit 1
}

Write-Host "`nBuilding BizConnect.Services..." -ForegroundColor Yellow
$servicesResult = dotnet build "BizConnect.Services\BizConnect.Services.csproj" --no-restore --verbosity minimal
Write-Host "Services build result: $LASTEXITCODE" -ForegroundColor ($LASTEXITCODE -eq 0 ? "Green" : "Red")

if ($LASTEXITCODE -ne 0) {
    Write-Host "Services build failed, stopping..." -ForegroundColor Red
    exit 1
}

Write-Host "`nBuilding BizConnect main..." -ForegroundColor Yellow
$mainResult = dotnet build "BizConnect\BizConnect.csproj" --no-restore --verbosity minimal
Write-Host "Main build result: $LASTEXITCODE" -ForegroundColor ($LASTEXITCODE -eq 0 ? "Green" : "Red")

if ($LASTEXITCODE -ne 0) {
    Write-Host "Main build failed, stopping..." -ForegroundColor Red
    exit 1
}

Write-Host "`nBuilding Tests..." -ForegroundColor Yellow
$testsResult = dotnet build "BizConnect.Tests\BizConnect.Tests.csproj" --no-restore --verbosity minimal
Write-Host "Tests build result: $LASTEXITCODE" -ForegroundColor ($LASTEXITCODE -eq 0 ? "Green" : "Red")

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ ALL PROJECTS COMPILED SUCCESSFULLY!" -ForegroundColor Green
} else {
    Write-Host "`n❌ Tests build failed" -ForegroundColor Red
    exit 1
}