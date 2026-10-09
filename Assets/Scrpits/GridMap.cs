using System.Collections;
using UnityEngine;

public enum TileType
{
    Empty,          // Różowa podłoga
    SolidWall,      // Ciemnoszara obramówka mapy (Krawędzie)
    Pillar,         // Niezniszczalny domek / filar
    Cookie,         // Zwykłe ciastko / Krasnal (1 wybuch)
    HeavyCookie,    // Gruba czekolada / Wafel (2 wybuchy)
    HoneyBlock,     // Blok miodu (Tworzy kałużę miodu)
    SodaBlock,      // Blok sody (Tworzy kałużę sody)
    HoneyPuddle,    // Kałuża miodu
    SodaPuddle      // Kałuża sody
}

public class GridMap : MonoBehaviour
{
    public static GridMap Instance;

    public const int Size = 17;

    [Header("Domyślny Sprite (Kwadrat)")]
    public Sprite squareSprite;

    [Header("Układ mapy")]
    public int forcedLayout = -1;
    public bool fillBorderLane = true;

    [Header("Gęstość bloków niszczalnych")]
    [Range(0.40f, 0.80f)] public float innerDestructibleDensity = 0.60f;

    [Header("Szanse na rodzaje bloków niszczalnych (%)")]
    [Range(0, 100)] public int cookieChance = 65;
    [Range(0, 100)] public int heavyCookieChance = 25;
    [Range(0, 100)] public int honeyBlockChance = 5;
    [Range(0, 100)] public int sodaBlockChance = 5;

    [Header("Czas trwania efektów (Sekundy)")]
    public float honeyPuddleDuration = 2.0f;
    public float sodaPuddleDuration = 2.0f;

    [Header("Opcjonalne własne grafiki (Sprites)")]
    public Sprite solidWallSprite;
    public Sprite pillarSprite;
    public Sprite cookieSprite;
    public Sprite heavyCookieSprite;
    public Sprite honeyBlockSprite;
    public Sprite sodaBlockSprite;
    public Sprite honeyPuddleSprite;
    public Sprite sodaPuddleSprite;

    [Header("Paleta Kolorów (Bomb It Style)")]
    public Color floorColor = new Color(0.95f, 0.78f, 0.88f);
    public Color borderWallColor = new Color(0.25f, 0.25f, 0.25f);
    public Color pillarColor = new Color(0.88f, 0.38f, 0.15f);
    public Color heavyCookieColor = new Color(0.45f, 0.22f, 0.08f);
    public Color cookieColor = new Color(0.98f, 0.55f, 0.75f);
    public Color honeyBlockColor = new Color(1f, 0.8f, 0.1f);
    public Color sodaBlockColor = new Color(0.15f, 0.65f, 0.95f);
    public Color honeyPuddleColor = new Color(1f, 0.9f, 0.45f);
    public Color sodaPuddleColor = new Color(0.55f, 0.88f, 1f);

    [Header("Start graczy (Rogi planszy 15x15)")]
    public Vector2Int[] spawnPositions = new Vector2Int[4]
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, Size - 2),
        new Vector2Int(Size - 2, 1),
        new Vector2Int(Size - 2, Size - 2)
    };

    private TileType[,] grid = new TileType[Size, Size];
    private SpriteRenderer[,] tileRenderers = new SpriteRenderer[Size, Size];
    private bool[,] bombs = new bool[Size, Size];
    private int[,] fire = new int[Size, Size];

    private Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    // Znak	Znaczenie
    /*
    #	niezniszczalny filar (pomarańczowy domek)
    C	środek 3x3 (na razie też filar, później wypluwacz rzeczy)
    B	zawsze blok niszczalny (pas przy borderze)
    .	losowo: blok niszczalny albo puste (szansa z innerDestructibleDensity)
    -	zawsze puste (miejsca startu)
    */
    private string[][] layouts =
{
    new string[]
    {
        "---BBBBBBBBB---",
        "-#.B###B#B##.#-",
        "-#....#.#....#-",
        "B#.##.#.#.##.BB",
        "B#....#.#....#B",
        "B#.##.....##.#B",
        "BB....CCC....BB",
        "BB.##.CCC.##.#B",
        "B#....CCC....#B",
        "B#.##.....##.BB",
        "BB....#.#....#B",
        "B#.##.#.#.##.#B",
        "-#....#.#....#-",
        "-#.#########.#-",
        "---BBBBBBBBB---"
    },
    new string[]
    {
        "---BBBBBBBBB---",
        "-#.##.###.##.#-",
        "-#...........#-",
        "B#.#B.###.B#.#B",
        "B#...#...#...#B",
        "B#.#B.#.#.B#.#B",
        "B#....CCC....#B",
        "B#.##.CCC.##.#B",
        "B#....CCC....#B",
        "B#.##.#.#.##.#B",
        "B#...B...B...#B",
        "B#.##.###.##.#B",
        "-#...........#-",
        "-#.##.###.##.#-",
        "---BBBBBBBBB---"
    },
    new string[]
    {
        "---BBBBBBBBB---",
        "-######.######-",
        "-#...........#-",
        "B#.###BB####.BB",
        "B#.#.......#.#B",
        "BB.#.#####.#.#B",
        "B#...#CCC#...BB",
        "B###.#CCC#.###B",
        "BB...#CCC#...#B",
        "B#.#.#####.#.#B",
        "B#.#.......#.BB",
        "BB.#########.#B",
        "-#...........#-",
        "-######.######-",
        "---BBBBBBBBB---"
    },
    new string[]
    {
        "---BBBBBBBBB---",
        "-#.##..#..##.#-",
        "-#.....#.....#-",
        "B#.B##.#.###.#B",
        "B##.........B#B",
        "BBB.######..##B",
        "B#...#CCC#...#B",
        "B###.#CCC#.###B",
        "BB...#CCC#...BB",
        "B##..#####..#BB",
        "B##.........##B",
        "BB.###...###.#B",
        "-#.....#.....#-",
        "-#.##.....##.#-",
        "---BBBBBBBBB---"
    }
};

    void Awake()
    {
        Instance = this;
        BuildMap();
    }

    public void BuildMap()
    {
        int index = Random.Range(0, layouts.Length);

        if (forcedLayout >= 0 && forcedLayout < layouts.Length)
        {
            index = forcedLayout;
        }

        GenerateGridData(layouts[index]);
        CreateAllTileVisuals();
    }

    void GenerateGridData(string[] layout)
    {
        int inner = Size - 2;

        if (layout.Length != inner || layout[0].Length != inner)
        {
            Debug.LogError("Układ mapy musi mieć " + inner + " wierszy po " + inner + " znaków.");
            return;
        }

        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                bool isBorder = x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
                grid[x, y] = isBorder ? TileType.SolidWall : TileType.Empty;
            }
        }

        for (int row = 0; row < inner; row++)
        {
            for (int col = 0; col < inner; col++)
            {
                int x = col + 1;
                int y = Size - 2 - row;
                grid[x, y] = TileFromSymbol(layout[row][col], x, y);
            }
        }
    }

    TileType TileFromSymbol(char symbol, int x, int y)
    {
        switch (symbol)
        {
            case '#':
                return TileType.Pillar;

            case 'C':
                return TileType.Pillar;

            case 'B':
                return fillBorderLane ? PickBlock(x, y) : TileType.Empty;

            case '.':
                return Random.value < innerDestructibleDensity ? PickBlock(x, y) : TileType.Empty;
        }
        return TileType.Empty;
    }

    TileType PickBlock(int x, int y)
    {
        TileType type = GetRandomDestructibleBlock();

        if (type == TileType.HoneyBlock && HasNeighbor(x, y, TileType.SodaBlock))
        {
            return TileType.Cookie;
        }
        if (type == TileType.SodaBlock && HasNeighbor(x, y, TileType.HoneyBlock))
        {
            return TileType.Cookie;
        }
        return type;
    }

    bool HasNeighbor(int x, int y, TileType type)
    {
        foreach (Vector2Int direction in directions)
        {
            Vector2Int neighbor = new Vector2Int(x, y) + direction;

            if (InBounds(neighbor) && grid[neighbor.x, neighbor.y] == type)
            {
                return true;
            }
        }
        return false;
    }

    TileType GetRandomDestructibleBlock()
    {
        int totalWeight = cookieChance + heavyCookieChance + honeyBlockChance + sodaBlockChance;
        if (totalWeight <= 0) return TileType.Cookie;

        int roll = Random.Range(0, totalWeight);

        if (roll < cookieChance)
            return TileType.Cookie;

        roll -= cookieChance;
        if (roll < heavyCookieChance)
            return TileType.HeavyCookie;

        roll -= heavyCookieChance;
        if (roll < honeyBlockChance)
            return TileType.HoneyBlock;

        return TileType.SodaBlock;
    }

    void CreateAllTileVisuals()
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                if (tileRenderers[x, y] == null)
                {
                    GameObject tile = new GameObject($"Tile_{x}_{y}");
                    tile.transform.parent = transform;
                    tile.transform.position = new Vector3(x, y, 0);

                    SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();
                    tileRenderers[x, y] = sr;
                }

                UpdateTileVisual(x, y);
            }
        }
    }

    void UpdateTileVisual(int x, int y)
    {
        TileType type = grid[x, y];
        SpriteRenderer sr = tileRenderers[x, y];
        if (sr == null) return;

        sr.sprite = squareSprite;
        sr.color = Color.white;

        switch (type)
        {
            case TileType.SolidWall:
                if (solidWallSprite != null) sr.sprite = solidWallSprite;
                else sr.color = borderWallColor;
                sr.sortingOrder = 3;
                break;

            case TileType.Pillar:
                if (pillarSprite != null) sr.sprite = pillarSprite;
                else sr.color = pillarColor;
                sr.sortingOrder = 3;
                break;

            case TileType.Cookie:
                if (cookieSprite != null) sr.sprite = cookieSprite;
                else sr.color = cookieColor;
                sr.sortingOrder = 2;
                break;

            case TileType.HeavyCookie:
                if (heavyCookieSprite != null) sr.sprite = heavyCookieSprite;
                else sr.color = heavyCookieColor;
                sr.sortingOrder = 2;
                break;

            case TileType.HoneyBlock:
                if (honeyBlockSprite != null) sr.sprite = honeyBlockSprite;
                else sr.color = honeyBlockColor;
                sr.sortingOrder = 2;
                break;

            case TileType.SodaBlock:
                if (sodaBlockSprite != null) sr.sprite = sodaBlockSprite;
                else sr.color = sodaBlockColor;
                sr.sortingOrder = 2;
                break;

            case TileType.HoneyPuddle:
                if (honeyPuddleSprite != null) sr.sprite = honeyPuddleSprite;
                else sr.color = honeyPuddleColor;
                sr.sortingOrder = 0;
                break;

            case TileType.SodaPuddle:
                if (sodaPuddleSprite != null) sr.sprite = sodaPuddleSprite;
                else sr.color = sodaPuddleColor;
                sr.sortingOrder = 0;
                break;

            default:
                sr.color = floorColor;
                sr.sortingOrder = 0;
                break;
        }
    }

    public void DestroyBlock(Vector2Int cell)
    {
        if (!InBounds(cell)) return;

        TileType current = grid[cell.x, cell.y];

        switch (current)
        {
            case TileType.Cookie:
                grid[cell.x, cell.y] = TileType.Empty;
                UpdateTileVisual(cell.x, cell.y);
                break;
            case TileType.HeavyCookie:
                grid[cell.x, cell.y] = TileType.Cookie;
                UpdateTileVisual(cell.x, cell.y);
                break;
            case TileType.HoneyBlock:
                SpawnPuddle(cell, TileType.HoneyPuddle, honeyPuddleDuration);
                break;
            case TileType.SodaBlock:
                SpawnPuddle(cell, TileType.SodaPuddle, sodaPuddleDuration);
                break;
        }
    }

    void SpawnPuddle(Vector2Int cell, TileType puddleType, float duration)
    {
        grid[cell.x, cell.y] = puddleType;
        UpdateTileVisual(cell.x, cell.y);
        StartCoroutine(RemovePuddleAfterTime(cell, duration));
    }

    IEnumerator RemovePuddleAfterTime(Vector2Int cell, float duration)
    {
        yield return new WaitForSeconds(duration);

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

    public void SetBomb(Vector2Int cell, bool hasBomb)
    {
        bombs[cell.x, cell.y] = hasBomb;
    }

    public void AddFire(Vector2Int cell)
    {
        fire[cell.x, cell.y]++;
    }

    public void RemoveFire(Vector2Int cell)
    {
        fire[cell.x, cell.y]--;
    }

    public bool IsOnFire(Vector2Int cell)
    {
        return InBounds(cell) && fire[cell.x, cell.y] > 0;
    }
}