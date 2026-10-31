@echo off
setlocal

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [build] csc.exe not found
  exit /b 1
)

if not exist "dist" mkdir "dist"

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 ^
  /win32manifest:launcher.manifest ^
  /out:dist\ZZZSwitcher.exe ^
  /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  src\Log.cs ^
  src\IniFile.cs ^
  src\ChannelProfile.cs ^
  src\GameFolder.cs ^
  src\ChannelSdk.cs ^
  src\LauncherMap.cs ^
  src\Switcher.cs ^
  src\Theme.cs ^
  src\Ui.cs ^
  src\Program.cs

if errorlevel 1 (
  echo [build] FAILED
  exit /b 1
)

echo [build] OK -^> dist\ZZZSwitcher.exe
endlocal
