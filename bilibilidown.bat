@echo off

:loop

call config.bat
timeout /T 300 /NOBREAK > nul

goto loop