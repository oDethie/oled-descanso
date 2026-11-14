@echo off
rem Compila o OLED Descanso usando o compilador C# que ja vem no Windows (.NET Framework 4.x).
rem Nao precisa instalar nada.

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Compilador C# nao encontrado. Instale o .NET Framework 4.8.
    exit /b 1
)

cd /d "%~dp0"
"%CSC%" /nologo /target:winexe /optimize /r:System.Management.dll /win32icon:OledDescanso.ico /out:OledDescanso.exe OledDescanso.cs
if errorlevel 1 (
    echo.
    echo Erro ao compilar.
    exit /b 1
)
echo.
echo Pronto: OledDescanso.exe
