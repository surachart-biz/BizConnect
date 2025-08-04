@echo off
echo Testing Enhanced Security Audit Service compilation...
cd /d "D:\workspace\Code\BizConnect"

echo.
echo Building BizConnect.Services project...
"C:\Program Files\dotnet\dotnet.exe" build BizConnect.Services\BizConnect.Services.csproj --no-restore --verbosity quiet

if %errorlevel% equ 0 (
    echo SUCCESS: Enhanced Security Audit Service compiled successfully!
    echo All collection type conversion issues have been fixed.
) else (
    echo FAILURE: Compilation errors detected.
    echo Check the specific errors above.
)

echo.
echo Cleaning up test file...
if exist test_compilation.cs del test_compilation.cs

pause