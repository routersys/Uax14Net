@echo off
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

set "UNICODE_VERSION=17.0.0"
set "BASE=https://www.unicode.org/Public/%UNICODE_VERSION%/ucd"
set "OUTPUT_DIR=%~1"
if "%OUTPUT_DIR%"=="" set "OUTPUT_DIR=data"
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

call :fetch "%BASE%/extracted/DerivedLineBreak.txt"       DerivedLineBreak.txt       "DerivedLineBreak-%UNICODE_VERSION%.txt"       || exit /b 1
call :fetch "%BASE%/EastAsianWidth.txt"                   EastAsianWidth.txt         "EastAsianWidth-%UNICODE_VERSION%.txt"         || exit /b 1
call :fetch "%BASE%/emoji/emoji-data.txt"                 emoji-data.txt             "Version: 17.0"                                || exit /b 1
call :fetch "%BASE%/extracted/DerivedGeneralCategory.txt" DerivedGeneralCategory.txt "DerivedGeneralCategory-%UNICODE_VERSION%.txt" || exit /b 1
call :fetch "%BASE%/auxiliary/LineBreakTest.txt"          LineBreakTest.txt          "LineBreakTest-%UNICODE_VERSION%.txt"          || exit /b 1

echo reference data for Unicode %UNICODE_VERSION% written to %OUTPUT_DIR%
endlocal
exit /b 0

:fetch
set "URL=%~1"
set "OUT=%~2"
set "TOKEN=%~3"
curl -fsSL -o "%OUTPUT_DIR%\%OUT%" "%URL%" || exit /b 1
findstr /c:"%TOKEN%" "%OUTPUT_DIR%\%OUT%" >nul || (
  echo version mismatch in %OUT%: expected header containing "%TOKEN%"
  exit /b 1
)
exit /b 0
