#!/usr/bin/env pwsh

Write-Host "Testing build of BizConnect.Dal..." -ForegroundColor Yellow
& dotnet build "BizConnect.Dal\BizConnect.Dal.csproj" --verbosity minimal --no-restore 2>&1
Write-Host "Dal build exit code: $LASTEXITCODE" -ForegroundColor ($LASTEXITCODE -eq 0 ? "Green" : "Red")

Write-Host "`nTesting build of BizConnect.Services..." -ForegroundColor Yellow  
& dotnet build "BizConnect.Services\BizConnect.Services.csproj" --verbosity minimal --no-restore 2>&1
Write-Host "Services build exit code: $LASTEXITCODE" -ForegroundColor ($LASTEXITCODE -eq 0 ? "Green" : "Red")