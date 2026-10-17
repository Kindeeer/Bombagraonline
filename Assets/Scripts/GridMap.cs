using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum TileType
{
    Empty,          // Podłoga
    SolidWall,      // Obramówka mapy
    Pillar,         // Filar w środku mapy (niszczy go tylko młotek)
    Cookie,         // Zwykłe ciastko (1 wybuch)
    HeavyCookie,    // Gruba czekolada (2 wybuchy)
    HoneyBlock,     // Blok miodu
    SodaBlock,      // Blok sody
    HoneyPuddle,    // Kałuża miodu
    SodaPuddle,     // Kałuża sody
    Landmark        // Stały obiekt, np. fontanna (nic go nie niszczy)
}

public static class TileRules
{
    public static bool IsIndestructible(TileType type)
    {
        return type == TileType.SolidWall || type == TileType.Pillar || type == TileType.Landmark;
    }

    public static bool IsDestructible(TileType type)
    {
        return type == TileType.Cookie
            || type == TileType.HeavyCookie
            || type == TileType.HoneyBlock
            || type == TileType.SodaBlock;
    }

    public static bool IsPuddle(TileType type)
    {
        return type == TileType.HoneyPuddle || type == TileType.SodaPuddle;
    }

    public static bool BlocksMovement(TileType type)
    {
        return IsIndestructible(type) || IsDestructible(type);
    }
}

public class GridMap : MonoBehaviour
{
    public static GridMap Instance;

    public const int Size = 15;
    const int CellCount = Size * Size;

    [Header("Domyślny Sprite (Używany dla Bomb i Płomieni)")]
    public Sprite squareSprite;

    [Header("Referencja do Tilemapy")]
    public Tilemap blocksTilemap;

    [Header("Opcjonalna Tilemapa kałuż (rysowana pod graczem)")]
    public Tilemap effectsTilemap;

    [Header("Kafelki z folderu Assets/Tiles")]
    public TileBase borderTile;
    public TileBase pillarTile;
    public TileBase cookieTile;
    public TileBase heavyCookieTile;
    public TileBase honeyBlockTile;
    public TileBase sodaBlockTile;
    public TileBase honeyPuddleTile;
    public TileBase sodaPuddleTile;

    [Header("Kafelek bezpiecznej strefy (Brak spawnu bloków)")]
    public TileBase noSpawnTile;

    [Header("Generowanie bloków niszczalnych w korytarzach")]
    public bool fillEmptyPathsWithBlocks = true;
    [Range(0.00f, 1.00f)] public float blockSpawnChance = 0.70f;

    [Header("Szanse na rodzaje bloków niszczalnych (%)")]
    [Range(0, 100)] public int cookieChance = 65;
    [Range(0, 100)] public int heavyCookieChance = 25;
    [Range(0, 100)] public int honeyBlockChance = 5;
    [Range(0, 100)] public int sodaBlockChance = 5;

    [Header("Czas trwania efektów (Sekundy)")]
    public float honeyPuddleDuration = 2.0f;
    public float sodaPuddleDuration = 2.0f;

    [Header("Ochrona bloku po trafieniu (Sekundy)")]
    public float blockHitCooldown = 0.5f;

    [Header("Start graczy (Rogi planszy)")]
    public Vector2Int[] spawnPositions =
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, Size - 2),
        new Vector2Int(Size - 2, 1),
        new Vector2Int(Size - 2, Size - 2)
    };

    // Tablice jednowymiarowe (szybsze niż [,]). Pole (x, y) leży pod indeksem y * Size + x.
    readonly TileType[] grid = new TileType[CellCount];
    readonly bool[] reservedCells = new bool[CellCount];
    readonly bool[] bombs = new bool[CellCount];
    readonly int[] fire = new int[CellCount];
    readonly int[] puddleToken = new int[CellCount];
    readonly float[] blockImmuneUntil = new float[CellCount];
    int nextPuddleToken;

    // false po przeładowaniu skryptów w trakcie gry (Unity czyści wtedy zwykłe pola i statyczne)
    bool mapReady;

    public event System.Action<Vector2Int, TileType> BlockDestroyed;

    static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    static int Index(int x, int y) => y * Size + x;
    static int Index(Vector2Int cell) => cell.y * Size + cell.x;

    void Awake()
    {
        Instance = this;
        BuildMapFromTilemap();
    }

    // Po rekompilacji w trakcie gry Awake nie wraca, ale OnEnable tak: podpinamy się jeszcze raz
    // i odczytujemy aktualny stan mapy z tilemapy (bez losowania nowych bloków).
    void OnEnable()
    {
        Instance = this;

        if (!mapReady && blocksTilemap != null)
        {
            ReadTilemap();
            mapReady = true;
        }
    }

    public Vector3 GridToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));
    }

    public void BuildMapFromTilemap()
    {
        if (blocksTilemap == null)
        {
            Debug.LogError("Brak przypisanej Blocks Tilemap w skrypcie GridMap!");
            return;
        }

        // 1. Odczytaj kafelki z namalowanej Tilemapy
        ReadTilemap();
        mapReady = true;

        // 2. Wypełnij wolne miejsce blokami
        if (fillEmptyPathsWithBlocks)
        {
            GenerateDestructibleBlocks();
        }
    }

    void ReadTilemap()
    {
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int i = Index(x, y);
                Vector3Int tilePos = new Vector3Int(x, y, 0);
                TileBase currentTile = blocksTilemap.GetTile(tilePos);

                if (currentTile == borderTile) grid[i] = TileType.SolidWall;
                else if (currentTile == pillarTile) grid[i] = TileType.Pillar;
                else if (currentTile == cookieTile) grid[i] = TileType.Cookie;
                else if (currentTile == heavyCookieTile) grid[i] = TileType.HeavyCookie;
                else if (currentTile == honeyBlockTile) grid[i] = TileType.HoneyBlock;
                else if (currentTile == sodaBlockTile) grid[i] = TileType.SodaBlock;
                else if (currentTile == noSpawnTile)
                {
                    // Zapamiętaj pole jako zarezerwowane i wyczyść widok kafelka-wskaźnika
                    grid[i] = TileType.Empty;
                    reservedCells[i] = true;
                    blocksTilemap.SetTile(tilePos, null);
                }
                else grid[i] = TileType.Empty;
            }
        }
    }

    void GenerateDestructibleBlocks()
    {
        for (int y = 1; y < Size - 1; y++)
        {
            for (int x = 1; x < Size - 1; x++)
            {
                int i = Index(x, y);

                // Nie stawiaj bloków na polach zarezerwowanych kafelkiem NoSpawn oraz wokół pozycji startowych
                if (grid[i] != TileType.Empty || reservedCells[i] || IsPlayerSpawnArea(x, y))
                {
                    continue;
                }

                if (Random.value < blockSpawnChance)
                {
                    grid[i] = PickBlock(x, y);
                    UpdateTileVisual(x, y);
                }
            }
        }
    }

    bool IsPlayerSpawnArea(int x, int y)
    {
        foreach (Vector2Int spawn in spawnPositions)
        {
            if (Mathf.Abs(spawn.x - x) <= 1 && Mathf.Abs(spawn.y - y) <= 1)
            {
                return true;
            }
        }
        return false;
    }

    TileType PickBlock(int x, int y)
    {
        TileType type = GetRandomDestructibleBlock();

        if (type == TileType.HoneyBlock && HasNeighbor(x, y, TileType.SodaBlock))
            return TileType.Cookie;
        if (type == TileType.SodaBlock && HasNeighbor(x, y, TileType.HoneyBlock))
            return TileType.Cookie;

        return type;
    }

    bool HasNeighbor(int x, int y, TileType type)
    {
        foreach (Vector2Int direction in Directions)
        {
            Vector2Int neighbor = new Vector2Int(x, y) + direction;
            if (InBounds(neighbor) && grid[Index(neighbor)] == type)
                return true;
        }
        return false;
    }

    TileType GetRandomDestructibleBlock()
    {
        int totalWeight = cookieChance + heavyCookieChance + honeyBlockChance + sodaBlockChance;
        if (totalWeight <= 0) return TileType.Cookie;

        int roll = Random.Range(0, totalWeight);

        if (roll < cookieChance) return TileType.Cookie;
        roll -= cookieChance;

        if (roll < heavyCookieChance) return TileType.HeavyCookie;
        roll -= heavyCookieChance;

        if (roll < honeyBlockChance) return TileType.HoneyBlock;

        return TileType.SodaBlock;
    }
    public void UpdateTileVisual(int x, int y)
    {
        Vector3Int tilePos = new Vector3Int(x, y, 0);
        TileBase blockTile = null;
        TileBase puddleTile = null;

        switch (grid[Index(x, y)])
        {
            case TileType.SolidWall: blockTile = borderTile; break;
            case TileType.Pillar: blockTile = pillarTile; break;
            case TileType.Cookie: blockTile = cookieTile; break;
            case TileType.HeavyCookie: blockTile = heavyCookieTile; break;
            case TileType.HoneyBlock: blockTile = honeyBlockTile; break;
            case TileType.SodaBlock: blockTile = sodaBlockTile; break;
            case TileType.HoneyPuddle: puddleTile = honeyPuddleTile; break;
            case TileType.SodaPuddle: puddleTile = sodaPuddleTile; break;
        }

        if (effectsTilemap == null)
        {
            SetTileIfDifferent(blocksTilemap, tilePos, blockTile != null ? blockTile : puddleTile);
            return;
        }

        SetTileIfDifferent(blocksTilemap, tilePos, blockTile);
        SetTileIfDifferent(effectsTilemap, tilePos, puddleTile);
    }

    static void SetTileIfDifferent(Tilemap tilemap, Vector3Int position, TileBase tile)
    {
        if (tilemap.GetTile(position) != tile)
        {
            tilemap.SetTile(position, tile);
        }
    }

    public void DestroyBlock(Vector2Int cell)
    {
        if (!InBounds(cell)) return;

        int i = Index(cell);
        if (Time.time < blockImmuneUntil[i]) return;

        TileType current = grid[i];

        switch (current)
        {
            case TileType.Cookie:
                grid[i] = TileType.Empty;
                UpdateTileVisual(cell.x, cell.y);
                BlockDestroyed?.Invoke(cell, current);
                break;
            case TileType.HeavyCookie:
                grid[i] = TileType.Cookie;
                blockImmuneUntil[i] = Time.time + blockHitCooldown;
                UpdateTileVisual(cell.x, cell.y);
                break;
            case TileType.HoneyBlock:
                SpreadPuddle(cell, TileType.HoneyPuddle, honeyPuddleDuration);
                BlockDestroyed?.Invoke(cell, current);
                break;
            case TileType.SodaBlock:
                SpreadPuddle(cell, TileType.SodaPuddle, sodaPuddleDuration);
                BlockDestroyed?.Invoke(cell, current);
                break;
        }
    }

    void SpreadPuddle(Vector2Int center, TileType puddleType, float duration)
    {
        SetPuddle(center, puddleType, duration);

        foreach (Vector2Int direction in Directions)
        {
            Vector2Int neighbor = center + direction;
            if (InBounds(neighbor) && CanHoldPuddle(neighbor))
            {
                SetPuddle(neighbor, puddleType, duration);
            }
        }
    }

    bool CanHoldPuddle(Vector2Int cell)
    {
        TileType type = grid[Index(cell)];
        return type == TileType.Empty || TileRules.IsPuddle(type);
    }

    void SetPuddle(Vector2Int cell, TileType puddleType, float duration)
    {
        nextPuddleToken++;
        int token = nextPuddleToken;
        int i = Index(cell);

        puddleToken[i] = token;
        grid[i] = puddleType;
        UpdateTileVisual(cell.x, cell.y);

        StartCoroutine(RemovePuddleAfterTime(cell, duration, token));
    }

    IEnumerator RemovePuddleAfterTime(Vector2Int cell, float duration, int token)
    {
        yield return new WaitForSeconds(duration);

        int i = Index(cell);
        if (puddleToken[i] != token) yield break;

        if (TileRules.IsPuddle(grid[i]))
        {
            grid[i] = TileType.Empty;
            UpdateTileVisual(cell.x, cell.y);
        }
    }

    // Zajmuje pole na stałe (np. fontanna): nic go nie niszczy, blokuje ruch i wybuchy, bez kafelka.
    public void ClaimCell(Vector2Int cell)
    {
        if (!InBounds(cell)) return;

        grid[Index(cell)] = TileType.Landmark;

        Vector3Int tilePos = new Vector3Int(cell.x, cell.y, 0);
        blocksTilemap.SetTile(tilePos, null);

        if (effectsTilemap != null)
        {
            effectsTilemap.SetTile(tilePos, null);
        }
    }

    // Młotek: rozbija od razu zwykłe bloki, grubą czekoladę i filary. Ramki i fontanny nie rusza.
    // Zwraca true, gdy coś rozbił.
    public bool SmashBlock(Vector2Int cell)
    {
        if (!InBounds(cell)) return false;

        int i = Index(cell);
        TileType type = grid[i];

        if (type == TileType.Pillar)
        {
            // filar nie daje przedmiotów, więc bez zdarzenia BlockDestroyed
            grid[i] = TileType.Empty;
            UpdateTileVisual(cell.x, cell.y);
            return true;
        }

        if (!TileRules.IsDestructible(type)) return false;

        if (type == TileType.HeavyCookie)
        {
            grid[i] = TileType.Cookie;
        }

        blockImmuneUntil[i] = 0f;
        DestroyBlock(cell);
        return true;
    }

    public bool CanEnter(Vector2Int cell)
    {
        return InBounds(cell) && !bombs[Index(cell)] && !TileRules.BlocksMovement(grid[Index(cell)]);
    }

    public bool IsWall(Vector2Int cell)
    {
        return !InBounds(cell) || TileRules.BlocksMovement(grid[Index(cell)]);
    }

    public TileType GetTileType(Vector2Int cell)
    {
        return InBounds(cell) ? grid[Index(cell)] : TileType.SolidWall;
    }

    static bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < Size && cell.y < Size;
    }

    public void SetBomb(Vector2Int cell, bool hasBomb)
    {
        if (InBounds(cell)) bombs[Index(cell)] = hasBomb;
    }

    public void AddFire(Vector2Int cell)
    {
        if (InBounds(cell)) fire[Index(cell)]++;
    }

    public void RemoveFire(Vector2Int cell)
    {
        if (InBounds(cell)) fire[Index(cell)]--;
    }

    public bool IsOnFire(Vector2Int cell) => InBounds(cell) && fire[Index(cell)] > 0;

    // Podgląd kolizji w czasie gry (Scene view albo Game view z włączonym przyciskiem Gizmos).
    // Czerwony: ramka, pomarańczowy: filar, żółty: blok do zniszczenia, niebieski: fontanna, czarny: bomba.
    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Vector3 size = new Vector3(0.9f, 0.9f, 0f);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int i = Index(x, y);
                TileType type = grid[i];

                if (bombs[i]) Gizmos.color = Color.black;
                else if (type == TileType.SolidWall) Gizmos.color = Color.red;
                else if (type == TileType.Pillar) Gizmos.color = new Color(1f, 0.5f, 0f);
                else if (type == TileType.Landmark) Gizmos.color = Color.cyan;
                else if (TileRules.IsDestructible(type)) Gizmos.color = Color.yellow;
                else continue;

                Gizmos.DrawWireCube(new Vector3(x + 0.5f, y + 0.5f, 0f), size);
            }
        }
    }
}