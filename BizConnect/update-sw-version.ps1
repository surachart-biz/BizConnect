# PowerShell script to update Service Worker version
# This script helps test the Service Worker update flow

param(
    [Parameter(Mandatory=$false)]
    [string]$NewVersion = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$AutoIncrement = $false
)

$ServiceWorkerPath = "wwwroot/service-worker.js"

if (-not (Test-Path $ServiceWorkerPath)) {
    Write-Error "Service Worker file not found at: $ServiceWorkerPath"
    exit 1
}

# Read current service worker content
$content = Get-Content $ServiceWorkerPath -Raw

# Extract current version
$versionPattern = "const CACHE_VERSION = '([^']+)';"
$match = [regex]::Match($content, $versionPattern)

if (-not $match.Success) {
    Write-Error "Could not find CACHE_VERSION in service worker file"
    exit 1
}

$currentVersion = $match.Groups[1].Value
Write-Host "Current version: $currentVersion" -ForegroundColor Yellow

# Determine new version
if ($AutoIncrement) {
    # Try to parse as semantic version and increment patch
    if ($currentVersion -match '^bizconnect-v(\d+)\.(\d+)\.(\d+)$') {
        $major = [int]$matches[1]
        $minor = [int]$matches[2]
        $patch = [int]$matches[3] + 1
        $NewVersion = "bizconnect-v$major.$minor.$patch"
    } else {
        # Fallback: append timestamp
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $NewVersion = "$currentVersion-$timestamp"
    }
} elseif ([string]::IsNullOrEmpty($NewVersion)) {
    # Prompt for new version
    $NewVersion = Read-Host "Enter new version (current: $currentVersion)"
    if ([string]::IsNullOrEmpty($NewVersion)) {
        Write-Host "No version provided. Exiting." -ForegroundColor Red
        exit 1
    }
}

Write-Host "New version: $NewVersion" -ForegroundColor Green

# Update the service worker file
$newContent = $content -replace $versionPattern, "const CACHE_VERSION = '$NewVersion';"

# Write back to file
Set-Content -Path $ServiceWorkerPath -Value $newContent -NoNewline

Write-Host "Service Worker version updated successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Build and deploy your application"
Write-Host "2. Visit your site to test the update flow"
Write-Host "3. Check browser DevTools > Application > Service Workers"
Write-Host "4. Use the test page at /sw-test.html to verify functionality"
Write-Host ""
Write-Host "The Service Worker will:" -ForegroundColor Yellow
Write-Host "- Automatically install the new version"
Write-Host "- Show an update notification to users"
Write-Host "- Reload the page when users accept the update"
