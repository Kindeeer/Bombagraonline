using System.Collections.Generic;
using UnityEngine;

public static class BombRegistry
{
    static readonly Dictionary<Vector2Int, Bomb> bombs = new Dictionary<Vector2Int, Bomb>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        bombs.Clear();
    }

    public static void Register(Bomb bomb)
    {
        bombs[bomb.Cell] = bomb;
    }

    public static void Unregister(Bomb bomb)
    {
        if (bombs.TryGetValue(bomb.Cell, out Bomb current) && current == bomb)
        {
            bombs.Remove(bomb.Cell);
        }
    }

    public static bool HasBomb(Vector2Int cell)
    {
        return bombs.ContainsKey(cell);
    }

    public static bool TryGet(Vector2Int cell, out Bomb bomb)
    {
        return bombs.TryGetValue(cell, out bomb);
    }
}
