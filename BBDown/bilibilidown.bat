@echo off

:loop

call config.bat
pause
timeout /t 300

goto loop