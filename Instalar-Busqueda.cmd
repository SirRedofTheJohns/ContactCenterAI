@echo off
cd /d "%~dp0"
echo Descarga explicita de BGE-M3 aprobado desde Ollama, aproximadamente 1.2 GB.
echo Requiere conexion a internet y Python. Los pesos se guardan dentro de .local.
python eng\Download-EmbeddingModel.py
pause
