using System;
using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    /// <summary>
    /// 매치3를 제외한 나머지 게임(테트리스/직소 퍼즐 등)은 대전이 없어서 서버 리더보드
    /// (NetworkClient.SubmitSoloScore/RequestLeaderboard)를 안 쓰고, 대신 이 기기의
    /// PlayerPrefs에만 점수 기록을 남긴다. 기록 방식은 서버 쪽(Server/server.js의
    /// addToLeaderboard)과 동일하게 맞췄다 - 같은 닉네임 기록은 하나만 유지하고, 더
    /// 높은 점수를 냈을 때만 갱신한다.
    /// </summary>
    public static class LocalLeaderboardStore
    {
        private const int MaxEntries = 50;
        private const string PrefKeyPrefix = "local_leaderboard_";

        [Serializable]
        private class EntryList
        {
            public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
        }

        /// <summary>라운드가 끝날 때마다 호출한다. 같은 이름의 기존 기록보다 높을 때만
        /// 실제로 갱신된다.</summary>
        public static void RecordScore(GameKind game, string name, int score)
        {
            var list = Load(game);
            var existing = list.entries.Find(e => e.name == name);

            if (existing != null)
            {
                if (score <= existing.score)
                    return;

                existing.score = score;
                existing.date = DateTime.UtcNow.ToString("o");
            }
            else
            {
                list.entries.Add(new LeaderboardEntry
                {
                    name = name,
                    score = score,
                    date = DateTime.UtcNow.ToString("o"),
                });
            }

            list.entries.Sort((a, b) => b.score.CompareTo(a.score));
            if (list.entries.Count > MaxEntries)
                list.entries.RemoveRange(MaxEntries, list.entries.Count - MaxEntries);

            Save(game, list);
        }

        /// <summary>점수 내림차순으로 정렬된 이 게임의 로컬 기록. NetworkClient.OnLeaderboard와
        /// 같은 LeaderboardEntry를 쓰므로, 나중에 리더보드 화면을 붙일 때 서버/로컬 어느
        /// 쪽이든 같은 코드로 그릴 수 있다.</summary>
        public static List<LeaderboardEntry> GetEntries(GameKind game) => Load(game).entries;

        /// <summary>RecordScore로 방금 기록한 name의 등수(1부터)와 전체 기록 수를 돌려준다.
        /// name이 없으면(RecordScore를 먼저 안 불렀으면) rank는 0이 된다.</summary>
        public static (int rank, int total) GetRank(GameKind game, string name)
        {
            var entries = Load(game).entries;
            int index = entries.FindIndex(e => e.name == name);
            return (index == -1 ? 0 : index + 1, entries.Count);
        }

        private static EntryList Load(GameKind game)
        {
            string json = PlayerPrefs.GetString(PrefKeyPrefix + game.ToServerId(), string.Empty);
            if (string.IsNullOrEmpty(json))
                return new EntryList();

            return JsonUtility.FromJson<EntryList>(json) ?? new EntryList();
        }

        private static void Save(GameKind game, EntryList list)
        {
            PlayerPrefs.SetString(PrefKeyPrefix + game.ToServerId(), JsonUtility.ToJson(list));
        }
    }
}
