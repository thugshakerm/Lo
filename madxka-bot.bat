@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title MadXka Discord Bot

echo ==================================================
echo   MadXka Discord Bot - setup + run  (Windows VPS)
echo ==================================================
echo.

set "REPO_URL=https://github.com/thugshakerm/Lo.git"
set "BRANCH=arena/01a03fb3-lo"
set "REPO_DIR=madxka-bot"
set "PROXY_DEFAULT=https://ma-ly00.onrender.com"

REM ============ find python ============
set "PY="
where python >nul 2>nul
if errorlevel 1 goto try_py
set "PY=python"
goto have_python
:try_py
where py >nul 2>nul
if errorlevel 1 goto no_python
set "PY=py -3"
:have_python
echo Using python: %PY%
echo.

REM ============ get the code ============
if exist "bot.py" goto in_repo
if exist "%REPO_DIR%\.git" goto update_repo
where git >nul 2>nul
if errorlevel 1 goto no_git
echo [1/3] Cloning the bot ^(branch %BRANCH%^)...
git clone --branch %BRANCH% %REPO_URL% %REPO_DIR%
if errorlevel 1 goto clone_failed
goto repo_ready
:in_repo
set "REPO_DIR=."
goto update_repo
:update_repo
echo [1/3] Updating the bot to the latest...
where git >nul 2>nul
if errorlevel 1 goto update_skip
pushd "%REPO_DIR%"
git pull
set "PULL_ERR=%errorlevel%"
popd
if not "%PULL_ERR%"=="0" echo (update failed - continuing with what is here)
goto update_done
:update_skip
echo (git not found - skipping the update, using what is here)
:update_done
:repo_ready
cd /d "%~dp0\%REPO_DIR%"
echo.

REM ============ python env ============
if exist ".venv\Scripts\python.exe" goto deps
echo [2/3] Creating the Python environment (first run only)...
%PY% -m venv .venv
if errorlevel 1 goto venv_failed
:deps
echo Installing/updating dependencies...
".venv\Scripts\python.exe" -m pip install --quiet --upgrade pip
".venv\Scripts\python.exe" -m pip install --quiet -r requirements.txt
if errorlevel 1 goto deps_failed
echo.
goto config

REM ============ config (.env) ============
:config
if not exist ".env" goto ask_config
set "KEEP="
set /p KEEP="A .env already exists. Keep the saved settings? [Y/n]: "
if /i "%KEEP%"=="n" goto ask_config
goto runbot
:ask_config
echo Get the token at https://discord.com/developers
echo   (your application - Bot tab - Reset Token)
echo.
set "TOKEN="
set /p TOKEN="Discord bot token: "
if "%TOKEN%"=="" goto token_failed
set "PROXY="
set /p PROXY="MadXka proxy URL [%PROXY_DEFAULT%]: "
if "%PROXY%"=="" set "PROXY=%PROXY_DEFAULT%"
set "GUILD="
set /p GUILD="Server ID for instant command sync [blank = global sync]: "
echo DISCORD_TOKEN=%TOKEN%> .env
echo MADXKA_BASE_URL=%PROXY%>> .env
echo DISCORD_GUILD_ID=%GUILD%>> .env
echo.
echo .env written.
goto runbot

REM ============ run ============
:runbot
echo [3/3] Starting the bot...
echo     close this window to stop it (or use Task Scheduler / NSSM to keep it running)
echo.
".venv\Scripts\python.exe" bot.py
echo.
echo Bot stopped.
goto done

REM ============ exits (always pauses, window never closes on its own) ============
:no_python
echo [ERROR] Python was not found on this machine.
echo         Install it from https://www.python.org/downloads/
echo         and tick "Add python.exe to PATH" during setup.
goto fail
:no_git
echo [ERROR] Git was not found. Install it with:
echo         winget install Git.Git
goto fail
:clone_failed
echo [ERROR] git clone failed. Check your internet connection.
goto fail
:venv_failed
echo [ERROR] could not create the Python environment.
goto fail
:deps_failed
echo [ERROR] dependency install failed.
goto fail
:token_failed
echo [ERROR] The bot token is required.
goto fail
:fail
echo.
echo Setup did not finish - read the error above.
echo Press any key to close this window.
pause >nul
endlocal & exit /b 1
:done
echo Press any key to close this window.
pause >nul
endlocal & exit /b 0
