@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Could not find csc.exe.
    pause
    exit /b 1
)
"%CSC%" /nologo /target:winexe /codepage:65001 /out:"%~dp0MouseStudio.exe" /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "%~dp0MouseStudio.cs" "%~dp0MainForm.cs"
if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
)
echo Build succeeded: %~dp0MouseStudio.exe
pause
