using System.Collections;
using System.Collections.Generic;
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

    // Rozmiar siatki z ramką: plansza jest szeroka, bo poziomy telefon ma szeroki ekran, a pole zależy od liczby wierszy.
    // Po zmianie wygeneruj arenę od nowa: przycisk "Maluj arenę" w obiekcie GameArt.
    public const int Width = 17;
    public const int Height = 15;
    const int CellCount = Width * Height;

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

    [Header("Układy planszy (losowane co rundę)")]
    [Tooltip("Prefaby układów z Assets/Layouts (wpisuje je przycisk w obiekcie GameArt). Puste = używana jest tilemapa namalowana w scenie.")]
    public GameObject[] layouts;
    [Tooltip("Losowo odbija wylosowany układ w poziomie i w pionie, więc z jednego układu powstają cztery plansze.")]
    public bool randomFlip = true;
    [Tooltip("-1 = losowy układ co rundę. 0, 1, 2... = zawsze ten sam układ (bez odbicia), do oglądania. Numer liczysz od 0 wśród układów pasujących do rozmiaru planszy, w kolejności listy.")]
    public int forcedLayoutIndex = -1;

    [Header("Generowanie bloków niszczalnych w korytarzach")]
    public bool fillEmptyPathsWithBlocks = true;
    [Range(0.00f, 1.00f)] public float blockSpawnChance = 0.70f;

    [Header("Otwartość planszy (więcej walki)")]
    [Tooltip("Mnożnik szansy na blok: 1 = jak w polu wyżej, mniej = bardziej otwarta plansza.")]
    [Range(0.2f, 1f)] public float densityScale = 0.75f;
    [Tooltip("Wokół fontanny nie pojawiają się losowe bloki, więc da się ją obejść.")]
    public bool keepFountainRingFree = true;
    [Tooltip("Środkowy rząd i środkowa kolumna nie dostają losowych bloków: gracze szybciej się spotykają.")]
    public bool openCenterLanes = true;

    [Header("Szanse na rodzaje bloków niszczalnych (%)")]
    [Range(0, 100)] public int cookieChance = 65;
    [Range(0, 100)] public int heavyCookieChance = 25;
    [Range(0, 100)] public int honeyBlockChance = 5;
    [Range(0, 100)] public int sodaBlockChance = 5;

    [Header("Czas trwania efektów (Sekundy)")]
    public float honeyPuddleDuration = 2.0f;
    public float sodaPuddleDuration = 2.0f;

    [Header("Start graczy (Rogi planszy)")]
    public Vector2Int[] spawnPositions = DefaultSpawnPositions();

    // Tablice jednowymiarowe (szybsze niż [,]). Pole (x, y) leży pod indeksem y * Width + x.
    readonly TileType[] grid = new TileType[CellCount];
    readonly bool[] reservedCells = new bool[CellCount];
    readonly bool[] bombs = new bool[CellCount];
    readonly int[] fire = new int[CellCount];
    readonly int[] puddleToken = new int[CellCount];
    readonly object[] blockLastHitBy = new object[CellCount];

    // warianty grafik z GameArt: kafelki na wariant dla każdego rodzaju i numer wariantu wylosowany dla każdego pola
    readonly Dictionary<TileType, TileBase[]> variantTiles = new Dictionary<TileType, TileBase[]>();
    readonly int[] variantSeed = new int[CellCount];
    int nextPuddleToken;

    // false po przeładowaniu skryptów w trakcie gry (Unity czyści wtedy zwykłe pola i statyczne)
    bool mapReady;

    public event System.Action<Vector2Int, TileType> BlockDestroyed;

    static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    static int Index(int x, int y) => y * Width + x;
    static int Index(Vector2Int cell) => cell.y * Width + cell.x;

    void Awake()
    {
        Instance = this;
        EnsureSpawnPositionsInBounds();
        BuildMapFromTilemap();
    }

    // Pozycje startowe zapisane w scenie mogą pochodzić ze starego rozmiaru siatki. Poza planszą stawiamy rogi od nowa.
    public void EnsureSpawnPositionsInBounds()
    {
        foreach (Vector2Int spawn in spawnPositions)
        {
            if (!InBounds(spawn))
            {
                spawnPositions = DefaultSpawnPositions();
                Debug.LogWarning("GridMap: pozycje startowe były poza planszą, ustawiłem rogi.", this);
                return;
            }
        }
    }

    public static Vector2Int[] DefaultSpawnPositions()
    {
        return new[]
        {
            new Vector2Int(1, 1),
            new Vector2Int(1, Height - 2),
            new Vector2Int(Width - 2, 1),
            new Vector2Int(Width - 2, Height - 2)
        };
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

        // 0. Jeśli są układy, losujemy jeden i malujemy go w tilemapie (z losowym odbiciem)
        if (layouts != null && layouts.Length > 0)
        {
            ApplyRandomLayout();
        }

        // warianty grafik z GameArt: dla każdego pola losujemy numer wariantu raz na rundę
        BuildVariantTiles();

        for (int i = 0; i < variantSeed.Length; i++)
        {
            variantSeed[i] = Random.Range(0, 100000);
        }

        // 1. Odczytaj kafelki z namalowanej Tilemapy
        ReadTilemap();
        mapReady = true;

        // 2. Wypełnij wolne miejsce blokami
        if (fillEmptyPathsWithBlocks)
        {
            GenerateDestructibleBlocks();
        }

        // 3. Ramka i ściany z układu też dostają losowe warianty, a podłoga układa się z wariantów
        ApplyVariantsToBlocks();
        ApplyFloorVariants();
    }

    // Dla każdego rodzaju kafelka, który ma w GameArt co najmniej dwa warianty, tworzymy po kafelku na wariant.
    // Przy jednym wariancie zostaje zwykły plik z Assets/Tiles (GameArt wpisuje w niego sprite).
    void BuildVariantTiles()
    {
        variantTiles.Clear();
        GameArt art = GameArt.Instance;

        if (art == null)
        {
            return;
        }

        AddVariants(art, TileType.SolidWall, borderTile);
        AddVariants(art, TileType.Pillar, pillarTile);
        AddVariants(art, TileType.Cookie, cookieTile);
        AddVariants(art, TileType.HeavyCookie, heavyCookieTile);
        AddVariants(art, TileType.HoneyBlock, honeyBlockTile);
        AddVariants(art, TileType.SodaBlock, sodaBlockTile);
        AddVariants(art, TileType.HoneyPuddle, honeyPuddleTile);
        AddVariants(art, TileType.SodaPuddle, sodaPuddleTile);
    }

    void AddVariants(GameArt art, TileType type, TileBase template)
    {
        Sprite[] sprites = art.TileVariants(type);

        // przy wybranym motywie nawet pojedyncza grafika wymaga własnego kafelka (plik w Assets/Tiles trzyma tylko jedną)
        int minimum = art.HasTheme ? 1 : 2;

        if (sprites == null || sprites.Length < minimum)
        {
            return;
        }

        List<TileBase> list = new List<TileBase>();

        foreach (Sprite sprite in sprites)
        {
            if (sprite != null)
            {
                list.Add(CreateVariantTile(sprite, template as Tile));
            }
        }

        if (list.Count >= minimum)
        {
            variantTiles[type] = list.ToArray();
        }
    }

    // Nowy kafelek z podanym sprite'em i ustawieniami (kolor, kolizja) wzorcowego kafelka.
    static Tile CreateVariantTile(Sprite sprite, Tile template)
    {
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.hideFlags = HideFlags.DontSave;
        tile.sprite = sprite;

        if (template != null)
        {
            tile.color = template.color;
            tile.colliderType = template.colliderType;
            tile.transform = template.transform;
        }

        return tile;
    }

    // Kafelek dla pola: wylosowany wariant, a gdy rodzaj nie ma wariantów, zwykły kafelek.
    TileBase PickTile(TileType type, TileBase fallback, int cellIndex)
    {
        if (variantTiles.TryGetValue(type, out TileBase[] tiles))
        {
            return tiles[variantSeed[cellIndex] % tiles.Length];
        }

        return fallback;
    }

    void ApplyVariantsToBlocks()
    {
        if (variantTiles.Count == 0)
        {
            return;
        }

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int i = Index(x, y);
                TileType type = grid[i];

                if (type == TileType.SolidWall || type == TileType.Pillar || TileRules.IsDestructible(type))
                {
                    TileBase tile = PickTile(type, null, i);

                    if (tile != null)
                    {
                        blocksTilemap.SetTile(new Vector3Int(x, y, 0), tile);
                    }
                }
            }
        }
    }

    // Podłoga: dwa warianty układają się w szachownicę, więcej wariantów jest losowanych dla każdego pola.
    void ApplyFloorVariants()
    {
        GameArt art = GameArt.Instance;
        Sprite[] sprites = art != null ? art.TileVariants(TileType.Empty) : null;
        int minimum = art != null && art.HasTheme ? 1 : 2;

        if (sprites == null || sprites.Length < minimum)
        {
            return;
        }

        Tilemap ground = null;

        foreach (Tilemap candidate in FindObjectsByType<Tilemap>())
        {
            if (candidate.name == "Ground")
            {
                ground = candidate;
                break;
            }
        }

        if (ground == null)
        {
            return;
        }

        Tile template = ground.GetTile(new Vector3Int(1, 1, 0)) as Tile;
        List<TileBase> tiles = new List<TileBase>();

        foreach (Sprite sprite in sprites)
        {
            if (sprite != null)
            {
                tiles.Add(CreateVariantTile(sprite, template));
            }
        }

        if (tiles.Count < minimum)
        {
            return;
        }

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int index = tiles.Count <= 2 ? (x + y) % tiles.Count : Random.Range(0, tiles.Count);
                ground.SetTile(new Vector3Int(x, y, 0), tiles[index]);
            }
        }
    }

    // Maluje w Blocks Tilemap ramkę z kodu oraz wnętrze skopiowane z losowego prefabu układu.
    // Układ zawiera tylko kafelki filarów, stałych bloków i pól NoSpawn. Resztę dosypuje GenerateDestructibleBlocks.
    void ApplyRandomLayout()
    {
        // bierzemy tylko układy narysowane dla bieżącego rozmiaru siatki, puste pola na liście pomijamy
        List<GameObject> compatible = new List<GameObject>();

        foreach (GameObject candidate in layouts)
        {
            if (candidate == null)
            {
                continue;
            }

            MapLayoutGuide guide = candidate.GetComponent<MapLayoutGuide>();

            if (guide == null || guide.FitsGrid(Width, Height))
            {
                compatible.Add(candidate);
            }
        }

        if (compatible.Count == 0)
        {
            Debug.LogWarning("GridMap: na liście nie ma układów dla siatki " + Width + " x " + Height
                + ". Użyj przycisku 'Przypisz układy' w obiekcie GameArt albo zmień rozmiar planszy.", this);
            return;
        }

        // do oglądania: konkretny układ (numer liczony wśród pasujących do rozmiaru), bez losowego odbicia
        bool forced = forcedLayoutIndex >= 0 && forcedLayoutIndex < compatible.Count;
        GameObject prefab = forced ? compatible[forcedLayoutIndex] : compatible[Random.Range(0, compatible.Count)];

        Debug.Log("GridMap: układ '" + prefab.name + "'" + (forced ? " (wybrany ręcznie)" : "") + ".", this);

        // prefab czytamy z tymczasowej kopii, bo tilemapa z pliku nie zawsze oddaje kafelki
        GameObject copy = Instantiate(prefab);
        copy.SetActive(false);
        Tilemap source = copy.GetComponentInChildren<Tilemap>(true);

        if (source == null)
        {
            Debug.LogWarning("GridMap: układ '" + prefab.name + "' nie ma Tilemapy.", this);
            Destroy(copy);
            return;
        }

        bool flipX = randomFlip && !forced && Random.value < 0.5f;
        bool flipY = randomFlip && !forced && Random.value < 0.5f;

        blocksTilemap.ClearAllTiles();

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                bool border = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                TileBase tile = border
                    ? borderTile
                    : source.GetTile(new Vector3Int(flipX ? Width - 1 - x : x, flipY ? Height - 1 - y : y, 0));

                if (tile != null)
                {
                    blocksTilemap.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }
        }

        Destroy(copy);
    }

    void ReadTilemap()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
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
        for (int y = 1; y < Height - 1; y++)
        {
            for (int x = 1; x < Width - 1; x++)
            {
                int i = Index(x, y);

                // Nie stawiaj bloków na polach zarezerwowanych kafelkiem NoSpawn oraz wokół pozycji startowych
                if (grid[i] != TileType.Empty || reservedCells[i] || IsPlayerSpawnArea(x, y))
                {
                    continue;
                }

                // wokół fontanny zostaje wolny pierścień, a przez środek biegną dwie otwarte aleje
                if (IsFountainRing(x, y) || IsOpenLane(x, y))
                {
                    continue;
                }

                if (Random.value < blockSpawnChance * densityScale)
                {
                    grid[i] = PickBlock(x, y);
                    UpdateTileVisual(x, y);
                }
            }
        }
    }

    // Pole w odległości do 2 od środka planszy (fontanna 3 x 3 i jeden rząd wokół niej).
    bool IsFountainRing(int x, int y)
    {
        return keepFountainRingFree && Mathf.Abs(x - Width / 2) <= 2 && Mathf.Abs(y - Height / 2) <= 2;
    }

    bool IsOpenLane(int x, int y)
    {
        return openCenterLanes && (x == Width / 2 || y == Height / 2);
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

        // jeśli rodzaj ma kilka wariantów w GameArt, bierzemy wariant wylosowany dla tego pola
        int cellIndex = Index(x, y);
        TileType currentType = grid[cellIndex];
        blockTile = blockTile != null ? PickTile(currentType, blockTile, cellIndex) : null;
        puddleTile = puddleTile != null ? PickTile(currentType, puddleTile, cellIndex) : null;

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

    // source to to, co uderza (bomba, rakieta). Ta sama eksplozja nie trafia bloku dwa razy,
    // ale każda kolejna bomba liczy się jako osobne trafienie, nawet gdy wybuchnie w tej samej chwili.
    // Dzięki temu gruba czekolada znika, gdy dostaną ją dwie bomby (np. gracza i bota).
    public void DestroyBlock(Vector2Int cell, object source = null)
    {
        if (!InBounds(cell)) return;

        int i = Index(cell);

        if (source != null && ReferenceEquals(blockLastHitBy[i], source)) return;

        TileType current = grid[i];

        switch (current)
        {
            case TileType.Cookie:
                blockLastHitBy[i] = null;
                grid[i] = TileType.Empty;
                UpdateTileVisual(cell.x, cell.y);
                GameAudio.Play(AudioEvent.BlockBreak, GridToWorld(cell));
                BlockDestroyed?.Invoke(cell, current);
                break;
            case TileType.HeavyCookie:
                blockLastHitBy[i] = source;
                grid[i] = TileType.Cookie;
                UpdateTileVisual(cell.x, cell.y);
                break;
            case TileType.HoneyBlock:
                blockLastHitBy[i] = null;
                SpreadPuddle(cell, TileType.HoneyPuddle, honeyPuddleDuration);
                BlockDestroyed?.Invoke(cell, current);
                break;
            case TileType.SodaBlock:
                blockLastHitBy[i] = null;
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

        blockLastHitBy[i] = null;
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
        return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
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

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
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