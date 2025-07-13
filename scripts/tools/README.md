# BizConnect Development Tools Directory

This directory stores auto-downloaded development tools to provide a seamless developer experience.

## Contents

- **jq.exe** (Windows only) - Portable JSON processor automatically downloaded by `update-db.sh` when running on Windows Git Bash/MSYS2/Cygwin environments.

## Purpose

The BizConnect database migration workflow requires `jq` for parsing JSON configuration files. To eliminate friction for developers using Git Bash on Windows, the `update-db.sh` script automatically downloads a portable version of `jq.exe` to this directory when needed.

## Git Ignore

This entire directory is git-ignored to prevent committing auto-downloaded binaries to the repository. Only this README file is tracked.

## Platform Support

- **Windows (Git Bash/MSYS2/Cygwin)**: Auto-downloads `jq.exe` 
- **macOS/Linux**: Requires system installation of `jq` via package manager

## Manual Cleanup

To clean up auto-downloaded tools:
```bash
rm -rf scripts/tools/jq.exe
```

The tools will be re-downloaded automatically on the next script run if needed.
