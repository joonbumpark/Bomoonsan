@echo off
rem 퍼블리셔 전달 전 정리: 쓰지 않는 파일을 지우고 publisher-handover 브랜치에 커밋한다.
rem Unity 에디터를 닫은 상태에서 실행하세요.
setlocal
cd /d "%~dp0.."

for /f %%b in ('git rev-parse --abbrev-ref HEAD') do set BRANCH=%%b
if not "%BRANCH%"=="publisher-handover" (
    echo 현재 브랜치가 %BRANCH% 입니다. publisher-handover 브랜치에서 실행하세요.
    pause
    exit /b 1
)

echo [1/3] 작업 메모, 백업, 중복 파일 삭제
git rm -q --ignore-unmatch _changed_files.txt _commits.txt
git rm -q --ignore-unmatch "Assets/Mp4/Title.hevc.mp4~" Assets/Mp4/Intro.mp4 Assets/Mp4/Intro.mp4.meta Assets/Mp4/Title.mp4 Assets/Mp4/Title.mp4.meta
git rm -q --ignore-unmatch Assets/Sound/sndBGM.mp3 Assets/Sound/sndBGM.mp3.meta
git rm -q --ignore-unmatch "Assets/Dialog/5.Horang 1.asset" "Assets/Dialog/5.Horang 1.asset.meta"
git rm -q --ignore-unmatch Tools/deploy_webgl.sh

echo [2/3] 템플릿 잔여물, 레거시 코드, 개발용 도구 삭제
git rm -r -q --ignore-unmatch Assets/TutorialInfo Assets/TutorialInfo.meta Assets/Readme.asset Assets/Readme.asset.meta
git rm -q --ignore-unmatch Assets/Scenes/SampleScene.unity Assets/Scenes/SampleScene.unity.meta
git rm -r -q --ignore-unmatch Assets/Scripts/Legacy Assets/Scripts/Legacy.meta
git rm -q --ignore-unmatch Assets/Scripts/CompletionCounter.cs Assets/Scripts/CompletionCounter.cs.meta
git rm -q --ignore-unmatch Assets/Editor/TriggerActionMigration.cs Assets/Editor/TriggerActionMigration.cs.meta
git rm -q --ignore-unmatch Assets/Editor/InsectSwarmDiagnostics.cs Assets/Editor/InsectSwarmDiagnostics.cs.meta
git rm -q --ignore-unmatch Assets/Editor/BuildRequestWatcher.cs Assets/Editor/BuildRequestWatcher.cs.meta
rem .gitignore에 있는데 커밋돼 있던 파일은 추적만 해제한다 (PC의 파일은 그대로 남는다)
git rm -r -q --cached --ignore-unmatch Bomonsan.slnx .vscode

echo [3/3] 커밋
git add -A
git commit -q -m "퍼블리셔 전달 전 정리" -m "- 쓰지 않는 레거시 액션, 마이그레이션 도구, 진단 도구 제거" -m "- 템플릿 잔여물, 중복 영상/BGM, 작업 메모, WebGL 배포 스크립트 제거" -m "- 대화 맞춤법, 오래된 주석, 테트리스 표기, .gitattributes 정리" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -m "Claude-Session: https://claude.ai/code/session_0187ccuP8smLdrFiZEz7g1Jy"
if errorlevel 1 (echo 커밋할 변경이 없거나 커밋에 실패했습니다.) else (echo 완료. git log -1 --stat 로 확인하세요.)
pause
