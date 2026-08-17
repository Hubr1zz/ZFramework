@echo off
setlocal EnableDelayedExpansion

set REPO_URL=https://github.com/Hubr1zz/ZFramework.git
set REPO_REF=Project
set TEMP_DIR=%TEMP%\ZFramework_tmp_%RANDOM%
:: 脚本所在目录即工程根目录
set PROJECT_DIR=%~dp0

echo ========================================
echo  ZFramework Core Updater
echo ========================================
echo.
echo Source : %REPO_URL%
echo Target : %PROJECT_DIR%
echo.

:: ── 路径合法性检查 ────────────────────────────────
echo [0/4] Checking project layout ...

:: 必须存在 UnityProject 子目录
if not exist "%PROJECT_DIR%UnityProject\" (
    echo [ERROR] 'UnityProject' not found under %PROJECT_DIR%
    echo         Please run this script from the TEngine project root.
    goto :fail
)

:: 必须存在目标 YooAsset 目录
if not exist "%PROJECT_DIR%UnityProject\Packages\YooAsset\" (
    echo [ERROR] 'UnityProject\Packages\YooAsset' not found.
    echo         Unexpected project structure.
    goto :fail
)

:: 必须存在目标 TEngine 目录
if not exist "%PROJECT_DIR%UnityProject\Assets\TEngine\" (
    echo [ERROR] 'UnityProject\Assets\TEngine' not found.
    echo         Unexpected project structure.
    goto :fail
)

:: 当前版本的 RTS/Roslyn 包必须存在
if not exist "%PROJECT_DIR%UnityProject\Packages\com.tengine.rts\" (
    echo [ERROR] 'UnityProject\Packages\com.tengine.rts' not found.
    echo         This updater targets the ZFramework Project branch.
    goto :fail
)

:: git 可用性检查
where git >nul 2>&1
if errorlevel 1 (
    echo [ERROR] 'git' not found in PATH. Please install Git for Windows.
    goto :fail
)

echo [OK] Project layout verified.
echo.

:: ZFramework Project 分支必须先发布到远端；禁止静默使用旧 main
git ls-remote --exit-code --heads "%REPO_URL%" "%REPO_REF%" >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Remote branch '%REPO_REF%' was not found at %REPO_URL%.
    echo         Publish the current ZFramework Project branch before using this updater.
    goto :fail
)

:: ── 稀疏克隆，只拉取指定目录 ──────────────────────
echo [1/3] Cloning (sparse) ...
git clone --no-checkout --depth=1 --filter=blob:none --branch "%REPO_REF%" "%REPO_URL%" "%TEMP_DIR%"
if errorlevel 1 (
    echo [ERROR] git clone failed.
    goto :cleanup
)

cd /d "%TEMP_DIR%"

git sparse-checkout init --cone
git sparse-checkout set UnityProject/Packages/YooAsset UnityProject/Packages/com.tengine.rts UnityProject/Assets/TEngine
git checkout
if errorlevel 1 (
    echo [ERROR] git checkout failed.
    goto :cleanup
)

echo.
echo [2/3] Syncing UnityProject\Packages\YooAsset ...
robocopy "%TEMP_DIR%\UnityProject\Packages\YooAsset" ^
         "%PROJECT_DIR%UnityProject\Packages\YooAsset" ^
         /E /NFL /NDL /NJH /NJS /NC /NS
if errorlevel 8 (
    echo [ERROR] robocopy failed for YooAsset.
    goto :cleanup
)

echo.
echo [3/3] Syncing UnityProject\Assets\TEngine ...
robocopy "%TEMP_DIR%\UnityProject\Assets\TEngine" ^
         "%PROJECT_DIR%UnityProject\Assets\TEngine" ^
         /E /NFL /NDL /NJH /NJS /NC /NS
if errorlevel 8 (
    echo [ERROR] robocopy failed for TEngine.
    goto :cleanup
)

echo.
echo [4/4] Syncing UnityProject\Packages\com.tengine.rts ...
robocopy "%TEMP_DIR%\UnityProject\Packages\com.tengine.rts" ^
         "%PROJECT_DIR%UnityProject\Packages\com.tengine.rts" ^
         /E /NFL /NDL /NJH /NJS /NC /NS
if errorlevel 8 (
    echo [ERROR] robocopy failed for com.tengine.rts.
    goto :cleanup
)

echo.
echo [OK] Update complete!

:cleanup
cd /d "%PROJECT_DIR%"
if exist "%TEMP_DIR%" rmdir /S /Q "%TEMP_DIR%"
goto :done

:fail
echo.
echo [ABORTED] No changes were made.

:done
echo.
pause
