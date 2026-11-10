@echo off
rem Starts the whole SMS Management system with one command: run (or run.cmd), from the repository root.
rem
rem   1. Uses the globally installed .NET 10 SDK, if there is one.
rem   2. Otherwise uses a private .NET 10 SDK in .dotnet\ inside the repository. On the first run it is downloaded
rem      with Microsoft's official dotnet-install.ps1: no admin rights, nothing installed system-wide, and the
rem      machine's existing .NET versions are not touched.
rem   3. Runs: dotnet run --project src\SmsManagement.Api   (dashboard on http://localhost:5080)

setlocal
cd /d "%~dp0"

set "SDK_CHANNEL=10.0"
set "LOCAL_DOTNET=%~dp0.dotnet"
set "DOTNET_NOLOGO=1"

call :has_sdk dotnet
if not errorlevel 1 (
    echo Using the installed .NET %SDK_CHANNEL% SDK.
    set "DOTNET=dotnet"
    goto :run
)

call :has_sdk "%LOCAL_DOTNET%\dotnet.exe"
if errorlevel 1 (
    call :install_sdk || goto :install_failed
)

rem Outside any ( ) block on purpose: PATH can contain "(x86)", which would end the block early.
set "DOTNET_ROOT=%LOCAL_DOTNET%"
set "PATH=%LOCAL_DOTNET%;%PATH%"
set "DOTNET=%LOCAL_DOTNET%\dotnet.exe"
echo Using the local .NET %SDK_CHANNEL% SDK in .dotnet\

:run
echo Starting the SMS Management system. Dashboard: http://localhost:5080 - press Ctrl+C to stop.
"%DOTNET%" run --project src\SmsManagement.Api
exit /b %ERRORLEVEL%

rem ---------------------------------------------------------------- helpers

rem Succeeds if the given dotnet executable has a .NET 10.0 SDK.
:has_sdk
rem A missing program on the left of a pipe aborts the whole batch file, so check that it exists first.
if "%~1"=="dotnet" (
    where dotnet >nul 2>nul || exit /b 1
) else (
    if not exist "%~1" exit /b 1
)
"%~1" --list-sdks 2>nul | findstr /b /c:"%SDK_CHANNEL%." >nul
exit /b %ERRORLEVEL%

:install_sdk
echo The .NET %SDK_CHANNEL% SDK was not found. Downloading it into .dotnet\ - first run only, about 300 MB...
if not exist "%LOCAL_DOTNET%" mkdir "%LOCAL_DOTNET%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue'; [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1' -OutFile (Join-Path $env:LOCAL_DOTNET 'dotnet-install.ps1')" || exit /b 1
powershell -NoProfile -ExecutionPolicy Bypass -File "%LOCAL_DOTNET%\dotnet-install.ps1" -Channel %SDK_CHANNEL% -InstallDir "%LOCAL_DOTNET%" -NoPath || exit /b 1
call :has_sdk "%LOCAL_DOTNET%\dotnet.exe"
exit /b %ERRORLEVEL%

:install_failed
echo.
echo Could not download the .NET %SDK_CHANNEL% SDK. Check the internet connection, or install the SDK from
echo https://dotnet.microsoft.com/download/dotnet/10.0 and run again.
exit /b 1
