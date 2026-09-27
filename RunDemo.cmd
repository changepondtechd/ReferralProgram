@echo off
cd /d "%~dp0"
echo MARS Circle demo will be available at http://localhost:5080
dotnet run --project src/MarsReferral.Web
pause
