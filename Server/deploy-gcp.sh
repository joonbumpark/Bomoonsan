#!/usr/bin/env bash
# Cloud Run 배포/재배포. 최초 1회는 gcp-setup.sh를 먼저 실행할 것.
#
# 서버는 매칭 큐를 메모리에 들고 있으므로 인스턴스가 여러 개로 늘어나면 안 된다 -
# min/max-instances를 1로 고정해 fly.io의 "항상 켜진 머신 1대" 구성을 그대로 재현한다.
# 리더보드 파일은 GCS 버킷을 /data에 볼륨 마운트해서 fly.io 볼륨을 대체한다.
#
# 사용법:
#   PROJECT_ID=your-project-id ./deploy-gcp.sh
set -euo pipefail

PROJECT_ID="${PROJECT_ID:?PROJECT_ID 환경변수를 지정하세요}"
REGION="${REGION:-asia-northeast3}"
SERVICE="${SERVICE:-bomoonsan-match3}"
BUCKET="${BUCKET:-${PROJECT_ID}-data}"

cd "$(dirname "$0")"

gcloud run deploy "$SERVICE" \
  --project "$PROJECT_ID" \
  --region "$REGION" \
  --source . \
  --allow-unauthenticated \
  --min-instances=1 \
  --max-instances=1 \
  --concurrency=250 \
  --timeout=3600 \
  --execution-environment=gen2 \
  --add-volume=name=data,type=cloud-storage,bucket="$BUCKET" \
  --add-volume-mount=volume=data,mount-path=/data \
  --set-env-vars=DATA_DIR=/data

echo
echo "배포 완료. 위 출력의 Service URL을 클라이언트(NetworkClient.cs, AppFlowManager.cs)의 serverUrl에 반영하세요."
echo "형식: wss://<run.app 도메인> (https:// 대신 wss://)"
