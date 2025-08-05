#!/usr/bin/env pwsh

Write-Host "Building BizConnect.Dal..." -ForegroundColor Yellow
dotnet build "BizConnect.Dal\BizConnect.Dal.csproj" --verbosity normal

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ BizConnect.Dal compiled successfully!" -ForegroundColor Green
    
    Write-Host "`nVerifying EF models are accessible..." -ForegroundColor Yellow
    
    # Check if key model classes exist in the assembly
    try {
        $assembly = [System.Reflection.Assembly]::LoadFrom("BizConnect.Dal\bin\Debug\net8.0\BizConnect.Dal.dll")
        $types = $assembly.GetTypes() | Where-Object { $_.Namespace -eq "BizConnect.Dal.Models" }
        
        Write-Host "Found model types:" -ForegroundColor Green
        foreach ($type in $types) {
            Write-Host "  - $($type.Name)" -ForegroundColor Cyan
        }
        
        # Check specific models
        $branchType = $types | Where-Object { $_.Name -eq "Branch" }
        if ($branchType) {
            $properties = $branchType.GetProperties() | Where-Object { $_.Name -like "*Name*" }
            Write-Host "`nBranch multi-language properties:" -ForegroundColor Green
            foreach ($prop in $properties) {
                Write-Host "  - $($prop.Name): $($prop.PropertyType.Name)" -ForegroundColor Cyan
            }
        }
    }
    catch {
        Write-Host "Could not load assembly for verification: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ BizConnect.Dal compilation failed" -ForegroundColor Red
    exit 1
}