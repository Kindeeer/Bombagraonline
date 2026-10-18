using System.Collections.Generic;
using UnityEngine;

// Losuje przedmioty z Tabeli przedmiotów: po zniszczeniu bloku i na prośbę fontanny.
public class PickupSpawner : MonoBehaviour
{
    public static PickupSpawner Instance { get; private set; }

    [Tooltip("Puste = wczytaj Assets/Resources/TabelaPrzedmiotow.")]
    public PickupTable table;

    GridMap subscribedMap;

    void OnEnable()
    {
        // OnEnable, a nie Awake: wraca też po rekompilacji w trakcie gry
        Instance = this;

        if (table == null)
        {
            table = Resources.Load<PickupTable>(PickupTable.ResourcePath);
        }

        if (table == null)
        {
            Debug.LogWarning("PickupSpawner: brak Tabeli przedmiotów (Assets/Resources/" + PickupTable.ResourcePath + "). Przedmioty nie będą wypadać.", this);
        }
    }

    void Start()
    {
        subscribedMap = GridMap.Instance;

        if (subscribedMap != null)
        {
            subscribedMap.BlockDestroyed += OnBlockDestroyed;
        }
    }

    void OnDestroy()
    {
        if (subscribedMap != null)
        {
            subscribedMap.BlockDestroyed -= OnBlockDestroyed;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    void OnBlockDestroyed(Vector2Int cell, TileType type)
    {
        if (table == null || Random.value >= table.blockDropChance)
        {
            return;
        }

        SpawnOnGround(table.PickFromBlocks(), cell);
    }

    public PickupDefinition PickFromFountain()
    {
        return table != null ? table.PickFromFountain() : null;
    }

    public PickupItem SpawnOnGround(PickupDefinition definition, Vector2Int cell)
    {
        if (definition == null || PickupRegistry.Has(cell))
        {
            return null;
        }

        PickupItem item = new GameObject("Pickup_" + definition.name).AddComponent<PickupItem>();
        item.InitOnGround(definition, cell, table != null ? table.fireImmuneSeconds : 0.7f);
        return item;
    }

    public PickupItem LaunchBubble(PickupDefinition definition, Vector2Int cell, Vector3 from, float flightTime, float arcHeight)
    {
        if (definition == null || PickupRegistry.Has(cell))
        {
            return null;
        }

        PickupItem item = new GameObject("Bubble_" + definition.name).AddComponent<PickupItem>();
        item.InitInFlight(definition, cell, from, flightTime, arcHeight);
        return item;
    }

    // Wolne pole podłogi: bez bloku, bomby, ognia i innego przedmiotu, z przejściem do któregoś sąsiada.
    public bool TryFindFreeCell(out Vector2Int result)
    {
        GridMap map = GridMap.Instance;
        List<Vector2Int> candidates = new List<Vector2Int>();

        if (map != null)
        {
            for (int x = 1; x < GridMap.Width - 1; x++)
            {
                for (int y = 1; y < GridMap.Height - 1; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    if (map.GetTileType(cell) != TileType.Empty
                        || BombRegistry.HasBomb(cell)
                        || map.IsOnFire(cell)
                        || PickupRegistry.Has(cell)
                        || !HasOpenNeighbor(map, cell))
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }
            }
        }

        if (candidates.Count == 0)
        {
            result = default;
            return false;
        }

        result = candidates[Random.Range(0, candidates.Count)];
        return true;
    }

    static bool HasOpenNeighbor(GridMap map, Vector2Int cell)
    {
        return map.CanEnter(cell + Vector2Int.up)
            || map.CanEnter(cell + Vector2Int.down)
            || map.CanEnter(cell + Vector2Int.left)
            || map.CanEnter(cell + Vector2Int.right);
    }
}
