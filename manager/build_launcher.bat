@echo off
rem Builds RescueOpsModLauncher.exe (one file) with the C# compiler that comes with Windows (.NET Framework 4.8).
rem Embeds the WebView2 SDK DLLs and every file in ui\ (as resources named ui/<file>).
setlocal EnableDelayedExpansion
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "WV=lib\webview2"
set "UI="
for %%f in (ui\*) do set "UI=!UI! /resource:ui\%%~nxf,ui/%%~nxf"
"%CSC%" /nologo /codepage:65001 /optimize+ /target:winexe /platform:x64 ^
  /out:RescueOpsModLauncher.exe /win32icon:icon.ico /win32manifest:app.manifest ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ^
  /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ^
  /r:%WV%\Microsoft.Web.WebView2.Core.dll /r:%WV%\Microsoft.Web.WebView2.WinForms.dll ^
  /resource:%WV%\Microsoft.Web.WebView2.Core.dll,Microsoft.Web.WebView2.Core.dll ^
  /resource:%WV%\Microsoft.Web.WebView2.WinForms.dll,Microsoft.Web.WebView2.WinForms.dll ^
  /resource:%WV%\WebView2Loader.dll,WebView2Loader.dll ^
  !UI! ^
  Launcher.cs LauncherForm.cs ModDev.cs Core.cs Services.cs
exit /b %errorlevel%
