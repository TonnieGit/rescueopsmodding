@echo off
rem Builds RescueOpsModManager.exe with the C# compiler that comes with Windows (.NET Framework 4.8).
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
"%CSC%" /nologo /codepage:65001 /optimize+ /target:winexe /platform:anycpu ^
  /out:RescueOpsModManager.exe /win32icon:icon.ico /win32manifest:app.manifest ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ^
  /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ^
  ModManager.cs
exit /b %errorlevel%
