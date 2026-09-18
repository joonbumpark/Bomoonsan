#!/usr/bin/env bash
# GCP 프로젝트/리소스 최초 1회 설정. 이후 배포는 deploy-gcp.sh 사용.
#
# 사용법:
#   PROJECT_ID=your-project-id ./gcp-setup.sh
set -euo pipefail

PROJECT_ID="${PROJECT_ID:?PROJECT_ID 환경변수를 지정하세요 (예: PROJECT_ID=bomoonsan-match3-xxxx ./gcp-setup.sh)}"
REGION="${REGION:-asia-northeast3}"
BUCKET="${BUCKET:-${PROJECT_ID}-data}"

echo "== 프로젝트 생성 (이미 있으면 건너뜀) =="
gcloud projects create "$PROJECT_ID" --name="Bomoonsan Match3" || true

gcloud config set project "$PROJECT_ID"
gcloud config set run/region "$REGION"

echo
echo "== 결제 계정 연결 =="
if ! gcloud billing projects describe "$PROJECT_ID" --format="value(billingEnabled)" 2>/dev/null | grep -q True; then
  echo "사용 가능한 결제 계정:"
  gcloud billing accounts list
  read -rp "위 목록의 ACCOUNT_ID를 입력하세요: " BILLING_ACCOUNT_ID
  gcloud billing projects link "$PROJECT_ID" --billing-account="$BILLING_ACCOUNT_ID"
else
  echo "이미 결제 계정이 연결되어 있습니다."
fi

echo
echo "== 필요한 API 활성화 =="
gcloud services enable run.googleapis.com artifactregistry.googleapis.com cloudbuild.googleapis.com storage.googleapis.com

echo
echo "== 리더보드 저장용 GCS 버킷 생성 (이미 있으면 건너뜀) =="
gcloud storage buckets create "gs://$BUCKET" --location="$REGION" --uniform-bucket-level-access || true

echo
echo "설정 완료. PROJECT_ID=$PROJECT_ID, REGION=$REGION, BUCKET=$BUCKET"
echo "다음 단계: PROJECT_ID=$PROJECT_ID REGION=$REGION BUCKET=$BUCKET ./deploy-gcp.sh"
