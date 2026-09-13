#!/usr/bin/env bash
# 3매치 퍼즐(InGameScene)을 WebGL로 빌드해서 나스 Web Station(/volume1/web/match3)에 업로드한다.
#
# 사전 조건:
#   1. Unity 에디터가 이 프로젝트를 열고 있으면 안 된다 (배치 모드 빌드가 막힘).
#   2. 나스 공유폴더가 Finder에서 마운트되어 있어야 한다: smb://192.168.0.15/web -> /Volumes/web
#
# 사용법: ./Tools/deploy_webgl.sh

set -uo pipefail

PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY="/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity"
LOG_FILE="$PROJECT_PATH/Logs/webgl_build.log"
BUILD_DIR="$PROJECT_PATH/Builds/WebGL"
NAS_SHARE="/Volumes/web"
NAS_TARGET="$NAS_SHARE/match3"
SITE_URL="https://beom86.direct.quickconnect.to/match3/"

mkdir -p "$PROJECT_PATH/Logs"

# 1) Unity 에디터가 이미 이 프로젝트를 열고 있는지 확인
if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ] && lsof "$PROJECT_PATH/Temp/UnityLockfile" >/dev/null 2>&1; then
    echo "오류: Unity 에디터가 이 프로젝트를 열고 있습니다. 먼저 에디터를 닫고 다시 실행해주세요." >&2
    exit 1
fi

# 2) 나스 공유폴더 마운트 확인
if [ ! -d "$NAS_SHARE" ]; then
    echo "오류: 나스 공유폴더가 마운트되어 있지 않습니다." >&2
    echo "Finder에서 Cmd+K -> smb://192.168.0.15/web 로 먼저 연결해주세요." >&2
    exit 1
fi

# 3) WebGL 빌드 (배치 모드)
echo "[1/2] WebGL 빌드 중... (로그: $LOG_FILE)"
"$UNITY" -batchmode -nographics -quit \
    -projectPath "$PROJECT_PATH" \
    -logFile "$LOG_FILE" \
    -executeMethod Match3.EditorTools.WebGLBuildScript.Build
BUILD_EXIT=$?

if [ $BUILD_EXIT -ne 0 ]; then
    echo "빌드 실패 (exit $BUILD_EXIT). 로그 마지막 40줄:" >&2
    tail -n 40 "$LOG_FILE" >&2
    exit 1
fi

if [ ! -f "$BUILD_DIR/index.html" ]; then
    echo "오류: 빌드 결과물을 찾을 수 없습니다: $BUILD_DIR" >&2
    exit 1
fi

# 4) 나스의 전용 폴더(match3)에만 업로드 - 다른 프로젝트 폴더는 건드리지 않는다
echo "[2/2] 나스로 업로드 중... ($NAS_TARGET)"
mkdir -p "$NAS_TARGET"
if ! rsync -av --delete "$BUILD_DIR/" "$NAS_TARGET/"; then
    echo "오류: 업로드 실패." >&2
    exit 1
fi

echo ""
echo "완료! 아래 URL에서 확인하세요:"
echo "$SITE_URL"
