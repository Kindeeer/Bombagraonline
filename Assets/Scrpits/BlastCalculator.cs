using System.Collections.Generic;
using UnityEngine;

public class BlastResult
{
    public readonly List<Vector2Int> flameCells = new List<Vector2Int>();
    public readonly List<Vector2Int> blocksToDestroy = new List<Vector2Int>();
    public readonly List<Bomb> bombsToTrigger = new List<Bomb>();
}

public static class BlastCalculator
{
    static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public static BlastResult Calculate(Vector2Int origin, int range)
    {
        BlastResult result = new BlastResult();
        result.flameCells.Add(origin);

        foreach (Vector2Int direction in Directions)
        {
            for (int step = 1; step <= range; step++)
            {
                Vector2Int cell = origin + direction * step;
                TileType tile = GridMap.Instance.GetTileType(cell);

                if (TileRules.IsIndestructible(tile))
                {
                    break;
                }

                result.flameCells.Add(cell);

                if (TileRules.IsDestructible(tile))
                {
                    result.blocksToDestroy.Add(cell);
                    break;
                }

                if (BombRegistry.TryGet(cell, out Bomb other))
                {
                    result.bombsToTrigger.Add(other);
                    break;
                }
            }
        }

        return result;
    }
}
