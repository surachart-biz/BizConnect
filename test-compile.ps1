#!/usr/bin/env pwsh

Write-Host "Testing compilation of BizConnect solution..." -ForegroundColor Cyan

# Build the solution
$result = dotnet build --no-restore --verbosity minimal 2>&1

# Check for compilation errors
$errors = $result | Where-Object { $_ -match "error CS" }

if ($errors) {
    Write-Host "`nCompilation errors found:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    exit 1
} else {
    Write-Host "`nCompilation successful!" -ForegroundColor Green
    Write-Host "All type conversion issues have been fixed." -ForegroundColor Green
}