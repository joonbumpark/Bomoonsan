using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    /// <summary>
    /// 3매치 퍼즐의 순수 로직(그리드 상태, 매치 판정, 중력/리필)을 담당하는 클래스.
    /// Unity 오브젝트나 렌더링에 대해서는 전혀 알지 못하며, Match3GameManager가 이 클래스를
    /// 감싸서 실제 화면에 보여주는 역할을 한다.
    /// </summary>
    public class Match3Board
    {
        public const int Empty = -1;

        public int Width { get; }
        public int Height { get; }
        public int TypeCount { get; }

        private readonly int[,] grid;
        private readonly System.Random rng;

        public Match3Board(int width, int height, int typeCount, int? seed = null)
        {
            Width = width;
            Height = height;
            TypeCount = typeCount;
            grid = new int[width, height];
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            FillInitialBoard();
        }

        public int GetType(int col, int row) => grid[col, row];

        /// <summary>초기 배치 시 처음부터 3매치가 만들어지지 않도록 채운다.</summary>
        private void FillInitialBoard()
        {
            for (int row = 0; row < Height; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    int type;
                    do
                    {
                        type = rng.Next(TypeCount);
                    }
                    while (WouldCreateInitialMatch(col, row, type));

                    grid[col, row] = type;
                }
            }
        }

        private bool WouldCreateInitialMatch(int col, int row, int type)
        {
            if (col >= 2 && grid[col - 1, row] == type && grid[col - 2, row] == type)
                return true;
            if (row >= 2 && grid[col, row - 1] == type && grid[col, row - 2] == type)
                return true;
            return false;
        }

        public bool AreAdjacent(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return dx + dy == 1;
        }

        public void Swap(Vector2Int a, Vector2Int b)
        {
            (grid[a.x, a.y], grid[b.x, b.y]) = (grid[b.x, b.y], grid[a.x, a.y]);
        }

        /// <summary>가로/세로로 3개 이상 연속된 칸들을 모두 찾아 반환한다.</summary>
        public HashSet<Vector2Int> FindMatches()
        {
            var matched = new HashSet<Vector2Int>();

            // 가로 매치
            for (int row = 0; row < Height; row++)
            {
                int runStart = 0;
                for (int col = 1; col <= Width; col++)
                {
                    bool same = col < Width && grid[col, row] == grid[runStart, row];
                    if (!same)
                    {
                        if (col - runStart >= 3)
                        {
                            for (int k = runStart; k < col; k++)
                                matched.Add(new Vector2Int(k, row));
                        }
                        runStart = col;
                    }
                }
            }

            // 세로 매치
            for (int col = 0; col < Width; col++)
            {
                int runStart = 0;
                for (int row = 1; row <= Height; row++)
                {
                    bool same = row < Height && grid[col, row] == grid[col, runStart];
                    if (!same)
                    {
                        if (row - runStart >= 3)
                        {
                            for (int k = runStart; k < row; k++)
                                matched.Add(new Vector2Int(col, k));
                        }
                        runStart = row;
                    }
                }
            }

            return matched;
        }

        public void Clear(IEnumerable<Vector2Int> cells)
        {
            foreach (var cell in cells)
                grid[cell.x, cell.y] = Empty;
        }

        /// <summary>
        /// 빈 칸 아래로 타일을 떨어뜨린다(중력). 실제로 이동한 타일들의 (이전 위치 -> 새 위치) 목록을 반환한다.
        /// </summary>
        public List<(Vector2Int from, Vector2Int to)> CollapseColumns()
        {
            var moves = new List<(Vector2Int, Vector2Int)>();

            for (int col = 0; col < Width; col++)
            {
                int writeRow = 0;
                for (int row = 0; row < Height; row++)
                {
                    if (grid[col, row] == Empty)
                        continue;

                    if (writeRow != row)
                    {
                        grid[col, writeRow] = grid[col, row];
                        grid[col, row] = Empty;
                        moves.Add((new Vector2Int(col, row), new Vector2Int(col, writeRow)));
                    }
                    writeRow++;
                }
            }

            return moves;
        }

        /// <summary>비어 있는 모든 칸을 새 랜덤 타일로 채우고, (위치, 타입) 목록을 반환한다.</summary>
        public List<(Vector2Int pos, int type)> RefillEmpties()
        {
            var spawned = new List<(Vector2Int, int)>();

            for (int col = 0; col < Width; col++)
            {
                for (int row = 0; row < Height; row++)
                {
                    if (grid[col, row] != Empty)
                        continue;

                    int type = rng.Next(TypeCount);
                    grid[col, row] = type;
                    spawned.Add((new Vector2Int(col, row), type));
                }
            }

            return spawned;
        }

        /// <summary>보드 안에 매치를 만들 수 있는 스왑이 하나라도 남아있는지 확인한다.</summary>
        public bool HasAnyValidMove()
        {
            for (int col = 0; col < Width; col++)
            {
                for (int row = 0; row < Height; row++)
                {
                    if (col + 1 < Width && WouldMatchIfSwapped(col, row, col + 1, row))
                        return true;
                    if (row + 1 < Height && WouldMatchIfSwapped(col, row, col, row + 1))
                        return true;
                }
            }
            return false;
        }

        private bool WouldMatchIfSwapped(int c1, int r1, int c2, int r2)
        {
            var a = new Vector2Int(c1, r1);
            var b = new Vector2Int(c2, r2);
            Swap(a, b);
            bool matched = FindMatches().Count > 0;
            Swap(a, b);
            return matched;
        }

        /// <summary>더 이상 가능한 수가 없을 때 보드를 다시 섞는다(최선을 다해 매치 없는 배치를 만든다).</summary>
        public void Shuffle()
        {
            var types = new List<int>(Width * Height);
            for (int col = 0; col < Width; col++)
                for (int row = 0; row < Height; row++)
                    types.Add(grid[col, row]);

            int attempts = 0;
            do
            {
                ShuffleList(types);
                int i = 0;
                for (int col = 0; col < Width; col++)
                    for (int row = 0; row < Height; row++)
                        grid[col, row] = types[i++];

                attempts++;
            }
            while ((FindMatches().Count > 0 || !HasAnyValidMove()) && attempts < 200);
        }

        private void ShuffleList(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
