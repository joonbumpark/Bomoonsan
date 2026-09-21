using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    /// <summary>
    /// 매치로 생성되는 아이템 블록의 종류.
    /// </summary>
    public enum ItemType
    {
        None,
        /// <summary>발동 시 자신이 속한 가로 한 줄(행) 전체를 지운다.</summary>
        LineHorizontal,
        /// <summary>발동 시 자신이 속한 세로 한 줄(열) 전체를 지운다.</summary>
        LineVertical,
        /// <summary>
        /// 발동 시 한 색상의 타일을 화면 전체에서 지운다. 드래그로 다른 타일과 스왑해서
        /// 발동하면 그 상대 타일의 색을 지우고(Match3GameManager.TrySwap), 탭으로 혼자
        /// 발동하면 자기 자신의 색을 지운다.
        /// </summary>
        ColorBomb,
        /// <summary>발동 시 자신을 중심으로 3x3 영역을 폭발시켜 지운다.</summary>
        AreaBomb
    }

    /// <summary>
    /// 한 번의 매치로 함께 지워지는 칸들의 묶음. 매치 모양에 따라 특정 칸이 지워지는 대신
    /// 아이템 블록으로 바뀌어야 하면 SpawnItem/SpawnCell에 그 정보가 담긴다.
    /// </summary>
    public sealed class MatchGroup
    {
        public readonly HashSet<Vector2Int> Cells;
        public readonly int ColorType;
        public readonly ItemType SpawnItem;
        public readonly Vector2Int SpawnCell;

        public MatchGroup(HashSet<Vector2Int> cells, int colorType, ItemType spawnItem, Vector2Int spawnCell)
        {
            Cells = cells;
            ColorType = colorType;
            SpawnItem = spawnItem;
            SpawnCell = spawnCell;
        }
    }

    /// <summary>
    /// 3매치 퍼즐의 순수 로직(그리드 상태, 매치 판정, 중력/리필, 아이템 블록)을 담당하는 클래스.
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
        private readonly ItemType[,] items;
        private readonly System.Random rng;

        public Match3Board(int width, int height, int typeCount, int? seed = null)
        {
            Width = width;
            Height = height;
            TypeCount = typeCount;
            grid = new int[width, height];
            items = new ItemType[width, height];
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            FillInitialBoard();
        }

        public int GetType(int col, int row) => grid[col, row];

        public ItemType GetItem(int col, int row) => items[col, row];

        public void SetItem(int col, int row, ItemType item) => items[col, row] = item;

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
                    items[col, row] = ItemType.None;
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
            (items[a.x, a.y], items[b.x, b.y]) = (items[b.x, b.y], items[a.x, a.y]);
        }

        /// <summary>가로/세로로 3개 이상 연속된 칸들을 모두 찾아 반환한다(호환용, 모양/아이템 정보 없음).</summary>
        public HashSet<Vector2Int> FindMatches()
        {
            var result = new HashSet<Vector2Int>();
            foreach (var group in FindMatchGroups())
                result.UnionWith(group.Cells);
            return result;
        }

        /// <summary>
        /// 매치를 모양별로 묶어서 반환한다. 가로/세로 런이 서로 겹치는 칸을 공유하면 하나의
        /// 그룹으로 합쳐지며, 그룹의 모양에 따라 아이템 블록 생성 여부가 결정된다.
        ///
        /// - 4개짜리 한 줄(가로만 또는 세로만): 합쳐진 방향으로 한 줄을 지우는 아이템(LineHorizontal/LineVertical)
        /// - 5개 이상 한 줄(가로만 또는 세로만): 같은 색상을 화면 전체에서 지우는 아이템(ColorBomb)
        /// - 일자가 아닌 모양(가로 런과 세로 런이 하나라도 겹쳐서 생기는 코너/T/십자 등 어떤 모양이든):
        ///   3x3을 폭발시키는 아이템(AreaBomb). 겹치려면 두 런 모두 길이 3 이상이어야 하므로
        ///   전체 칸 수는 항상 5개 이상이 된다.
        ///
        /// preferredSpawnCell을 주면(스왑으로 매치를 만들었을 때, 플레이어가 드래그해서 옮긴
        /// 칸) 그 칸이 속한 그룹은 기본 위치(줄 가운데/교차점) 대신 그 칸에 아이템을 만든다.
        /// </summary>
        public List<MatchGroup> FindMatchGroups(Vector2Int? preferredSpawnCell = null)
        {
            var hRuns = new List<(int row, int start, int end)>();
            var vRuns = new List<(int col, int start, int end)>();

            for (int row = 0; row < Height; row++)
            {
                int runStart = 0;
                for (int col = 1; col <= Width; col++)
                {
                    bool same = col < Width && grid[col, row] == grid[runStart, row];
                    if (!same)
                    {
                        if (col - runStart >= 3)
                            hRuns.Add((row, runStart, col - 1));
                        runStart = col;
                    }
                }
            }

            for (int col = 0; col < Width; col++)
            {
                int runStart = 0;
                for (int row = 1; row <= Height; row++)
                {
                    bool same = row < Height && grid[col, row] == grid[col, runStart];
                    if (!same)
                    {
                        if (row - runStart >= 3)
                            vRuns.Add((col, runStart, row - 1));
                        runStart = row;
                    }
                }
            }

            var groups = new List<MatchGroup>();
            if (hRuns.Count == 0 && vRuns.Count == 0)
                return groups;

            // 유니온-파인드로, 셀을 공유하는 가로/세로 런들을 하나의 그룹으로 합친다.
            var parent = new Dictionary<Vector2Int, Vector2Int>();

            Vector2Int Find(Vector2Int v)
            {
                while (parent[v] != v)
                {
                    parent[v] = parent[parent[v]];
                    v = parent[v];
                }
                return v;
            }

            void EnsureCell(Vector2Int v)
            {
                if (!parent.ContainsKey(v))
                    parent[v] = v;
            }

            void Union(Vector2Int a, Vector2Int b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra != rb)
                    parent[ra] = rb;
            }

            foreach (var run in hRuns)
            {
                var first = new Vector2Int(run.start, run.row);
                EnsureCell(first);
                for (int c = run.start + 1; c <= run.end; c++)
                {
                    var cell = new Vector2Int(c, run.row);
                    EnsureCell(cell);
                    Union(first, cell);
                }
            }

            foreach (var run in vRuns)
            {
                var first = new Vector2Int(run.col, run.start);
                EnsureCell(first);
                for (int r = run.start + 1; r <= run.end; r++)
                {
                    var cell = new Vector2Int(run.col, r);
                    EnsureCell(cell);
                    Union(first, cell);
                }
            }

            // 그룹별로 셀과, 그 그룹에 기여한 가로/세로 런들을 모은다.
            var groupCells = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
            var groupHRuns = new Dictionary<Vector2Int, List<(int row, int start, int end)>>();
            var groupVRuns = new Dictionary<Vector2Int, List<(int col, int start, int end)>>();

            void AddCellToGroup(Vector2Int cell)
            {
                var root = Find(cell);
                if (!groupCells.TryGetValue(root, out var set))
                {
                    set = new HashSet<Vector2Int>();
                    groupCells[root] = set;
                }
                set.Add(cell);
            }

            foreach (var run in hRuns)
            {
                var root = Find(new Vector2Int(run.start, run.row));
                for (int c = run.start; c <= run.end; c++)
                    AddCellToGroup(new Vector2Int(c, run.row));

                if (!groupHRuns.TryGetValue(root, out var list))
                {
                    list = new List<(int, int, int)>();
                    groupHRuns[root] = list;
                }
                list.Add(run);
            }

            foreach (var run in vRuns)
            {
                var root = Find(new Vector2Int(run.col, run.start));
                for (int r = run.start; r <= run.end; r++)
                    AddCellToGroup(new Vector2Int(run.col, r));

                if (!groupVRuns.TryGetValue(root, out var list))
                {
                    list = new List<(int, int, int)>();
                    groupVRuns[root] = list;
                }
                list.Add(run);
            }

            foreach (var kv in groupCells)
            {
                var root = kv.Key;
                var cells = kv.Value;
                int colorType = grid[root.x, root.y];

                groupHRuns.TryGetValue(root, out var hList);
                groupVRuns.TryGetValue(root, out var vList);
                int hCount = hList?.Count ?? 0;
                int vCount = vList?.Count ?? 0;

                ItemType spawnItem = ItemType.None;
                Vector2Int spawnCell = root;

                if (hCount >= 1 && vCount >= 1)
                {
                    // 가로 런과 세로 런이 하나라도 겹치면 일자가 아닌 모양(코너/T/십자/그 이상)이다.
                    // 겹치려면 두 런 모두 길이 3 이상이어야 하므로 전체 칸 수는 항상 5개 이상이라
                    // 별도 개수 확인 없이 바로 3x3 폭탄으로 만든다.
                    spawnItem = ItemType.AreaBomb;
                    spawnCell = (hCount == 1 && vCount == 1)
                        ? new Vector2Int(vList[0].col, hList[0].row) // 런이 각각 하나뿐이면 교차점에 만든다.
                        : root; // 런이 여러 개 얽힌 복잡한 모양은 그룹 대표 칸에 만든다.
                }
                else if (hCount == 1)
                {
                    var run = hList[0];
                    spawnCell = new Vector2Int((run.start + run.end) / 2, run.row);
                    spawnItem = ItemForLineLength(run.end - run.start + 1, horizontal: true);
                }
                else if (vCount == 1)
                {
                    var run = vList[0];
                    spawnCell = new Vector2Int(run.col, (run.start + run.end) / 2);
                    spawnItem = ItemForLineLength(run.end - run.start + 1, horizontal: false);
                }

                // 플레이어가 드래그해서 옮긴 칸이 이 그룹 안에 있으면, 기본 위치 대신 그
                // 칸에 아이템을 만든다 - "내가 완성한 자리"에 아이템이 나오게 하기 위함.
                if (spawnItem != ItemType.None && preferredSpawnCell.HasValue && cells.Contains(preferredSpawnCell.Value))
                    spawnCell = preferredSpawnCell.Value;

                groups.Add(new MatchGroup(cells, colorType, spawnItem, spawnCell));
            }

            return groups;
        }

        private static ItemType ItemForLineLength(int length, bool horizontal)
        {
            if (length >= 5)
                return ItemType.ColorBomb;
            if (length == 4)
                return horizontal ? ItemType.LineHorizontal : ItemType.LineVertical;
            return ItemType.None;
        }

        /// <summary>
        /// 아이템 블록을 발동시켰을 때 지워질 칸들을 계산해 반환한다(자기 자신 포함).
        /// 실제로 칸을 비우는 것은 Clear()가 담당하며, 이 메서드는 대상 칸 집합만 계산한다.
        /// </summary>
        /// <summary>
        /// colorClearOverride를 주면(0 이상) ColorBomb가 자기 자신의 색 대신 그 색을 지운다.
        /// 스왑으로 발동될 때 "드래그해서 맞바꾼 상대 타일의 색"을 넘겨주기 위함이다 -
        /// Match3GameManager.TrySwap 참고.
        /// </summary>
        public HashSet<Vector2Int> ActivateItem(Vector2Int cell, int colorClearOverride = -1)
        {
            var result = new HashSet<Vector2Int> { cell };
            ItemType item = items[cell.x, cell.y];

            switch (item)
            {
                case ItemType.LineHorizontal:
                    for (int c = 0; c < Width; c++)
                        result.Add(new Vector2Int(c, cell.y));
                    break;

                case ItemType.LineVertical:
                    for (int r = 0; r < Height; r++)
                        result.Add(new Vector2Int(cell.x, r));
                    break;

                case ItemType.ColorBomb:
                    int color = colorClearOverride >= 0 ? colorClearOverride : grid[cell.x, cell.y];
                    for (int c = 0; c < Width; c++)
                        for (int r = 0; r < Height; r++)
                            if (grid[c, r] == color)
                                result.Add(new Vector2Int(c, r));
                    break;

                case ItemType.AreaBomb:
                    for (int dc = -1; dc <= 1; dc++)
                        for (int dr = -1; dr <= 1; dr++)
                        {
                            int c = cell.x + dc;
                            int r = cell.y + dr;
                            if (c >= 0 && c < Width && r >= 0 && r < Height)
                                result.Add(new Vector2Int(c, r));
                        }
                    break;
            }

            return result;
        }

        public void Clear(IEnumerable<Vector2Int> cells)
        {
            foreach (var cell in cells)
            {
                grid[cell.x, cell.y] = Empty;
                items[cell.x, cell.y] = ItemType.None;
            }
        }

        /// <summary>
        /// 빈 칸 아래로 타일을 떨어뜨린다(중력). 실제로 이동한 타일들의 (이전 위치 -> 새 위치) 목록을 반환한다.
        /// 타일에 아이템이 붙어 있었다면 아이템도 함께 이동한다.
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
                        items[col, writeRow] = items[col, row];
                        grid[col, row] = Empty;
                        items[col, row] = ItemType.None;
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
                    items[col, row] = ItemType.None;
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

        /// <summary>더 이상 가능한 수가 없을 때 보드를 다시 섞는다(최선을 다해 매치 없는 배치를 만든다).
        /// 타일에 붙어 있던 아이템도 타입과 함께 섞여서 사라지지 않는다.</summary>
        public void Shuffle()
        {
            var cells = new List<(int type, ItemType item)>(Width * Height);
            for (int col = 0; col < Width; col++)
                for (int row = 0; row < Height; row++)
                    cells.Add((grid[col, row], items[col, row]));

            int attempts = 0;
            do
            {
                ShuffleList(cells);
                int i = 0;
                for (int col = 0; col < Width; col++)
                {
                    for (int row = 0; row < Height; row++)
                    {
                        var (type, item) = cells[i++];
                        grid[col, row] = type;
                        items[col, row] = item;
                    }
                }

                attempts++;
            }
            while ((FindMatches().Count > 0 || !HasAnyValidMove()) && attempts < 200);
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
