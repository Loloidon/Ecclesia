@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET SDK est introuvable. Installez .NET SDK 10 puis reessayez.
    pause
    exit /b 1
)
where npm.cmd >nul 2>&1
if errorlevel 1 (
    echo npm est introuvable. Installez Node.js puis reessayez.
    pause
    exit /b 1
)

pushd "%~dp0frontend"
if not exist "node_modules\@angular\cli" (
    echo Installation des dependances Angular...
    call npm.cmd ci
    if errorlevel 1 (
        echo L'installation des dependances a echoue.
        popd
        pause
        exit /b 1
    )
)
start "Ecclesia - Angular" cmd /k "npm start"
popd

pushd "%~dp0backend\Ecclesia.Api"
start "Ecclesia - API" cmd /k "dotnet run --launch-profile http"
popd

echo API et Angular ont ete lances dans deux terminaux.
echo Angular : http://localhost:4200
echo API     : http://localhost:5145
echo PostgreSQL doit etre demarre et configure separement.
endlocal
