#!/bin/bash

# Bash script to update Service Worker version
# This script helps test the Service Worker update flow

SERVICE_WORKER_PATH="wwwroot/service-worker.js"
NEW_VERSION=""
AUTO_INCREMENT=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--version)
            NEW_VERSION="$2"
            shift 2
            ;;
        -a|--auto-increment)
            AUTO_INCREMENT=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  -v, --version VERSION    Set specific version"
            echo "  -a, --auto-increment     Auto-increment patch version"
            echo "  -h, --help              Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Check if service worker file exists
if [[ ! -f "$SERVICE_WORKER_PATH" ]]; then
    echo "Error: Service Worker file not found at: $SERVICE_WORKER_PATH"
    exit 1
fi

# Extract current version
CURRENT_VERSION=$(grep -o "const CACHE_VERSION = '[^']*'" "$SERVICE_WORKER_PATH" | sed "s/const CACHE_VERSION = '//;s/'//")

if [[ -z "$CURRENT_VERSION" ]]; then
    echo "Error: Could not find CACHE_VERSION in service worker file"
    exit 1
fi

echo "Current version: $CURRENT_VERSION"

# Determine new version
if [[ "$AUTO_INCREMENT" == true ]]; then
    # Try to parse as semantic version and increment patch
    if [[ $CURRENT_VERSION =~ ^bizconnect-v([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
        MAJOR="${BASH_REMATCH[1]}"
        MINOR="${BASH_REMATCH[2]}"
        PATCH=$((${BASH_REMATCH[3]} + 1))
        NEW_VERSION="bizconnect-v$MAJOR.$MINOR.$PATCH"
    else
        # Fallback: append timestamp
        TIMESTAMP=$(date +"%Y%m%d-%H%M%S")
        NEW_VERSION="$CURRENT_VERSION-$TIMESTAMP"
    fi
elif [[ -z "$NEW_VERSION" ]]; then
    # Prompt for new version
    read -p "Enter new version (current: $CURRENT_VERSION): " NEW_VERSION
    if [[ -z "$NEW_VERSION" ]]; then
        echo "No version provided. Exiting."
        exit 1
    fi
fi

echo "New version: $NEW_VERSION"

# Create backup
cp "$SERVICE_WORKER_PATH" "$SERVICE_WORKER_PATH.backup"

# Update the service worker file
sed -i.tmp "s/const CACHE_VERSION = '[^']*'/const CACHE_VERSION = '$NEW_VERSION'/" "$SERVICE_WORKER_PATH"

# Remove temporary file (macOS compatibility)
rm -f "$SERVICE_WORKER_PATH.tmp"

echo "Service Worker version updated successfully!"
echo ""
echo "Next steps:"
echo "1. Build and deploy your application"
echo "2. Visit your site to test the update flow"
echo "3. Check browser DevTools > Application > Service Workers"
echo "4. Use the test page at /sw-test.html to verify functionality"
echo ""
echo "The Service Worker will:"
echo "- Automatically install the new version"
echo "- Show an update notification to users"
echo "- Reload the page when users accept the update"
echo ""
echo "Backup created at: $SERVICE_WORKER_PATH.backup"
