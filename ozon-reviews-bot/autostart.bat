@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo === Помощник Ozon: работа 24/7 ===
echo.
echo 1. Запуск бота при входе в Windows
schtasks /Create /TN "OzonReviewsBot" /TR "\"%~dp0run.bat\"" /SC ONLOGON /RL LIMITED /F
echo.
echo 2. Компьютер не засыпает от сети (экран гаснуть может — боту это не мешает)
powercfg /change standby-timeout-ac 0
powercfg /change hibernate-timeout-ac 0
echo.
echo Готово. Бот стартует сам после каждого входа в Windows.
echo Чтобы он поднимался и после отключения света без тебя — включи автовход:
echo   Win+R, netplwiz, снять галочку "Требовать ввод имени пользователя и пароля".
echo   (Если галочки нет: Параметры, Учётные записи, Варианты входа, отключить "Вход только с Windows Hello".)
echo И в BIOS можно включить "Restore on AC Power Loss" = Power On.
echo.
echo Убрать автозапуск: schtasks /Delete /TN "OzonReviewsBot" /F
pause
