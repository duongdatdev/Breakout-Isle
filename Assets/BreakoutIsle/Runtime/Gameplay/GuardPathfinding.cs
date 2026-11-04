using System.Collections.Generic;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public static class GuardPathfinding
    {
        public const float CellSize = 0.5f;
        private const int MaxVisited = 1600;
        private static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        public static bool FindPath(Vector2 start, Vector2 destination, LayerMask obstacles, List<Vector2> result)
        {
            if (result == null) return false;
            result.Clear();
            var startCell = ToCell(start);
            var goalCell = FindOpenCell(ToCell(destination), obstacles);
            if (goalCell == null) return false;
            if (!IsOpen(startCell, obstacles))
            {
                var openStart = FindOpenCell(startCell, obstacles);
                if (openStart == null) return false;
                startCell = openStart.Value;
            }

            var open = new List<Vector2Int> { startCell };
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var cost = new Dictionary<Vector2Int, float> { [startCell] = 0f };
            var goal = goalCell.Value;
            var visited = 0;
            while (open.Count > 0 && visited++ < MaxVisited)
            {
                var bestIndex = 0;
                var bestCell = open[0];
                var bestScore = cost[bestCell] + Heuristic(bestCell, goal);
                for (var i = 1; i < open.Count; i++)
                {
                    var candidate = open[i];
                    var score = cost[candidate] + Heuristic(candidate, goal);
                    if (score >= bestScore) continue;
                    bestIndex = i;
                    bestCell = candidate;
                    bestScore = score;
                }

                open.RemoveAt(bestIndex);
                if (bestCell == goal)
                {
                    Reconstruct(cameFrom, bestCell, startCell, result);
                    return result.Count > 0 || startCell == goal;
                }

                foreach (var offset in Neighbours)
                {
                    var next = bestCell + offset;
                    if (!IsOpen(next, obstacles)) continue;
                    if (offset.x != 0 && offset.y != 0 &&
                        (!IsOpen(bestCell + new Vector2Int(offset.x, 0), obstacles) ||
                         !IsOpen(bestCell + new Vector2Int(0, offset.y), obstacles))) continue;
                    var step = offset.x != 0 && offset.y != 0 ? 1.41421356f : 1f;
                    var nextCost = cost[bestCell] + step;
                    if (cost.TryGetValue(next, out var priorCost) && nextCost >= priorCost) continue;
                    cameFrom[next] = bestCell;
                    cost[next] = nextCost;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
            return false;
        }

        private static Vector2Int? FindOpenCell(Vector2Int origin, LayerMask obstacles)
        {
            if (IsOpen(origin, obstacles)) return origin;
            for (var radius = 1; radius <= 4; radius++)
            for (var x = -radius; x <= radius; x++)
            for (var y = -radius; y <= radius; y++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius) continue;
                var candidate = origin + new Vector2Int(x, y);
                if (IsOpen(candidate, obstacles)) return candidate;
            }
            return null;
        }

        private static bool IsOpen(Vector2Int cell, LayerMask obstacles)
        {
            if (obstacles.value == 0) return true;
            return Physics2D.OverlapCircle(ToWorld(cell), CellSize * 0.52f, obstacles) == null;
        }

        private static Vector2Int ToCell(Vector2 position) => new Vector2Int(
            Mathf.RoundToInt(position.x / CellSize), Mathf.RoundToInt(position.y / CellSize));

        private static Vector2 ToWorld(Vector2Int cell) => new Vector2(cell.x * CellSize, cell.y * CellSize);

        private static float Heuristic(Vector2Int a, Vector2Int b)
        {
            var dx = Mathf.Abs(a.x - b.x);
            var dy = Mathf.Abs(a.y - b.y);
            return Mathf.Max(dx, dy) + 0.41421356f * Mathf.Min(dx, dy);
        }

        private static void Reconstruct(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current, Vector2Int start, List<Vector2> result)
        {
            var reverse = new List<Vector2Int>();
            while (current != start && cameFrom.TryGetValue(current, out var prior))
            {
                reverse.Add(current);
                current = prior;
            }
            reverse.Reverse();
            foreach (var cell in reverse) result.Add(ToWorld(cell));
        }
    }
}
