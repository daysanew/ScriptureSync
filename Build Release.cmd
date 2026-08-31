@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-release.ps1"
if errorlevel 1 (
  echo.
  echo Release build failed. Review the message above.
) else (
  echo.
  echo Release package created successfully.
)
pause
