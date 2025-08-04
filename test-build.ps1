#!/usr/bin/env pwsh
# Simple test build script to verify compilation

Write-Host "Testing build after fixing test compilation errors..." -ForegroundColor Green

try {
    # Build the solution
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Build successful!" -ForegroundColor Green
        
        # Run a quick test to verify test compilation
        Write-Host "Testing controller tests compilation..." -ForegroundColor Yellow
        dotnet test --no-build --filter "FullyQualifiedName~AccountControllerTests" --dry-run
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ AccountController tests compile successfully!" -ForegroundColor Green
        } else {
            Write-Host "❌ AccountController tests have compilation issues" -ForegroundColor Red
            exit 1
        }
        
        dotnet test --no-build --filter "FullyQualifiedName~KBankControllerTests" --dry-run
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ KBankController tests compile successfully!" -ForegroundColor Green
        } else {
            Write-Host "❌ KBankController tests have compilation issues" -ForegroundColor Red
            exit 1
        }
        
        Write-Host "✅ All test compilation errors have been fixed!" -ForegroundColor Green
        
    } else {
        Write-Host "❌ Build failed" -ForegroundColor Red
        exit 1
    }
    
} catch {
    Write-Host "❌ Error during build: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}