@echo OFF

IF EXIST .\publish\ rmdir .\publish\ /S /Q

mkdir publish
mkdir publish\build

dotnet build ..\src\ExportSWC\ExportSWC.csproj /p:SolutionDir=..\src\ /p:PreBuildEvent= -o publish\build -v q

IF %ERRORLEVEL% gtr 0 GOTO error

dotnet build ..\src\ExportSWCPackager\ExportSWCPackager.csproj -o publish -v q

IF %ERRORLEVEL% gtr 0 GOTO error

copy ..\README.md publish\
IF %ERRORLEVEL% gtr 0 GOTO error
copy ..\LICENSE.txt publish\
IF %ERRORLEVEL% gtr 0 GOTO error
copy publish\build\ExportSWC.dll publish\
IF %ERRORLEVEL% gtr 0 GOTO error

cd publish
ExportSWCPackager.exe
cd..
IF %ERRORLEVEL% gtr 0 GOTO error

GOTO complete

exit 0

:error
echo:
echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
echo ERROR: Error building the package 2>&1
echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
echo:

:: force error level 1
@(call)

:complete