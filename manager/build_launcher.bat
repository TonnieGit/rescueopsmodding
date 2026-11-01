@echo off
rem Builds RescueOpsModLauncher.exe (one file) with the C# compiler that comes with Windows (.NET Framework 4.8).
rem The WebView2 SDK DLLs and the UI are embedded as resources.
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "WV=lib\webview2"
"%CSC%" /nologo /codepage:65001 /optimize+ /target:winexe /platform:x64 ^
  /out:RescueOpsModLauncher.exe /win32icon:icon.ico /win32manifest:app.manifest ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ^
  /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ^
  /r:%WV%\Microsoft.Web.WebView2.Core.dll /r:%WV%\Microsoft.Web.WebView2.WinForms.dll ^
  /resource:%WV%\Microsoft.Web.WebView2.Core.dll,Microsoft.Web.WebView2.Core.dll ^
  /resource:%WV%\Microsoft.Web.WebView2.WinForms.dll,Microsoft.Web.WebView2.WinForms.dll ^
  /resource:%WV%\WebView2Loader.dll,WebView2Loader.dll ^
  /resource:ui\index.html,index.html ^
  Launcher.cs LauncherForm.cs
exit /b %errorlevel%
