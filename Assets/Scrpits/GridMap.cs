using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum TileType
{
    Empty,          // Podłoga
    SolidWall,      // Obramówka mapy
    Pillar,         // Niezniszczalny filar
    Cookie,         // Zwykłe ciastko (1 wybuch)
    HeavyCookie,    // Gruba czekolada (2 wybuchy)
    HoneyBlock,     // Blok miodu
    SodaBlock,      // Blok sody
    HoneyPuddle,    // Kałuża miodu
    SodaPuddle      // Kałuża sody
}

public class GridMap : MonoBehaviour
{
    public static GridMap Instance;

    public const int Size = 17;

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
    public TileBase noSpawnTile; // Namaluj nim respy na Tilemapie!

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
    public Vector2Int[] spawnPositions = new Vector2Int[4]
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, Size - 2),
        new Vector2Int(Size - 2, 1),
        new Vector2Int(Size - 2, Size - 2)
    };

    private TileType[,] grid = new TileType[Size, Size];
    private bool[,] reservedCells = new bool[Size, Size]; // Tablica zablokowanych pól
    private bool[,] bombs = new bool[Size, Size];
    private int[,] fire = new int[Size, Size];
    private int[,] puddleToken = new int[Size, Size];
    private int nextPuddleToken = 0;
    private float[,] blockImmuneUntil = new float[Size, Size];

    public event System.Action<Vector2Int, TileType> BlockDestroyed;

    private Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    void Awake()
    {
        Instance = this;
        BuildMapFromTilemap();
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
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Vector3Int tilePos = new Vector3Int(x, y, 0);
                TileBase currentTile = blocksTilemap.GetTile(tilePos);

                if (currentTile == borderTile) grid[x, y] = TileType.SolidWall;
                else if (currentTile == pillarTile) grid[x, y] = TileType.Pillar;
                else if (currentTile == cookieTile) grid[x, y] = TileType.Cookie;
                else if (currentTile == heavyCookieTile) grid[x, y] = TileType.HeavyCookie;
                else if (currentTile == honeyBlockTile) grid[x, y] = TileType.HoneyBlock;
                else if (currentTile == sodaBlockTile) grid[x, y] = TileType.SodaBlock;
                else if (currentTile == noSpawnTile)
                {
                    // Zapamiętaj pole jako zarezerwowane i wyczyść widok kafelka-wskaźnika
                    grid[x, y] = TileType.Empty;
                    reservedCells[x, y] = true;
                    blocksTilemap.SetTile(tilePos, null);
                }
                else grid[x, y] = TileType.Empty;
            }
        }

        // 2. Wypełnij wolne miejsce blokami
        if (fillEmptyPathsWithBlocks)
        {
            GenerateDestructibleBlocks();
        }
    }

    void GenerateDestructibleBlocks()
    {
        for (int x = 1; x < Size - 1; x++)
        {
            for (int y = 1; y < Size - 1; y++)
            {
                // Nie stawiaj bloków na polach zarezerwowanych kafelkiem NoSpawn oraz wokół pozycji startowych
                if (grid[x, y] == TileType.Empty && !reservedCells[x, y] && !IsPlayerSpawnArea(x, y))
                {
                    if (Random.value < blockSpawnChance)
                    {
                        TileType randomBlock = PickBlock(x, y);
                        grid[x, y] = randomBlock;
                        UpdateTileVisual(x, y);
                    }
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
        foreach (Vector2Int direction in directions)
        {
            Vector2Int neighbor = new Vector2Int(x, y) + direction;
            if (InBounds(neighbor) && grid[neighbor.x, neighbor.y] == type)
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
        TileType type = grid[x, y];
        TileBase blockTile = null;
        TileBase puddleTile = null;

        switch (type)
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

    void SetTileIfDifferent(Tilemap tilemap, Vector3Int position, TileBase tile)
    {
        if (tilemap.GetTile(position) != tile)
        {
            tilemap.SetTile(position, tile);
        }
    }

    public void DestroyBlock(Vector2Int cell)
    {
        if (!InBounds(cell)) return;
        if (Time.time < blockImmuneUntil[cell.x, cell.y]) return;

        TileType current = grid[cell.x, cell.y];

        switch (current)
        {
            case TileType.Cookie:
                grid[cell.x, cell.y] = TileType.Empty;
                UpdateTileVisual(cell.x, cell.y);
                BlockDestroyed?.Invoke(cell, current);
                break;
            case TileType.HeavyCookie:
                grid[cell.x, cell.y] = TileType.Cookie;
                blockImmuneUntil[cell.x, cell.y] = Time.time + blockHitCooldown;
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

        foreach (Vector2Int direction in directions)
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
        TileType type = grid[cell.x, cell.y];
        return type == TileType.Empty || type == TileType.HoneyPuddle || type == TileType.SodaPuddle;
    }

    void SetPuddle(Vector2Int cell, TileType puddleType, float duration)
    {
        nextPuddleToken++;
        int token = nextPuddleToken;

        puddleToken[cell.x, cell.y] = token;
        grid[cell.x, cell.y] = puddleType;
        UpdateTileVisual(cell.x, cell.y);

        StartCoroutine(RemovePuddleAfterTime(cell, duration, token));
    }

    IEnumerator RemovePuddleAfterTime(Vector2Int cell, float duration, int token)
    {
        yield return new WaitForSeconds(duration);

        if (puddleToken[cell.x, cell.y] != token) yield break;

        if (grid[cell.x, cell.y] == TileType.HoneyPuddle || grid[cell.x, cell.y] == TileType.SodaPuddle)
        {
            grid[cell.x, cell.y] = TileType.Empty;
            UpdateTileVisual(cell.x, cell.y);
        }
    }

    public bool CanEnter(Vector2Int cell)
    {
        if (!InBounds(cell) || bombs[cell.x, cell.y]) return false;
        return !IsWall(cell);
    }

    public bool IsWall(Vector2Int cell)
    {
        if (!InBounds(cell)) return true;
        TileType type = grid[cell.x, cell.y];
        return type == TileType.SolidWall || type == TileType.Pillar ||
               type == TileType.Cookie || type == TileType.HeavyCookie ||
               type == TileType.HoneyBlock || type == TileType.SodaBlock;
    }

    public TileType GetTileType(Vector2Int cell)
    {
        return InBounds(cell) ? grid[cell.x, cell.y] : TileType.SolidWall;
    }

    bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < Size && cell.y < Size;
    }

    public void SetBomb(Vector2Int cell, bool hasBomb) => bombs[cell.x, cell.y] = hasBomb;
    public void AddFire(Vector2Int cell) => fire[cell.x, cell.y]++;
    public void RemoveFire(Vector2Int cell) => fire[cell.x, cell.y]--;
    public bool IsOnFire(Vector2Int cell) => InBounds(cell) && fire[cell.x, cell.y] > 0;
}