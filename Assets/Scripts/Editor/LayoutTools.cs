using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Narzędzia do układów planszy (przyciski w Inspectorze obiektu GameArt).
// Układ to prefab z Tilemapą, w której malujesz filary, stałe bloki i pola NoSpawn zwykłą paletą kafelków.
// - New Layout: tworzy pusty prefab z prowadnicą (widać, gdzie rysować) i otwiera go do malowania,
// - Mirror Layout: kopiuje namalowaną lewą górną ćwiartkę na resztę planszy (symetria = uczciwy start dla wszystkich),
// - Validate Layout: sprawdza, czy gracze mogą się do siebie dostać i czy układ jest symetryczny,
// - Assign All Layouts To GridMap: wpisuje wszystkie układy z Assets/Layouts do GridMap.
// Ten plik działa tylko w edytorze.
public static class LayoutTools
{
    const string LayoutFolder = "Assets/Layouts";
    const string PillarTilePath = "Assets/Tiles/Tile_Pillar.asset";

    internal static void NewLayout()
    {
        Directory.CreateDirectory(LayoutFolder);

        GameObject root = new GameObject("Layout", typeof(Grid), typeof(MapLayoutGuide));
        StampSize(root, GridMap.Width, GridMap.Height);
        GameObject tiles = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer));
        tiles.transform.SetParent(root.transform, false);
        tiles.GetComponent<TilemapRenderer>().sortingOrder = 5;

        string path = AssetDatabase.GenerateUniqueAssetPath(LayoutFolder + "/Layout_Nowy.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Selection.activeObject = prefab;
        AssetDatabase.OpenAsset(prefab);
        Debug.Log("Bomb Game: nowy układ " + path + ". Zaznacz Tilemapę 'Tiles', wybierz kafelek z palety (Window > 2D > Tile Palette) i maluj w żółtej ćwiartce. Potem w GameArt przycisk Odbij układ.");
    }

    internal static void MirrorLayout()
    {
        Tilemap tilemap = FindLayoutTilemap();

        if (tilemap == null)
        {
            return;
        }

        Undo.RegisterCompleteObjectUndo(tilemap, "Mirror Layout");
        MirrorQuadrant(tilemap);
        MarkLayoutDirty(tilemap);
        Debug.Log("Bomb Game: układ odbity lustrzanie (lewa górna ćwiartka skopiowana na całą planszę).");
    }

    // Źródło to lewa górna ćwiartka (razem z liniami środkowymi), kopiujemy ją na pozostałe trzy.
    static void MirrorQuadrant(Tilemap tilemap)
    {
        int w = GridMap.Width;
        int h = GridMap.Height;

        for (int x = 0; x <= w / 2; x++)
        {
            for (int y = h / 2; y < h; y++)
            {
                TileBase tile = tilemap.GetTile(new Vector3Int(x, y, 0));
                tilemap.SetTile(new Vector3Int(w - 1 - x, y, 0), tile);
                tilemap.SetTile(new Vector3Int(x, h - 1 - y, 0), tile);
                tilemap.SetTile(new Vector3Int(w - 1 - x, h - 1 - y, 0), tile);
            }
        }
    }

    // Pięć gotowych układów na start. Powstały jako wzorowane na statystykach układów z Bomb It 7 (ok. 30% pól to ściany,
    // połowa ścian to pojedyncze filary, reszta to dwu- i trzypolowe odcinki, wolne rogi i środek), ale są własne.
    // Rysunek to lewa górna ćwiartka (kolumny x = 1..7, wiersze od y = 9 w dół do y = 5), # to filar, kropka to puste pole.
    internal static void CreateSampleLayouts()
    {
        // rysunki ćwiartek są ułożone dla siatki 15 x 11, więc przy innym rozmiarze wyszłyby bezsensowne układy
        bool phoneSize = GridMap.Width == 15 && GridMap.Height == 11;

        if (!phoneSize)
        {
            Debug.LogError("Bomb Game: przykładowe układy są dla siatki 15 x 11, a teraz jest " + GridMap.Width + " x " + GridMap.Height
                + ". Użyj przycisku 'Generuj losowe układy' w obiekcie GameArt, który dopasowuje się do rozmiaru planszy.");
            return;
        }

        TileBase pillar = AssetDatabase.LoadAssetAtPath<TileBase>(PillarTilePath);

        if (pillar == null)
        {
            Debug.LogError("Bomb Game: nie znalazłem kafelka " + PillarTilePath + ".");
            return;
        }

        Directory.CreateDirectory(LayoutFolder);

        // starsze wersje przykładów (zbyt rzadkie albo zbyt ciasne wokół fontanny) usuwamy i tworzymy od nowa
        foreach (string old in new[] { "Klasyczny", "Krzyzyki", "Komory", "Rzadki", "Tkanina", "Bastiony", "Wyspy", "Aleje", "Kolumny" })
        {
            AssetDatabase.DeleteAsset(LayoutFolder + "/Layout_" + old + ".prefab");
        }

        // wolny pierścień wokół fontanny i luźniejsze ściany (ok. 26%), reszta to losowe bloki z GridMap
        string[][] patterns =
        {
            new[] { "..#..##", "..#...#", "#..#...", ".......", ".###..." },
            new[] { ".....##", ".#.#..#", "..#....", "#.#....", "#......" },
            new[] { "..##...", ".....##", ".#.....", "..##...", "#..#..." },
            new[] { "....#..", ".##..#.", "...#...", ".#.#...", ".#....." },
            new[] { "...##..", "....#..", "..##...", "#......", "#.##..." }
        };
        string[] names = { "Tkanina", "Bastiony", "Wyspy", "Aleje", "Kolumny" };

        for (int i = 0; i < patterns.Length; i++)
        {
            bool[,] quadrant = new bool[QuadrantColumns, QuadrantRows];

            for (int row = 0; row < patterns[i].Length; row++)
            {
                for (int column = 0; column < patterns[i][row].Length; column++)
                {
                    quadrant[column, row] = patterns[i][row][column] == '#';
                }
            }

            SaveLayout("Layout_" + names[i], quadrant, pillar);
        }

        AssetDatabase.Refresh();
        Debug.Log("Bomb Game: utworzono 5 przykładowych układów w " + LayoutFolder + ". Teraz w GameArt przycisk Przypisz układy.");
    }

    // Losuje wiele układów podobnych w charakterze do Bomb It 7 i zapisuje najlepsze (te, które są spójne i mają
    // naturalną mieszankę pojedynczych filarów oraz krótkich odcinków). Każde uruchomienie daje inny zestaw.
    internal static void GenerateRandomLayouts()
    {
        TileBase pillar = AssetDatabase.LoadAssetAtPath<TileBase>(PillarTilePath);

        if (pillar == null)
        {
            Debug.LogError("Bomb Game: nie znalazłem kafelka " + PillarTilePath + ".");
            return;
        }

        Directory.CreateDirectory(LayoutFolder);

        List<Candidate> candidates = new List<Candidate>();
        HashSet<string> seen = new HashSet<string>();

        for (int attempt = 0; attempt < 60000; attempt++)
        {
            bool[,] quadrant = RandomQuadrant();
            float score = ScoreLayout(quadrant);

            if (score < 0f)
            {
                continue;
            }

            string key = QuadrantKey(quadrant);

            if (seen.Add(key))
            {
                candidates.Add(new Candidate { quadrant = quadrant, key = key, score = score });
            }
        }

        candidates.Sort((a, b) => a.score.CompareTo(b.score));

        // bierzemy najlepsze, ale każdy kolejny musi wyraźnie różnić się od już wybranych
        List<Candidate> picks = new List<Candidate>();

        foreach (Candidate candidate in candidates)
        {
            if (picks.Count >= 8)
            {
                break;
            }

            bool different = true;

            foreach (Candidate picked in picks)
            {
                if (Distance(picked.key, candidate.key) < 12)
                {
                    different = false;
                    break;
                }
            }

            if (different)
            {
                picks.Add(candidate);
            }
        }

        for (int i = 0; i < picks.Count; i++)
        {
            SaveLayout("Layout_Gen_" + (i + 1).ToString("00"), picks[i].quadrant, pillar);
        }

        AssetDatabase.Refresh();
        Debug.Log("Bomb Game: wygenerowano " + picks.Count + " układów (z " + candidates.Count + " spójnych kandydatów). Obejrzyj je w " + LayoutFolder + ", niechciane usuń, a potem Assign All Layouts To GridMap.");
    }

    // ---------- Generowanie układów ----------

    class Candidate
    {
        public bool[,] quadrant;
        public string key;
        public float score;
    }

    // Lewa górna ćwiartka wnętrza: kolumny x = 1..W/2, wiersze od y = H-2 w dół do y = H/2.
    static int QuadrantColumns => GridMap.Width / 2;
    static int QuadrantRows => GridMap.Height - 1 - GridMap.Height / 2;

    static readonly int[][][] Motifs =
    {
        new[] { new[] { 0, 0 } },
        new[] { new[] { 0, 0 } },
        new[] { new[] { 0, 0 } },
        new[] { new[] { 0, 0 }, new[] { 1, 0 } },
        new[] { new[] { 0, 0 }, new[] { 1, 0 } },
        new[] { new[] { 0, 0 }, new[] { 0, 1 } },
        new[] { new[] { 0, 0 }, new[] { 0, 1 } },
        new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 } },
        new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 0, 2 } },
        new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 0, 1 } },
        new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 1, 1 } },
        new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 0, 1 } }
    };

    // Czy na polu ćwiartki wolno postawić filar (nie na wyjściu ze startu i nie w strefie fontanny).
    static bool CanPlace(int column, int row)
    {
        int x = 1 + column;
        int y = GridMap.Height - 2 - row;
        int top = GridMap.Height - 2;

        if (y == top && (x == 1 || x == 2)) return false;
        if (y == top - 1 && x == 1) return false;
        // fontanna 3 x 3 i wolny pierścień wokół niej (do obejścia fontanny)
        if (Mathf.Abs(x - GridMap.Width / 2) <= 2 && Mathf.Abs(y - GridMap.Height / 2) <= 2) return false;

        return true;
    }

    static bool[,] RandomQuadrant()
    {
        int columns = QuadrantColumns;
        int rows = QuadrantRows;
        bool[,] quadrant = new bool[columns, rows];
        int goal = 7 + Random.Range(0, 3);
        int cells = 0;

        for (int tries = 0; tries < 60 && cells < goal; tries++)
        {
            int[][] motif = Motifs[Random.Range(0, Motifs.Length)];
            int column0 = Random.Range(0, columns);
            int row0 = Random.Range(0, rows);
            bool fits = true;

            foreach (int[] part in motif)
            {
                int c = column0 + part[0];
                int r = row0 + part[1];

                if (c >= columns || r >= rows || !CanPlace(c, r) || quadrant[c, r])
                {
                    fits = false;
                    break;
                }
            }

            // odstęp jednego pola od już postawionych ścian, żeby odcinki były osobne
            for (int p = 0; fits && p < motif.Length; p++)
            {
                int c = column0 + motif[p][0];
                int r = row0 + motif[p][1];

                foreach (Vector2Int d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    int nc = c + d.x;
                    int nr = r + d.y;

                    if (nc < 0 || nr < 0 || nc >= columns || nr >= rows || !quadrant[nc, nr])
                    {
                        continue;
                    }

                    bool inMotif = false;

                    foreach (int[] part in motif)
                    {
                        if (column0 + part[0] == nc && row0 + part[1] == nr)
                        {
                            inMotif = true;
                        }
                    }

                    if (!inMotif)
                    {
                        fits = false;
                    }
                }
            }

            if (!fits)
            {
                continue;
            }

            foreach (int[] part in motif)
            {
                quadrant[column0 + part[0], row0 + part[1]] = true;
                cells++;
            }
        }

        return quadrant;
    }

    // Pełna plansza (ściany po odbiciu ćwiartki): true = ściana. Ramka też jest ścianą.
    static bool[,] Expand(bool[,] quadrant)
    {
        int w = GridMap.Width;
        int h = GridMap.Height;
        bool[,] wall = new bool[w, h];

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                wall[x, y] = x == 0 || y == 0 || x == w - 1 || y == h - 1;
            }
        }

        for (int c = 0; c < quadrant.GetLength(0); c++)
        {
            for (int r = 0; r < quadrant.GetLength(1); r++)
            {
                if (!quadrant[c, r])
                {
                    continue;
                }

                int x = 1 + c;
                int y = h - 2 - r;
                wall[x, y] = true;
                wall[w - 1 - x, y] = true;
                wall[x, h - 1 - y] = true;
                wall[w - 1 - x, h - 1 - y] = true;
            }
        }

        return wall;
    }

    // Im mniejsza liczba, tym układ bardziej przypomina strukturę planszy Bomb It 7. Wartość ujemna = układ odrzucony.
    static float ScoreLayout(bool[,] quadrant)
    {
        int w = GridMap.Width;
        int h = GridMap.Height;
        bool[,] wall = Expand(quadrant);
        int cx = w / 2;
        int cy = h / 2;
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        int walls = 0;
        int interior = 0;

        for (int x = 1; x < w - 1; x++)
        {
            for (int y = 1; y < h - 1; y++)
            {
                interior++;

                if (wall[x, y])
                {
                    walls++;
                }
            }
        }

        float density = (float)walls / interior;

        // luźniejsza plansza (ok. 24-31% ścian), żeby zostało miejsce na walkę
        if (density < 0.24f || density > 0.31f)
        {
            return -1f;
        }

        // wolne pole to takie, które nie jest ścianą ani fontanną
        bool IsFree(int px, int py)
        {
            return !wall[px, py] && !(Mathf.Abs(px - cx) <= 1 && Mathf.Abs(py - cy) <= 1);
        }

        // wszystkie wolne pola muszą być połączone z pierwszym startem
        bool[,] reached = new bool[w, h];
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(1, h - 2));
        reached[1, h - 2] = true;
        int reachedCount = 0;

        while (stack.Count > 0)
        {
            Vector2Int cell = stack.Pop();
            reachedCount++;

            foreach (Vector2Int d in dirs)
            {
                int nx = cell.x + d.x;
                int ny = cell.y + d.y;

                if (IsFree(nx, ny) && !reached[nx, ny])
                {
                    reached[nx, ny] = true;
                    stack.Push(new Vector2Int(nx, ny));
                }
            }
        }

        int freeCount = 0;
        int deadEnds = 0;

        for (int x = 1; x < w - 1; x++)
        {
            for (int y = 1; y < h - 1; y++)
            {
                if (!IsFree(x, y))
                {
                    continue;
                }

                freeCount++;
                int blocked = 0;

                foreach (Vector2Int d in dirs)
                {
                    if (!IsFree(x + d.x, y + d.y))
                    {
                        blocked++;
                    }
                }

                if (blocked >= 3)
                {
                    deadEnds++;
                }
            }
        }

        if (reachedCount != freeCount || deadEnds > 4)
        {
            return -1f;
        }

        // bez pełnych bloków 2x2
        for (int x = 1; x < w - 2; x++)
        {
            for (int y = 1; y < h - 2; y++)
            {
                if (wall[x, y] && wall[x + 1, y] && wall[x, y + 1] && wall[x + 1, y + 1])
                {
                    return -1f;
                }
            }
        }

        // spójne grupy ścian: rozkład rozmiarów ma przypominać Bomb It 7 (ok. 49% pojedynczych, 21% par, 13% trójek, 9% czwórek)
        float[] target = { 0.49f, 0.21f, 0.13f, 0.09f, 0.08f };
        int[] groups = new int[5];
        int groupCount = 0;
        bool[,] visited = new bool[w, h];

        for (int x = 1; x < w - 1; x++)
        {
            for (int y = 1; y < h - 1; y++)
            {
                if (!wall[x, y] || visited[x, y])
                {
                    continue;
                }

                int size = 0;
                Stack<Vector2Int> group = new Stack<Vector2Int>();
                group.Push(new Vector2Int(x, y));
                visited[x, y] = true;

                while (group.Count > 0)
                {
                    Vector2Int cell = group.Pop();
                    size++;

                    foreach (Vector2Int d in dirs)
                    {
                        int nx = cell.x + d.x;
                        int ny = cell.y + d.y;

                        if (nx > 0 && ny > 0 && nx < w - 1 && ny < h - 1 && wall[nx, ny] && !visited[nx, ny])
                        {
                            visited[nx, ny] = true;
                            group.Push(new Vector2Int(nx, ny));
                        }
                    }
                }

                groups[Mathf.Min(5, size) - 1]++;
                groupCount++;
            }
        }

        float distance = 0f;

        for (int i = 0; i < 5; i++)
        {
            distance += Mathf.Abs((float)groups[i] / groupCount - target[i]);
        }

        // ściana w linii nie dłuższa niż 4 pola
        int longest = 0;

        for (int y = 1; y < h - 1; y++)
        {
            int run = 0;

            for (int x = 1; x < w; x++)
            {
                run = wall[x, y] && x < w - 1 ? run + 1 : 0;
                longest = Mathf.Max(longest, run);
            }
        }

        for (int x = 1; x < w - 1; x++)
        {
            int run = 0;

            for (int y = 1; y < h; y++)
            {
                run = wall[x, y] && y < h - 1 ? run + 1 : 0;
                longest = Mathf.Max(longest, run);
            }
        }

        return longest > 4 ? -1f : distance;
    }

    static string QuadrantKey(bool[,] quadrant)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        for (int r = 0; r < quadrant.GetLength(1); r++)
        {
            for (int c = 0; c < quadrant.GetLength(0); c++)
            {
                builder.Append(quadrant[c, r] ? '#' : '.');
            }
        }

        return builder.ToString();
    }

    static int Distance(string a, string b)
    {
        int difference = 0;

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                difference++;
            }
        }

        return difference;
    }

    // Zapisuje układ jako prefab: ćwiartka jest malowana kafelkami filarów i odbijana na całą planszę.
    static void SaveLayout(string name, bool[,] quadrant, TileBase pillar)
    {
        GameObject root = new GameObject(name, typeof(Grid), typeof(MapLayoutGuide));
        StampSize(root, GridMap.Width, GridMap.Height);
        GameObject tiles = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer));
        tiles.transform.SetParent(root.transform, false);
        tiles.GetComponent<TilemapRenderer>().sortingOrder = 5;
        Tilemap tilemap = tiles.GetComponent<Tilemap>();

        for (int c = 0; c < quadrant.GetLength(0); c++)
        {
            for (int r = 0; r < quadrant.GetLength(1); r++)
            {
                if (quadrant[c, r])
                {
                    tilemap.SetTile(new Vector3Int(1 + c, GridMap.Height - 2 - r, 0), pillar);
                }
            }
        }

        MirrorQuadrant(tilemap);

        string path = AssetDatabase.GenerateUniqueAssetPath(LayoutFolder + "/" + name + ".prefab");
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    internal static void ValidateLayout()
    {
        Tilemap tilemap = FindLayoutTilemap();

        if (tilemap == null)
        {
            return;
        }

        TileBase pillar = AssetDatabase.LoadAssetAtPath<TileBase>(PillarTilePath);

        if (pillar == null)
        {
            Debug.LogError("Bomb Game: nie znalazłem kafelka " + PillarTilePath + ".");
            return;
        }

        int w = GridMap.Width;
        int h = GridMap.Height;
        bool[,] blocked = new bool[w, h];
        int pillars = 0;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                bool isPillar = !border && tilemap.GetTile(new Vector3Int(x, y, 0)) == pillar;
                blocked[x, y] = border || isPillar;

                if (isPillar)
                {
                    pillars++;
                }
            }
        }

        Vector2Int[] spawns = GridMap.DefaultSpawnPositions();
        int problems = 0;

        // filar na polu startowym albo tuż przy nim zamykałby graczowi wyjście
        Vector2Int[] exits = { Vector2Int.zero, Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int spawn in spawns)
        {
            foreach (Vector2Int offset in exits)
            {
                Vector2Int cell = spawn + offset;

                if (cell.x > 0 && cell.y > 0 && cell.x < w - 1 && cell.y < h - 1 && blocked[cell.x, cell.y])
                {
                    Debug.LogWarning("Bomb Game: filar przy starcie gracza na polu (" + cell.x + ", " + cell.y + ").");
                    problems++;
                }
            }
        }

        // filar w pierścieniu wokół fontanny utrudniałby jej obejście
        for (int x = w / 2 - 2; x <= w / 2 + 2; x++)
        {
            for (int y = h / 2 - 2; y <= h / 2 + 2; y++)
            {
                if (blocked[x, y])
                {
                    Debug.LogWarning("Bomb Game: filar w pierścieniu wokół fontanny na polu (" + x + ", " + y + ").");
                    problems++;
                }
            }
        }

        // czy z pierwszego startu da się dojść do pozostałych (bloki niszczalne nie przeszkadzają, filary tak)
        bool[,] reached = new bool[w, h];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(spawns[0]);
        reached[spawns[0].x, spawns[0].y] = true;
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;

                if (next.x < 0 || next.y < 0 || next.x >= w || next.y >= h || blocked[next.x, next.y] || reached[next.x, next.y])
                {
                    continue;
                }

                reached[next.x, next.y] = true;
                queue.Enqueue(next);
            }
        }

        for (int i = 1; i < spawns.Length; i++)
        {
            if (!reached[spawns[i].x, spawns[i].y])
            {
                Debug.LogError("Bomb Game: gracz " + (i + 1) + " jest odcięty filarami od gracza 1.");
                problems++;
            }
        }

        // symetria lewo-prawo i góra-dół
        int asymmetric = 0;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                TileBase tile = tilemap.GetTile(new Vector3Int(x, y, 0));

                if (tile != tilemap.GetTile(new Vector3Int(w - 1 - x, y, 0)) || tile != tilemap.GetTile(new Vector3Int(x, h - 1 - y, 0)))
                {
                    asymmetric++;
                }
            }
        }

        if (asymmetric > 0)
        {
            Debug.LogWarning("Bomb Game: układ nie jest symetryczny (" + asymmetric + " pól). Uruchom Mirror Layout, jeśli ma być uczciwy dla czterech graczy.");
            problems++;
        }

        if (problems == 0)
        {
            Debug.Log("Bomb Game: układ jest w porządku. Filarów: " + pillars + ", wszyscy gracze mogą się do siebie dostać, układ jest symetryczny.");
        }
        else
        {
            Debug.Log("Bomb Game: sprawdzanie zakończone, problemów: " + problems + " (szczegóły wyżej). Filarów: " + pillars + ".");
        }
    }

    internal static void AssignAll()
    {
        GridMap map = Object.FindAnyObjectByType<GridMap>();

        if (map == null)
        {
            Debug.LogError("Bomb Game: nie znaleziono GridMap w scenie. Jeśli masz otwarty prefab układu, zamknij go (strzałka wstecz w Hierarchii).");
            return;
        }

        List<GameObject> layouts = new List<GameObject>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { LayoutFolder }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));

            if (prefab != null && prefab.GetComponent<MapLayoutGuide>() != null)
            {
                layouts.Add(prefab);
            }
        }

        // kolejność po nazwie, żeby numer w polu Forced Layout Index był przewidywalny
        layouts.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        Undo.RecordObject(map, "Assign Layouts");
        map.layouts = layouts.ToArray();
        EditorUtility.SetDirty(map);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Bomb Game: GridMap ma teraz " + layouts.Count + " układów. Zapisz scenę (Ctrl+S).");
    }

    // Zapisuje w prefabie, dla jakiego rozmiaru siatki jest narysowany. GridMap używa tylko pasujących układów.
    internal static void StampSize(GameObject root, int width, int height)
    {
        MapLayoutGuide guide = root.GetComponent<MapLayoutGuide>();
        guide.gridWidth = width;
        guide.gridHeight = height;
    }

    // Tilemapa otwartego prefabu układu albo zaznaczonego obiektu w scenie.
    static Tilemap FindLayoutTilemap()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();

        if (stage != null)
        {
            Tilemap inPrefab = stage.prefabContentsRoot.GetComponentInChildren<Tilemap>(true);

            if (inPrefab != null)
            {
                return inPrefab;
            }
        }

        GameObject selected = Selection.activeGameObject;

        if (selected != null && !EditorUtility.IsPersistent(selected))
        {
            Tilemap inScene = selected.GetComponentInChildren<Tilemap>(true);

            if (inScene != null)
            {
                return inScene;
            }
        }

        Debug.LogError("Bomb Game: otwórz prefab układu (dwuklik w Project na pliku z Assets/Layouts) i spróbuj jeszcze raz.");
        return null;
    }

    static void MarkLayoutDirty(Tilemap tilemap)
    {
        EditorUtility.SetDirty(tilemap);

        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();

        if (stage != null)
        {
            EditorSceneManager.MarkSceneDirty(stage.scene);
        }
        else
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
