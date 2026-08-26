@echo off
setlocal EnableExtensions
cd /d "%~dp0"

title MadXka Discord Bot (Windows VPS)

set "REPO_URL=https://github.com/thugshakerm/Lo.git"
set "BRANCH=arena/01a03fb3-lo"
set "REPO_DIR=madxka-bot"
set "PROXY_DEFAULT=https://ma-ly00.onrender.com"

echo ==================================================
echo   MadXka Discord Bot  -  setup + run  (Windows)
echo ==================================================
echo.

REM ---------- find python ----------
set "PY="
where python >nul 2>nul && set "PY=python"
if not defined PY where py >nul 2>nul && set "PY=py -3"
if not defined PY (
    echo [ERROR] Python was not found on this machine.
    echo         Install it from https://www.python.org/downloads/
    echo         and tick "Add python.exe to PATH" during setup.
    echo.
    pause
    exit /b 1
)
echo Using python: %PY%
echo.

REM ---------- git clone / update ----------
if not exist "%REPO_DIR%\.git" (
    where git >nul 2>nul
    if errorlevel 1 (
        echo [ERROR] Git was not found. Install it with:
        echo         winget install Git.Git
        echo.
        pause
        exit /b 1
    )
    echo [1/3] Cloning the bot (branch %BRANCH%)...
    git clone --branch %BRANCH% %REPO_URL% %REPO_DIR%
    if errorlevel 1 (
        echo [ERROR] git clone failed. Check your internet connection.
        pause
        exit /b 1
    )
) else (
    echo [1/3] Bot already cloned - updating to latest...
    pushd "%REPO_DIR%"
    git fetch origin %BRANCH%
    git checkout %BRANCH%
    git pull
    popd
)
cd /d "%~dp0\%REPO_DIR%"
echo.

REM ---------- python env ----------
if not exist ".venv\Scripts\python.exe" (
    echo [2/3] Creating the Python environment (first run only)...
    %PY% -m venv .venv
)
echo Installing/updating dependencies...
".venv\Scripts\python.exe" -m pip install --quiet --upgrade pip
".venv\Scripts\python.exe" -m pip install --quiet -r requirements.txt
if errorlevel 1 (
    echo [ERROR] dependency install failed.
    pause
    exit /b 1
)
echo.

REM ---------- config (.env) ----------
if exist ".env" (
    set "KEEP="
    set /p KEEP="A .env already exists. Keep the saved settings? [Y/n]: "
    if /i not "%KEEP%"=="n" goto runbot
)

echo Get the token at https://discord.com/developers
echo   (your application - Bot tab - Reset Token)
echo.
set "TOKEN="
set /p TOKEN="Discord bot token: "
if "%TOKEN%"=="" (
    echo [ERROR] The bot token is required.
    pause
    exit /b 1
)
set "PROXY="
set /p PROXY="MadXka proxy URL [%PROXY_DEFAULT%]: "
if "%PROXY%"=="" set "PROXY=%PROXY_DEFAULT%"
set "GUILD="
set /p GUILD="Server ID for instant command sync [blank = global sync]: "

> ".env" (
    echo DISCORD_TOKEN=%TOKEN%
    echo MADXKA_BASE_URL=%PROXY%
    echo DISCORD_GUILD_ID=%GUILD%
)
echo.
echo .env written.

:runbot
echo [3/3] Starting the bot...
echo     close this window to stop it (or use Task Scheduler / NSSM to keep it running)
echo.
".venv\Scripts\python.exe" bot.py
echo.
echo Bot stopped. Press any key to close.
pause >nul
endlocal
