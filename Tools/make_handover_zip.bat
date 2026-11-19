@echo off
rem 퍼블리셔에게 넘길 압축 파일을 만든다. git 저장소와 Unity 캐시, 내부용 도구는 빠진다.
rem 결과: 프로젝트 폴더 옆에 Bomonsan_handover_날짜.zip
setlocal
cd /d "%~dp0.."
set PROJECT=%CD%
for %%i in ("%PROJECT%\..") do set PARENT=%%~fi
for /f %%d in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd"') do set TODAY=%%d
set STAGE=%TEMP%\Bomonsan_handover\Bomonsan
set ZIP=%PARENT%\Bomonsan_handover_%TODAY%.zip

if exist "%TEMP%\Bomonsan_handover" rmdir /s /q "%TEMP%\Bomonsan_handover"
if exist "%ZIP%" del "%ZIP%"

echo [1/2] 전달할 파일 복사 중...
robocopy "%PROJECT%" "%STAGE%" /E /NFL /NDL /NJH /NP ^
  /XD .git .vs .vscode .idea Library Temp Logs obj Builds UserSettings MemoryCaptures Recordings "%PROJECT%\Tools\asset-gen" ^
  /XF *.csproj *.sln *.slnx *.user *.suo *.pidb *.booproj *.userprefs *.apk *.aab .DS_Store
if errorlevel 8 (
    echo 복사 실패
    pause
    exit /b 1
)

echo [2/2] 압축 중... (몇 분 걸릴 수 있습니다)
powershell -NoProfile -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::CreateFromDirectory('%STAGE%', '%ZIP%', [IO.Compression.CompressionLevel]::Optimal, $true)"
if errorlevel 1 (
    echo 압축 실패
    pause
    exit /b 1
)

rmdir /s /q "%TEMP%\Bomonsan_handover"
echo 완료: %ZIP%
pause
