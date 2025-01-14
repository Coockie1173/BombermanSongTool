@echo off
REM Change to the directory where the batch script is located
cd /d "%~dp0"

REM Define source and destination directories
set "src_dir=WorkSpace"
set "dest_dir=.\bin\Debug\net8.0\%src_dir%"

REM Remove the destination directory if it exists
if exist "%dest_dir%" (
    rmdir /s /q "%dest_dir%"
)

REM Create the destination directory
mkdir "%dest_dir%"


REM Copy files recursively
xcopy "%src_dir%\*" "%dest_dir%\" /E /H /C /I

REM Exit the script
exit /b
