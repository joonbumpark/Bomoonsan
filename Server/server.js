// Bomoonsan 미니게임 모음 - 대전 모드 서버.
//
// 흐름: 커넥트 -> join_queue(name, game)로 그 게임의 큐에 들어감 -> 같은 게임을
//      기다리는 2명이 모이면 매칭되어 같은 시드 전달 -> 각자 클라이언트에서
//      정해진 시간(게임마다 다름) 플레이 -> submit_score로 최종 점수 제출
//      -> 둘 다 제출하면(또는 타임아웃되면) 승/패/무 판정 후 각자에게 결과 전송
//      -> 해당 게임의 리더보드(점수 내림차순)에 기록.
//
// 싱글 플레이는 매칭이 없으므로 submit_solo_score로 바로 리더보드에 기록한다 -
// 대전이든 싱글이든 같은 리더보드를 쓰고, 같은 이름(닉네임)의 기록은 항상 더 높은
// 점수만 남긴다 (addToLeaderboard 참고). 기록 직후엔 결과 화면의 "내 등수/전체
// 유저" 표시용으로 leaderboard_rank 메시지를 그 자리에서 바로 보내준다(getRank).
//
// 게임 종류(GAMES)마다 큐와 리더보드가 독립적이라, 다른 게임을 고른 플레이어끼리는
// 매칭되지 않는다. 보안/부정행위 방지는 신경 쓰지 않는다 (클라이언트가 보낸 점수를
// 그대로 신뢰한다).

const http = require('http');
const fs = require('fs');
const path = require('path');
const { WebSocketServer, WebSocket } = require('ws');

const PORT = process.env.PORT || 8080;
const DATA_DIR = process.env.DATA_DIR || path.join(__dirname, 'data');
const LEADERBOARD_FILE = path.join(DATA_DIR, 'leaderboard.json');
const LEADERBOARD_MAX = 100;
// 가장 긴 라운드(3매치 퍼즐, 90초) + 여유시간. 이 안에 양쪽 점수가 다 안 모이면
// 강제로 매치를 종료한다. 더 짧은 게임(복주머니 잡기/순서 기억하기)에도 그대로 쓴다 -
// 정상적으로 끝나면 어차피 각자 submit_score를 바로 보내니 이 타임아웃까지 안 간다.
const MATCH_TIMEOUT_MS = 130000;

const GAMES = ['match3', 'whack', 'simon', 'tetris', 'jigsaw'];
function normalizeGame(game) {
  return GAMES.includes(game) ? game : 'match3';
}

// ---------------------------------------------------------------------------
// 리더보드 (게임별로 분리, 파일 기반 - fly.io 볼륨에 마운트되어 재배포/재시작에도 유지된다)
// ---------------------------------------------------------------------------

if (!fs.existsSync(DATA_DIR)) fs.mkdirSync(DATA_DIR, { recursive: true });

let leaderboards = { match3: [], whack: [], simon: [], tetris: [], jigsaw: [] };
try {
  const loaded = JSON.parse(fs.readFileSync(LEADERBOARD_FILE, 'utf8'));
  if (Array.isArray(loaded)) {
    // 게임 구분이 생기기 전(예전 버전)의 파일 형식 - 전부 match3 기록으로 취급한다.
    leaderboards.match3 = loaded;
  } else if (loaded && typeof loaded === 'object') {
    for (const g of GAMES) {
      if (Array.isArray(loaded[g])) leaderboards[g] = loaded[g];
    }
  }
} catch {
  // 파일이 없거나 파싱 실패 -> 빈 상태로 시작
}

function saveLeaderboards() {
  try {
    fs.writeFileSync(LEADERBOARD_FILE, JSON.stringify(leaderboards, null, 2));
  } catch (err) {
    console.error('리더보드 저장 실패:', err);
  }
}

// 같은 이름(닉네임)의 기록은 하나만 유지하고, 새 점수가 기존 기록보다 높을 때만
// 갱신한다 (싱글/대전 어느 쪽에서 냈든 동일하게 적용).
function addToLeaderboard(game, name, score) {
  const list = leaderboards[game] || (leaderboards[game] = []);
  const existing = list.find((entry) => entry.name === name);

  if (existing) {
    if (score <= existing.score) return;
    existing.score = score;
    existing.date = new Date().toISOString();
  } else {
    list.push({ name, score, date: new Date().toISOString() });
  }

  list.sort((a, b) => b.score - a.score);
  if (list.length > LEADERBOARD_MAX) list.length = LEADERBOARD_MAX;
  saveLeaderboards();
}

// addToLeaderboard 직후에 그 이름의 등수(1부터)와 전체 기록 수를 구한다 - 결과
// 화면의 "내 등수/전체 유저" 표시용. 리더보드가 LEADERBOARD_MAX로 잘려 있으면
// 그 밖으로 밀려난(너무 낮은 점수의) 기록은 안 잡힐 수 있다.
function getRank(game, name) {
  const list = leaderboards[game] || [];
  const index = list.findIndex((entry) => entry.name === name);
  return { rank: index === -1 ? 0 : index + 1, total: list.length };
}

// ---------------------------------------------------------------------------
// 매치메이킹 (게임별 큐)
// ---------------------------------------------------------------------------

const queues = { match3: [], whack: [], simon: [], tetris: [], jigsaw: [] };
const matches = new Map(); // matchId -> { game, players, scores, names, timeout }

function send(ws, obj) {
  if (ws.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify(obj));
  }
}

function tryMatch(game) {
  const queue = queues[game];
  while (queue.length >= 2) {
    const a = queue.shift();
    const b = queue.shift();

    if (a.readyState !== WebSocket.OPEN) { if (b.readyState === WebSocket.OPEN) queue.unshift(b); continue; }
    if (b.readyState !== WebSocket.OPEN) { queue.unshift(a); continue; }

    const matchId = Math.random().toString(36).slice(2) + Date.now().toString(36);
    const seed = Math.floor(Math.random() * 2147483647);

    const match = { game, players: [a, b], scores: [null, null], names: [a.playerName, b.playerName], timeout: null };
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

  if (match.names[0]) {
    addToLeaderboard(match.game, match.names[0], s0);
    const { rank, total } = getRank(match.game, match.names[0]);
    send(p0, { type: 'leaderboard_rank', game: match.game, rank, total });
  }
  if (match.names[1]) {
    addToLeaderboard(match.game, match.names[1], s1);
    const { rank, total } = getRank(match.game, match.names[1]);
    send(p1, { type: 'leaderboard_rank', game: match.game, rank, total });
  }
}

function removeFromQueue(ws) {
  for (const g of GAMES) {
    const idx = queues[g].indexOf(ws);
    if (idx !== -1) queues[g].splice(idx, 1);
  }
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
        const game = normalizeGame(msg.game);
        removeFromQueue(ws); // 혹시 다른 게임 큐에 있었으면 빼고 새 큐로 옮긴다.
        queues[game].push(ws);
        send(ws, { type: 'queued' });
        tryMatch(game);
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

      case 'submit_solo_score': {
        const game = normalizeGame(msg.game);
        const name = String(msg.name || ws.playerName || 'Player').slice(0, 20) || 'Player';
        const score = Math.max(0, Math.floor(Number(msg.score)) || 0);
        addToLeaderboard(game, name, score);
        const { rank, total } = getRank(game, name);
        send(ws, { type: 'leaderboard_rank', game, rank, total });
        break;
      }

      case 'get_leaderboard': {
        const game = normalizeGame(msg.game);
        send(ws, { type: 'leaderboard', entries: (leaderboards[game] || []).slice(0, 20) });
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
