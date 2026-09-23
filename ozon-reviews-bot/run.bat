@echo off
chcp 65001 >nul
cd /d "%~dp0"
call .venv\Scripts\activate.bat
:loop
python -m ozon_bot run
echo Бот остановился. Перезапуск через 30 секунд, Ctrl+C — выйти.
timeout /t 30 >nul
goto loop
