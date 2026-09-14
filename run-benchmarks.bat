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
rem    SerializeFixture       - net10.0 only
rem    DeserializeFixture     - net472 + net8.0 + net10.0, categories DEEP/REGULAR
rem    HugeSerializeFixture   - net10.0 only, ~100 MB mixed document
rem    HugeDeserializeFixture - net472 + net8.0 + net10.0, same 100 MB document
rem                           (net472 is how the netstandard2.0 assemblies run)
rem
rem  On a hybrid CPU the run is pinned to the performance cores - see the
rem  affinity block below and eng\get-pcore-affinity.ps1.
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

rem A hybrid CPU (Intel P/E cores, Snapdragon X, Zen 5c) lets the scheduler move
rem a benchmark from a P core to an E core in the middle of an iteration, and that
rem is a several-fold difference, not noise within StdDev - BenchmarkDotNet only
rem reports it as a wide spread. eng\get-pcore-affinity.ps1 asks Windows for the
rem efficiency class of every physical core and prints the mask of the fastest
rem ones; on a CPU whose cores are all equal it prints nothing and the run stays
rem unrestricted. Its own diagnostics go to the log.
rem
rem Set XMLSERDE_BENCH_AFFINITY to a hex mask to pin the run by hand, or to "off"
rem to run on every core.
set "AFFINITY="
if defined XMLSERDE_BENCH_AFFINITY (
    if /i not "%XMLSERDE_BENCH_AFFINITY%"=="off" set "AFFINITY=%XMLSERDE_BENCH_AFFINITY%"
) else (
    for /f "usebackq tokens=1" %%M in (`powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0eng\get-pcore-affinity.ps1" 2^>^>"%LOG%"`) do set "AFFINITY=%%M"
)

rem Whatever powershell.exe writes to stdout ends up here, and that is not always
rem the mask: a missing script file, for one, makes it print its logo instead. A
rem non-mask would be handed to start below, which would refuse to launch anything.
if defined AFFINITY call :checkmask

if defined AFFINITY (
    echo Affinity: 0x%AFFINITY% - the run is pinned to these CPUs.
) else (
    echo Affinity: not restricted.
)

echo [1/2] Building Release: net472 + net8.0 + net10.0 ...
dotnet build -c Release "%PROJECT%" --nologo -v quiet >> "%LOG%" 2>&1
if errorlevel 1 goto :failed

rem The mask goes onto the host process rather than into BenchmarkDotNet's own
rem --affinity, for two reasons. Windows hands a process affinity down to every
rem child it starts, so the one mask covers the per-job exe BenchmarkDotNet
rem generates, builds and runs - and it is in place BEFORE the runtime of those
rem children starts, so the GC sizes its heaps for the cores they will actually
rem get. --affinity is applied to an already running process and is parsed as a
rem signed 32-bit number, so it cannot even express cores above CPU 30.
rem
rem /b keeps the child in this console, so the redirection below still reaches it,
rem and /wait is what makes errorlevel below mean the benchmark run.
if defined AFFINITY (
    echo [2/2] Running benchmarks on the performance cores, this takes several minutes ...
    start "XmlSerDe benchmarks" /affinity %AFFINITY% /b /wait "%HOSTEXE%" >> "%LOG%" 2>&1
) else (
    echo [2/2] Running benchmarks, this takes several minutes ...
    "%HOSTEXE%" >> "%LOG%" 2>&1
)
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

rem No space before the pipe: it would be echoed as part of the mask.
:checkmask
if /i "%AFFINITY:~0,2%"=="0x" set "AFFINITY=%AFFINITY:~2%"
echo %AFFINITY%| findstr /r /i /c:"^[0-9a-f][0-9a-f]*$" > nul
if errorlevel 1 (
    echo Affinity: "%AFFINITY%" is not a hex mask, ignoring it.
    set "AFFINITY="
)
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
