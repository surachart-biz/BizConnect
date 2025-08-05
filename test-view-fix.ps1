#!/usr/bin/env pwsh
# Test script to verify the view compilation fix

Write-Host "Testing view compilation fix..." -ForegroundColor Green
Write-Host "Building BizConnect solution..." -ForegroundColor Yellow

try {
    $result = dotnet build --no-restore --verbosity minimal 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "BUILD SUCCESS: View compilation errors have been resolved!" -ForegroundColor Green
        Write-Host "The following issues were fixed:" -ForegroundColor Cyan
        Write-Host "- Line 249: Fixed UpdatedAt.ToString() for nullable DateTime" -ForegroundColor White
        Write-Host "- Line 250: Fixed UpdatedAt.ToString() for nullable DateTime" -ForegroundColor White  
        Write-Host "- Line 371: Fixed UpdatedAt.ToString() for nullable DateTime" -ForegroundColor White
    } else {
        Write-Host "BUILD FAILED: There are still compilation issues" -ForegroundColor Red
        Write-Host $result -ForegroundColor Red
    }
} catch {
    Write-Host "Error running build: $_" -ForegroundColor Red
}