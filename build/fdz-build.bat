@echo OFF
SET FlashDevelopCompatability=%1

echo FlashDevelop Compatability: %FlashDevelopCompatability%

"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe > msbuildpath.txt
SET /p msbuildpath=<msbuildpath.txt
del msbuildpath.txt

echo Found MSBuild.exe at '%msbuildpath%'

IF EXIST .\publish\ rmdir .\publish\ /S /Q

mkdir publish
mkdir publish\build

IF %FlashDevelopCompatability% == 5.3.3 dotnet build ..\src\ExportSWC\ExportSWC.csproj /p:SolutionDir=..\src\ /p:PreBuildEvent= /p:FlashDevelopCompatability=%FlashDevelopCompatability% -o publish\build -a AnyCPU -v q

IF %FlashDevelopCompatability% == development "%msbuildpath%" ..\src\ExportSWC\ExportSWC.csproj /p:SolutionDir=..\src\ /p:PreBuildEvent= /p:FlashDevelopCompatability=%FlashDevelopCompatability% /p:OutputPath="..\..\build\publish\build" /p:Platform="AnyCPU" -v:m

::IF %FlashDevelopCompatability% == development dotnet msbuild ..\src\ExportSWC\ExportSWC.csproj /p:SolutionDir=..\src\ /p:PreBuildEvent= /p:FlashDevelopCompatability=%FlashDevelopCompatability% /p:OutputPath="..\..\build\publish\build" /p:Platform="AnyCPU"


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
ExportSWCPackager.exe %FlashDevelopCompatability%
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