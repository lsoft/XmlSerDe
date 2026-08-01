@echo off
rem ---------------------------------------------------------------------------
rem  Runs the XmlSerDe benchmarks and prints the result tables only.
rem
rem  Everything dotnet build and BenchmarkDotNet print - package restore, one
rem  generated exe per (class, job) pair, per-iteration progress - goes to the
rem  log file. The console gets only the markdown tables BenchmarkDotNet exports
rem  as *-report-github.md.
rem
rem  What is measured:
rem    SerializeFixture     - net10.0 only
rem    DeserializeFixture   - net472 + net8.0 + net10.0, categories DEEP/REGULAR
rem                           (net472 is how the netstandard2.0 assemblies run)
rem
rem  Takes several minutes. The full log is kept either way.
rem
rem  ASCII only on purpose: cmd.exe reads batch files in the OEM codepage, so
rem  non-ASCII comments here would be mangled into unparsable commands.
rem ---------------------------------------------------------------------------

setlocal

cd /d "%~dp0"

set "PROJECT=XmlSerDe.PerformanceTests\XmlSerDe.PerformanceTests.csproj"
set "HOSTEXE=XmlSerDe.PerformanceTests\bin\Release\net10.0\XmlSerDe.PerformanceTests.exe"
set "ARTIFACTS=%CD%\BenchmarkDotNet.Artifacts"
set "LOG=%CD%\benchmarks.log"

rem Remembered now, restored after the reports are printed - see :restorecp.
rem Split on the colon only and trim afterwards: chcp's message is localized and
rem its wording contains spaces, so a space delimiter would pick up a word.
for /f "tokens=2 delims=:" %%C in ('chcp') do for /f "tokens=*" %%D in ("%%C") do set "OLDCP=%%D"

rem Report file names are derived from class names, so a stale report left by a
rem renamed fixture would be printed below as if it came from this run.
if exist "%ARTIFACTS%" rd /s /q "%ARTIFACTS%"
if exist "%LOG%" del /q "%LOG%"

echo [1/2] Building Release: net472 + net8.0 + net10.0 ...
dotnet build -c Release "%PROJECT%" --nologo -v quiet >> "%LOG%" 2>&1
if errorlevel 1 goto :failed

echo [2/2] Running benchmarks, this takes several minutes ...
"%HOSTEXE%" >> "%LOG%" 2>&1
if errorlevel 1 goto :failed

if not exist "%ARTIFACTS%\results\*-report-github.md" goto :noresults

rem Not "type": the reports are UTF-8 and contain the micro sign, which the OEM
rem codepage turns into mojibake. They are also HTML-escaped by BenchmarkDotNet's
rem github exporter, so a method name shows up as &#39;Serialize: ...&#39;.
rem PowerShell reads them as UTF-8 and unescapes; chcp makes the console show it.
chcp 65001 > nul
powershell -NoProfile -Command "[Console]::OutputEncoding = [Text.Encoding]::UTF8; Get-ChildItem '%ARTIFACTS%\results' -Filter '*-report-github.md' | ForEach-Object { Write-Output ''; Write-Output ('=' * 79); Write-Output (' ' + $_.BaseName); Write-Output ('=' * 79); Write-Output ([System.Net.WebUtility]::HtmlDecode((Get-Content -Raw -Encoding UTF8 $_.FullName))) }"
call :restorecp

echo Full log: %LOG%
endlocal
exit /b 0

rem The console codepage is process-wide and survives this script, so put back
rem whatever the user had before.
:restorecp
if defined OLDCP chcp %OLDCP% > nul
goto :eof

:noresults
echo.
echo No report files were produced. Full log: %LOG%
endlocal
exit /b 1

:failed
echo.
echo BUILD OR RUN FAILED. Log tail:
echo -------------------------------------------------------------------------------
powershell -NoProfile -Command "Get-Content -Tail 40 '%LOG%'"
echo -------------------------------------------------------------------------------
echo Full log: %LOG%
endlocal
exit /b 1
