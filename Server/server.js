// Bomoonsan 3매치 퍼즐 - 대전 모드 서버.
//
// 흐름: 커넥트 -> join_queue로 큐에 들어감 -> 2명이 모이면 매칭되어 같은 시드 전달
//      -> 각자 클라이언트에서 90초 플레이 -> submit_score로 최종 점수 제출
//      -> 둘 다 제출하면(또는 타임아웃되면) 승/패/무 판정 후 각자에게 결과 전송
//      -> 리더보드(점수 내림차순)에 기록.
//
// 보안/부정행위 방지는 신경 쓰지 않는다 (클라이언트가 보낸 점수를 그대로 신뢰).

const http = require('http');
const fs = require('fs');
const path = require('path');
const { WebSocketServer, WebSocket } = require('ws');

const PORT = process.env.PORT || 8080;
const DATA_DIR = process.env.DATA_DIR || path.join(__dirname, 'data');
const LEADERBOARD_FILE = path.join(DATA_DIR, 'leaderboard.json');
const LEADERBOARD_MAX = 100;
// 한 라운드(90초) + 여유시간. 이 안에 양쪽 점수가 다 안 모이면 강제로 매치를 종료한다.
const MATCH_TIMEOUT_MS = 130000;

// ---------------------------------------------------------------------------
// 리더보드 (파일 기반 - fly.io 볼륨에 마운트되어 재배포/재시작에도 유지된다)
// ---------------------------------------------------------------------------

if (!fs.existsSync(DATA_DIR)) fs.mkdirSync(DATA_DIR, { recursive: true });

let leaderboard = [];
try {
  leaderboard = JSON.parse(fs.readFileSync(LEADERBOARD_FILE, 'utf8'));
  if (!Array.isArray(leaderboard)) leaderboard = [];
} catch {
  leaderboard = [];
}

function saveLeaderboard() {
  try {
    fs.writeFileSync(LEADERBOARD_FILE, JSON.stringify(leaderboard, null, 2));
  } catch (err) {
    console.error('리더보드 저장 실패:', err);
  }
}

function addToLeaderboard(name, score) {
  leaderboard.push({ name, score, date: new Date().toISOString() });
  leaderboard.sort((a, b) => b.score - a.score);
  if (leaderboard.length > LEADERBOARD_MAX) leaderboard.length = LEADERBOARD_MAX;
  saveLeaderboard();
}

// ---------------------------------------------------------------------------
// 매치메이킹
// ---------------------------------------------------------------------------

let queue = [];
const matches = new Map(); // matchId -> { players, scores, names, timeout }

function send(ws, obj) {
  if (ws.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify(obj));
  }
}

function tryMatch() {
  while (queue.length >= 2) {
    const a = queue.shift();
    const b = queue.shift();

    if (a.readyState !== WebSocket.OPEN) { if (b.readyState === WebSocket.OPEN) queue.unshift(b); continue; }
    if (b.readyState !== WebSocket.OPEN) { queue.unshift(a); continue; }

    const matchId = Math.random().toString(36).slice(2) + Date.now().toString(36);
    const seed = Math.floor(Math.random() * 2147483647);

    const match = { players: [a, b], scores: [null, null], names: [a.playerName, b.playerName], timeout: null };
    matches.set(matchId, match);
    a.matchId = matchId;
    b.matchId = matchId;

    match.timeout = setTimeout(() => finishMatch(matchId), MATCH_TIMEOUT_MS);

    send(a, { type: 'matched', matchId, seed, opponentName: b.playerName });
    send(b, { type: 'matched', matchId, seed, opponentName: a.playerName });
  }
}

function finishMatch(matchId) {
  const match = matches.get(matchId);
  if (!match) return;
  clearTimeout(match.timeout);
  matches.delete(matchId);

  const scores = match.scores.map((s) => (typeof s === 'number' ? s : 0));
  const [s0, s1] = scores;
  const [p0, p1] = match.players;

  let result0;
  let result1;
  if (s0 === s1) {
    result0 = result1 = 'draw';
  } else if (s0 > s1) {
    result0 = 'win';
    result1 = 'lose';
  } else {
    result0 = 'lose';
    result1 = 'win';
  }

  send(p0, { type: 'match_result', matchId, result: result0, yourScore: s0, opponentScore: s1 });
  send(p1, { type: 'match_result', matchId, result: result1, yourScore: s1, opponentScore: s0 });

  if (match.names[0]) addToLeaderboard(match.names[0], s0);
  if (match.names[1]) addToLeaderboard(match.names[1], s1);
}

function removeFromQueue(ws) {
  const idx = queue.indexOf(ws);
  if (idx !== -1) queue.splice(idx, 1);
}

// 접속이 끊긴 플레이어는 0점 처리하고, 상대가 이미 점수를 냈으면 바로 매치를 마무리한다.
function handleDisconnect(ws) {
  removeFromQueue(ws);
  if (!ws.matchId) return;
  const match = matches.get(ws.matchId);
  if (!match) return;

  const idx = match.players.indexOf(ws);
  if (idx !== -1 && match.scores[idx] === null) {
    match.scores[idx] = 0;
  }
  if (match.scores.every((s) => s !== null)) {
    finishMatch(ws.matchId);
  }
}

// ---------------------------------------------------------------------------
// HTTP(헬스체크용) + WebSocket 서버
// ---------------------------------------------------------------------------

const httpServer = http.createServer((req, res) => {
  res.writeHead(200, { 'Content-Type': 'text/plain; charset=utf-8' });
  res.end('match3 server ok');
});

const wss = new WebSocketServer({ server: httpServer });

wss.on('connection', (ws) => {
  ws.playerName = 'Player';
  ws.matchId = null;

  ws.on('message', (raw) => {
    let msg;
    try {
      msg = JSON.parse(raw.toString());
    } catch {
      return;
    }

    switch (msg.type) {
      case 'join_queue': {
        ws.playerName = String(msg.name || 'Player').slice(0, 20) || 'Player';
        if (!queue.includes(ws)) queue.push(ws);
        send(ws, { type: 'queued' });
        tryMatch();
        break;
      }

      case 'leave_queue': {
        removeFromQueue(ws);
        break;
      }

      case 'submit_score': {
        const match = matches.get(msg.matchId);
        if (!match) return;
        const idx = match.players.indexOf(ws);
        if (idx === -1) return;

        const score = Math.max(0, Math.floor(Number(msg.score)) || 0);
        match.scores[idx] = score;

        if (match.scores.every((s) => s !== null)) {
          finishMatch(msg.matchId);
        }
        break;
      }

      case 'get_leaderboard': {
        send(ws, { type: 'leaderboard', entries: leaderboard.slice(0, 20) });
        break;
      }

      default:
        break;
    }
  });

  ws.on('close', () => handleDisconnect(ws));
  ws.on('error', () => handleDisconnect(ws));
});

httpServer.listen(PORT, () => {
  console.log(`match3 server listening on :${PORT}`);
});
