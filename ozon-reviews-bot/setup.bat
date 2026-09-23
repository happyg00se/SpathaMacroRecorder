@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo === Установка бота отзывов Ozon ===
where py >nul 2>nul && (set PY=py -3) || (set PY=python)
%PY% -m venv .venv || (echo Нужен Python 3.10+ с python.org — поставь галочку "Add to PATH" & pause & exit /b 1)
call .venv\Scripts\activate.bat
python -m pip install --upgrade pip >nul
pip install -r requirements.txt || (pause & exit /b 1)
if not exist config.yaml (
  copy config.example.yaml config.yaml >nul
  echo Создан config.yaml — открой его в Блокноте и заполни.
  notepad config.yaml
)
echo.
echo Готово. Дальше:
echo   1. Установи Ollama с ollama.com и выполни: ollama pull qwen2.5:14b
echo   2. Если source: browser — запусти login.bat и войди в кабинет Ozon
echo   3. Проверка без публикации: dry-run.bat
echo   4. Запуск бота: run.bat
pause
