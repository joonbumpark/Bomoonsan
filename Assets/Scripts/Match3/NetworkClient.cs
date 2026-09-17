using System;
using System.Collections.Generic;
using System.Text;
using NativeWebSocket;
using UnityEngine;

namespace Match3
{
    [Serializable]
    public class LeaderboardEntry
    {
        public string name;
        public int score;
        public string date;
    }

    /// <summary>
    /// 대전 서버(Server/server.js, fly.io에 배포)와의 WebSocket 통신을 담당한다.
    /// 큐 참가 -> 매칭(같은 시드 수신) -> 점수 제출 -> 승패 수신, 리더보드 조회까지 처리한다.
    /// 프로토콜은 한 줄짜리 JSON 메시지이며 서버 쪽 구현과 짝을 이룬다. 보안은 신경 쓰지 않는다
    /// (클라이언트가 보고하는 점수를 서버가 그대로 신뢰한다).
    /// </summary>
    public class NetworkClient : MonoBehaviour
    {
        public string serverUrl = "wss://bomoonsan-match3.fly.dev";

        public event Action OnOpen;
        public event Action OnQueued;
        public event Action<string, int, string> OnMatched; // matchId, seed, opponentName
        public event Action<string, int, int> OnMatchResult; // result(win/lose/draw), yourScore, opponentScore
        public event Action<List<LeaderboardEntry>> OnLeaderboard;
        public event Action<string, int, int> OnLeaderboardRank; // game, rank(1부터), total
        public event Action<string> OnError;

        private WebSocket socket;

        public bool IsConnected => socket != null && socket.State == WebSocketState.Open;
        public bool IsConnecting => socket != null && socket.State == WebSocketState.Connecting;

        public async void Connect()
        {
            if (IsConnected || IsConnecting)
                return;

            socket = new WebSocket(serverUrl);
            socket.OnOpen += () => OnOpen?.Invoke();
            socket.OnError += (err) => OnError?.Invoke(err);
            socket.OnMessage += HandleMessage;

            try
            {
                await socket.Connect();
            }
            catch (Exception e)
            {
                OnError?.Invoke(e.Message);
            }
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // WebGL에서는 브라우저가 메시지를 직접 콜백으로 밀어주므로 큐를 돌릴 필요가 없다.
            socket?.DispatchMessageQueue();
#endif
        }

        private void HandleMessage(byte[] data)
        {
            string json = Encoding.UTF8.GetString(data);

            TypeOnly typeOnly;
            try
            {
                typeOnly = JsonUtility.FromJson<TypeOnly>(json);
            }
            catch
            {
                return;
            }

            switch (typeOnly.type)
            {
                case "queued":
                    OnQueued?.Invoke();
                    break;

                case "matched":
                    var matched = JsonUtility.FromJson<MatchedMessage>(json);
                    OnMatched?.Invoke(matched.matchId, matched.seed, matched.opponentName);
                    break;

                case "match_result":
                    var result = JsonUtility.FromJson<MatchResultMessage>(json);
                    OnMatchResult?.Invoke(result.result, result.yourScore, result.opponentScore);
                    break;

                case "leaderboard":
                    var lb = JsonUtility.FromJson<LeaderboardMessage>(json);
                    OnLeaderboard?.Invoke(new List<LeaderboardEntry>(lb.entries ?? Array.Empty<LeaderboardEntry>()));
                    break;

                case "leaderboard_rank":
                    var rank = JsonUtility.FromJson<LeaderboardRankMessage>(json);
                    OnLeaderboardRank?.Invoke(rank.game, rank.rank, rank.total);
                    break;
            }
        }

        public void JoinQueue(string playerName, string game)
        {
            SendJson(new JoinQueueMessage { name = playerName, game = game });
        }

        public void LeaveQueue()
        {
            SendJson(new SimpleMessage { type = "leave_queue" });
        }

        public void SubmitScore(string matchId, int score)
        {
            SendJson(new SubmitScoreMessage { matchId = matchId, score = score });
        }

        /// <summary>싱글 플레이는 매칭이 없으므로 매치 없이 바로 리더보드에 점수를 낸다.
        /// 서버는 같은 이름의 기존 기록보다 높을 때만 갱신한다.</summary>
        public void SubmitSoloScore(string playerName, string game, int score)
        {
            SendJson(new SubmitSoloScoreMessage { name = playerName, game = game, score = score });
        }

        public void RequestLeaderboard(string game)
        {
            SendJson(new GetLeaderboardMessage { game = game });
        }

        private void SendJson(object message)
        {
            if (!IsConnected)
            {
                OnError?.Invoke("서버에 연결되어 있지 않습니다.");
                return;
            }
            socket.SendText(JsonUtility.ToJson(message));
        }

        public async void CloseConnection()
        {
            if (socket != null)
                await socket.Close();
        }

        private void OnApplicationQuit()
        {
            CloseConnection();
        }

        [Serializable] private class TypeOnly { public string type; }
        [Serializable] private class SimpleMessage { public string type; }
        [Serializable] private class JoinQueueMessage { public string type = "join_queue"; public string name; public string game; }
        [Serializable] private class GetLeaderboardMessage { public string type = "get_leaderboard"; public string game; }
        [Serializable] private class SubmitScoreMessage { public string type = "submit_score"; public string matchId; public int score; }
        [Serializable] private class SubmitSoloScoreMessage { public string type = "submit_solo_score"; public string name; public string game; public int score; }
        [Serializable] private class MatchedMessage { public string type; public string matchId; public int seed; public string opponentName; }
        [Serializable] private class MatchResultMessage { public string type; public string matchId; public string result; public int yourScore; public int opponentScore; }
        [Serializable] private class LeaderboardMessage { public string type; public LeaderboardEntry[] entries; }
        [Serializable] private class LeaderboardRankMessage { public string type; public string game; public int rank; public int total; }
    }
}
