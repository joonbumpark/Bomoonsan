@echo off
rem Unity 에디터를 닫은 상태에서 실행하세요 (에디터가 프로젝트를 열고 있으면 배치 모드가 실패합니다).
setlocal
set UNITY="D:\Program Files\6000.0.82f1\Editor\Unity.exe"
set PROJECT=%~dp0..
if not exist "%PROJECT%\Builds" mkdir "%PROJECT%\Builds"

echo Android 빌드 중...
%UNITY% -batchmode -nographics -quit -projectPath "%PROJECT%" -buildTarget Android -executeMethod Match3.EditorTools.AndroidBuildScript.Build -logFile "%PROJECT%\Builds\build_android.log"
if errorlevel 1 (echo Android 빌드 실패 - Builds\build_android.log 확인) else (echo Android 빌드 성공: Builds\Android)

pause
