@echo off
echo Cleaning up temporary test files...
if exist test_compilation.cs del test_compilation.cs
if exist test-enhanced-security.bat del test-enhanced-security.bat
if exist cleanup.bat del cleanup.bat
echo Cleanup complete.