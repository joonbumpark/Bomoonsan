@echo off
rem Unity 에디터를 닫은 상태에서 실행하세요 (에디터가 프로젝트를 열고 있으면 배치 모드가 실패합니다).
setlocal
set UNITY="D:\Program Files\6000.0.82f1\Editor\Unity.exe"
set PROJECT=%~dp0..
if not exist "%PROJECT%\Builds" mkdir "%PROJECT%\Builds"

echo Windows 빌드 중...
%UNITY% -batchmode -nographics -quit -projectPath "%PROJECT%" -buildTarget Win64 -executeMethod Match3.EditorTools.WindowsBuildScript.Build -logFile "%PROJECT%\Builds\build_windows.log"
if errorlevel 1 (echo Windows 빌드 실패 - Builds\build_windows.log 확인) else (echo Windows 빌드 성공: Builds\Windows\Bomoonsan.exe)

pause
