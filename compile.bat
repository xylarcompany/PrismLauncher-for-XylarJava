@echo off
REM ==================================================================
REM PrismLauncher for XylarJava - Compilation Script
REM ==================================================================

echo.
echo ========================================
echo Prism Launcher for XylarJava Compilation
echo ========================================
echo.

REM Check if Python is installed
where python >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Python non trovato! Installa Python da https://www.python.org/
    pause
    exit /b 1
)

REM Check if CMake is installed
where cmake >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: CMake non trovato! Installa CMake da https://cmake.org/download/
    pause
    exit /b 1
)

REM Check if Visual Studio is installed
if not exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Tools\MSVC" (
    echo ERROR: Visual Studio 2022 non trovato!
    echo Scarica da: https://visualstudio.microsoft.com/downloads/
    pause
    exit /b 1
)

echo [1/4] Installando aqtinstall per scaricare Qt6...
python -m pip install aqtinstall --quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Impossibile installare aqtinstall
    pause
    exit /b 1
)

echo [2/4] Scaricando Qt 6.4.0 per Windows...
python -m aqt install-qt windows desktop 6.4.0 win64_msvc2022_64 --outputdir C:/Qt
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Impossibile scaricare Qt6
    echo Prova a scaricare manualmente da: https://www.qt.io/download
    pause
    exit /b 1
)

echo [3/4] Configurando CMake...
cd /d "%~dp0"
if exist build rmdir /s /q build
mkdir build
cd build

cmake -G "Visual Studio 17 2022" -A x64 ^
    -DCMAKE_BUILD_TYPE=Release ^
    -DCMAKE_TOOLCHAIN_FILE="C:/vcpkg/scripts/buildsystems/vcpkg.cmake" ^
    -DVCPKG_TARGET_TRIPLET=x64-windows ^
    -DQt6_DIR="C:/Qt/6.4.0/msvc2022_64/lib/cmake/Qt6" ^
    ..

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: CMake configuration failed
    pause
    exit /b 1
)

echo [4/4] Compilando in Release mode...
cmake --build . --config Release --parallel 4

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Compilation failed
    pause
    exit /b 1
)

echo.
echo ========================================
echo COMPILAZIONE COMPLETATA CON SUCCESSO!
echo ========================================
echo.
echo L'exe si trova in:
echo %~dp0build\launcher\Release\PrismLauncher.exe
echo.
pause
