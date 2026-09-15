// 보문산 3매치 퍼즐 타일/아이템 아트 생성기.
// 사용법: node --env-file=.env generate.mjs <이름> "<묘사>"
//   예)  node --env-file=.env generate.mjs pouch_base "a traditional Korean lucky pouch"
//
// 매 호출이 독립적인 API라 세션처럼 스타일이 이어지지 않으므로,
// 공통 스타일 지시문(STYLE_PREFIX)을 코드에서 항상 자동으로 붙여서
// 손으로 매번 긴 프롬프트를 반복해 쓰지 않아도 되게 한다.
//
// - 흰색 채우기 + 검은 외곽선의 "색칠 전" 라인아트로 뽑아서, 유니티에서
//   Image.color로 직접 색을 입힐 수 있게 한다.
// - 배경은 투명(png, alpha)으로 뽑아서 유니티 스프라이트에 바로 쓸 수 있게 한다.
// - 매 호출의 토큰 사용량을 usage_log.json에 누적 기록해서, 정확한 계정 잔액은
//   아니지만 "이번 세션에서 대략 얼마나 썼는지" 누적치를 계속 보여준다.

import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const API_KEY = process.env.OPENAI_API_KEY;
if (!API_KEY) {
  console.error('OPENAI_API_KEY가 없습니다. .env 파일을 확인하세요.');
  process.exit(1);
}

// background=transparent를 그대로 쓰면 흰색으로 채운 오브젝트 내부까지
// "배경"으로 오인해서 같이 지워버리는 문제가 있었다. 그래서 불투명한
// 마젠타(크로마키) 배경으로 뽑은 뒤, chroma_key.py로 배경만 걷어낸다.
const STYLE_PREFIX = `white flat vector line art icon, coloring-book style,
solid white fill with a bold clean black outline only, absolutely no color,
no shading, no gradients, no texture, single centered object,
solid flat magenta background color exactly #FF00FF, no gradient, no vignette,
no shadow on the background.
Absolutely no text, no letters, no words, no numbers, no Korean hangul,
no Chinese/Hanja characters, no symbols or glyphs of any kind anywhere
in the image, no watermark, no signature — pure line-art shapes only.`;

const [, , name, description] = process.argv;
if (!name || !description) {
  console.error('사용법: node --env-file=.env generate.mjs <파일이름> "<묘사>"');
  process.exit(1);
}

const outDir = path.join(import.meta.dirname, 'out');
const rawDir = path.join(import.meta.dirname, 'raw');
fs.mkdirSync(outDir, { recursive: true });
fs.mkdirSync(rawDir, { recursive: true });

const prompt = `${STYLE_PREFIX}. Subject: ${description}`;

console.log(`생성 중: ${name}`);
console.log(`프롬프트: ${prompt}`);

const res = await fetch('https://api.openai.com/v1/images/generations', {
  method: 'POST',
  headers: {
    Authorization: `Bearer ${API_KEY}`,
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({
    model: 'gpt-image-1',
    prompt,
    size: '1024x1024',
    n: 1,
    background: 'opaque',
    output_format: 'png',
  }),
});

const data = await res.json();

if (!res.ok) {
  console.error('API 에러:', JSON.stringify(data, null, 2));
  process.exit(1);
}

const item = data.data[0];
const rawPath = path.join(rawDir, `${name}.png`);
const outPath = path.join(outDir, `${name}.png`);

if (item.b64_json) {
  fs.writeFileSync(rawPath, Buffer.from(item.b64_json, 'base64'));
} else if (item.url) {
  const imgRes = await fetch(item.url);
  fs.writeFileSync(rawPath, Buffer.from(await imgRes.arrayBuffer()));
} else {
  console.error('응답에 이미지 데이터가 없습니다:', JSON.stringify(data, null, 2));
  process.exit(1);
}

console.log(`원본(마젠타 배경) 저장됨: ${rawPath}`);

// --- 마젠타 배경 제거 (크로마키) ---
// 원본은 raw/에 그대로 남겨두므로, chroma_key.py의 임계값만 바꿔서
// API를 다시 호출하지 않고 로컬에서 재처리할 수 있다:
//   .venv/bin/python3 chroma_key.py raw/<name>.png out/<name>.png
const pythonBin = path.join(import.meta.dirname, '.venv', 'bin', 'python3');
const chromaScript = path.join(import.meta.dirname, 'chroma_key.py');
execFileSync(pythonBin, [chromaScript, rawPath, outPath], { stdio: 'inherit' });

// --- 사용량 누적 기록 (계정 잔액 조회 API가 없어서, 이번 세션 누적치로 참고) ---
if (data.usage) {
  const logPath = path.join(import.meta.dirname, 'usage_log.json');
  let log = { calls: 0, total_tokens: 0, input_tokens: 0, output_tokens: 0 };
  try {
    log = JSON.parse(fs.readFileSync(logPath, 'utf8'));
  } catch {
    // 첫 실행이면 기본값 사용
  }
  log.calls += 1;
  log.total_tokens += data.usage.total_tokens || 0;
  log.input_tokens += data.usage.input_tokens || 0;
  log.output_tokens += data.usage.output_tokens || 0;
  fs.writeFileSync(logPath, JSON.stringify(log, null, 2));

  console.log(`이번 요청 토큰: ${JSON.stringify(data.usage)}`);
  console.log(`누적(이 세션): 호출 ${log.calls}회, 총 ${log.total_tokens} 토큰`);
} else {
  console.log('(응답에 usage 필드 없음 — 계정 잔액은 대시보드에서 확인하세요: https://platform.openai.com/settings/organization/billing/overview)');
}
