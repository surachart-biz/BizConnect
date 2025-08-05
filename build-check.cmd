@echo off
echo Building BizConnect.Dal...
dotnet build "BizConnect.Dal\BizConnect.Dal.csproj" --no-restore --verbosity minimal
if %ERRORLEVEL% neq 0 (
    echo Dal build failed with error level %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo.
echo Building BizConnect.Services...
dotnet build "BizConnect.Services\BizConnect.Services.csproj" --no-restore --verbosity minimal
if %ERRORLEVEL% neq 0 (
    echo Services build failed with error level %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo.
echo SUCCESS: Both Dal and Services compiled successfully!