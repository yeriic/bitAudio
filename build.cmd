@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist "%CSC%" (
  echo No se encontro el compilador de .NET Framework 4.8.
  exit /b 1
)

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:bitAudio.exe ^
 /win32manifest:src\app.manifest ^
 /reference:System.dll ^
 /reference:System.Core.dll ^
 /reference:System.Drawing.dll ^
 /reference:System.Windows.Forms.dll ^
 /reference:netstandard.dll ^
 /reference:lib\NAudio.Core.dll ^
 /reference:lib\NAudio.Wasapi.dll ^
 /resource:lib\NAudio.Core.dll,bitAudio.Resources.NAudio.Core.dll ^
 /resource:lib\NAudio.Wasapi.dll,bitAudio.Resources.NAudio.Wasapi.dll ^
 src\Program.cs ^
 src\MainForm.cs ^
 src\StartupManager.cs ^
 src\Audio\AudioDeviceInfo.cs ^
 src\Audio\AudioEngine.cs ^
 src\Audio\GainLimiterSampleProvider.cs ^
 src\Audio\ChannelMapperSampleProvider.cs ^
 src\Audio\ContinuousSampleProvider.cs ^
 src\Controls\SlimSlider.cs

if errorlevel 1 (
  echo.
  echo Compilacion fallida.
  exit /b 1
)

echo.
echo Listo: bitAudio.exe
endlocal
