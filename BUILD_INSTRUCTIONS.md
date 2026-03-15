# Compilazione Prism Launcher for XylarJava

Se GitHub Actions non riesce a compilare, puoi compilare localmente seguendo questi step.

## Prerequisiti

1. **Visual Studio 2022** (Community, Professional, o Enterprise)
   - Download: https://visualstudio.microsoft.com/downloads/
   - Durante l'installazione, seleziona: "Desktop development with C++"

2. **CMake 3.22+**
   - Download: https://cmake.org/download/
   - Installa ed assicurati sia nel PATH

3. **VCPKG** (dovrebbe essere già installato in `C:\vcpkg\`)
   - Se non presente, clona da: https://github.com/Microsoft/vcpkg

4. **Python 3.8+**
   - Download: https://www.python.org/downloads/
   - Assicurati di aggiungere Python al PATH durante l'installazione

## Compilazione Automatica (Consigliato)

Esegui lo script batch:

```cmd
cd "C:\Users\MarioSyri\Desktop\PrismLauncherforXylarJava-develop\PrismLauncherforXylarJava-develop"
compile.bat
```

Lo script farà automaticamente:
1. ✓ Installa aqtinstall (scarica Qt6)
2. ✓ Scarica Qt 6.4.0 per Windows MSVC 2022
3. ✓ Configura CMake
4. ✓ Compila in modalità Release
5. ✓ Crea l'exe

## Compilazione Manuale

Se preferisci compilare step-by-step:

### Step 1: Scarica Qt6

```cmd
python -m pip install aqtinstall
python -m aqt install-qt windows desktop 6.4.0 win64_msvc2022_64 --outputdir C:/Qt
```

### Step 2: Configura CMake

```cmd
cd "C:\Users\MarioSyri\Desktop\PrismLauncherforXylarJava-develop\PrismLauncherforXylarJava-develop"
mkdir build
cd build

cmake -G "Visual Studio 17 2022" -A x64 ^
    -DCMAKE_BUILD_TYPE=Release ^
    -DCMAKE_TOOLCHAIN_FILE="C:/vcpkg/scripts/buildsystems/vcpkg.cmake" ^
    -DVCPKG_TARGET_TRIPLET=x64-windows ^
    -DQt6_DIR="C:/Qt/6.4.0/msvc2022_64/lib/cmake/Qt6" ^
    ..
```

### Step 3: Compila

```cmd
cmake --build . --config Release --parallel 4
```

### Step 4: Localizza l'exe

L'eseguibile si troverà in:
```
build\launcher\Release\PrismLauncher.exe
```

## Troubleshooting

**Errore: "Qt6 non trovato"**
- Assicurati che Qt6 sia stato scaricato in `C:/Qt/6.4.0/`
- Verifica il percorso esatto: `C:/Qt/6.4.0/msvc2022_64/lib/cmake/Qt6`

**Errore: "CMAKE_TOOLCHAIN_FILE non trovato"**
- Assicurati che VCPKG sia installato in `C:\vcpkg\`
- Se in una cartella diversa, aggiorna il path nel comando CMake

**Errore: "Visual Studio non trovato"**
- Installa Visual Studio 2022 da https://visualstudio.microsoft.com/
- Assicurati di selezionare "Desktop development with C++" durante l'installazione

## Customizzazioni

Tutte le modifiche XylarJava sono già applicate nel codice:

- ✓ Launcher limitato a **Fabric 1.21.11 ONLY**
- ✓ Integrazione **Discord "Official Server of Xylar Inc."**
- ✓ Rebranding a **"Prism Launcher for XylarJava"**
- ✓ Infrastruttura per **pre-installazione mods**

## Support

Se hai problemi con la compilazione, controlla:
1. Versioni corrette di tutti i prerequisiti
2. Percorsi corretti nel comando CMake
3. Spazio disco sufficiente (almeno 10GB per Qt6 + compilazione)
4. Connessione internet stabile per scaricare Qt6 (~2.5GB)

Buona fortuna! 🚀
