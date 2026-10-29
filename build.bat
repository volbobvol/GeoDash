@echo off
setlocal
where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET 10 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/10.0
    exit /b 1
)
dotnet --list-sdks | findstr /r "." >nul
if errorlevel 1 (
    echo No .NET SDK is installed. Install the .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0
    exit /b 1
)
dotnet build "%~dp0GeoDashSequencer.sln" -c Release
exit /b %errorlevel%