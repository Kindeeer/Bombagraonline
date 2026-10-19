using UnityEngine;

// Atomówka: spada z góry na wybrane pole (czerwony obszar pokazuje, gdzie wybuchnie) i robi ogromny wybuch kwadratem
// (promień BlastRadius pól w każdą stronę). Niszczy bloki i zapala wszystkie pola, więc zabija każdego w zasięgu.
// Zasada zwycięstwa: kto zrzucił atomówkę i zabił nią wszystkich przeciwników, wygrywa rundę nawet jeśli sam zginął
// (pilnuje tego MatchManager.NoteNuke).
public class Nuke : MonoBehaviour
{
    // Promień wybuchu w polach (3 = kwadrat 7 x 7).
    public const int BlastRadius = 3;

    const float FallSeconds = 1.5f;
    const float FallHeight = 11f;

    static Sprite squareSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        squareSprite = null;
    }

    // Biały kwadrat 1 x 1 jednostka (do znaczników obszaru wybuchu).
    public static Sprite SquareSprite()
    {
        if (squareSprite != null)
        {
            return squareSprite;
        }

        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[16];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(255, 255, 255, 255);
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        squareSprite.hideFlags = HideFlags.HideAndDontSave;
        return squareSprite;
    }

    // Czy pole leży w zasięgu wybuchu atomówki zrzuconej na dane pole (kwadrat).
    public static bool InBlast(Vector2Int center, Vector2Int cell)
    {
        return Mathf.Abs(cell.x - center.x) <= BlastRadius && Mathf.Abs(cell.y - center.y) <= BlastRadius;
    }

    public static void Drop(Vector2Int cell, int ownerSlot, Bomb bombPrefab, Sprite icon)
    {
        GameObject go = new GameObject("Nuke");
        go.AddComponent<Nuke>().Begin(cell, ownerSlot, bombPrefab, icon);
    }

    Vector2Int target;
    int ownerSlot;
    Bomb bombPrefab;
    Transform missile;
    SpriteRenderer marker;
    float time;
    bool exploded;

    void Begin(Vector2Int cell, int owner, Bomb prefab, Sprite icon)
    {
        target = cell;
        ownerSlot = owner;
        bombPrefab = prefab;

        Vector3 center = GridMap.Instance.GridToWorld(cell);
        transform.position = center;

        // czerwony obszar wybuchu, miga coraz szybciej
        GameObject area = new GameObject("BlastArea");
        area.transform.SetParent(transform, false);
        area.transform.localScale = Vector3.one * (BlastRadius * 2 + 1);
        marker = area.AddComponent<SpriteRenderer>();
        marker.sprite = SquareSprite();
        marker.sortingOrder = 2;
        marker.color = new Color(1f, 0.15f, 0.1f, 0.25f);

        // pocisk spada z góry
        GameObject body = new GameObject("Missile");
        body.transform.SetParent(transform, false);
        missile = body.transform;
        missile.localScale = Vector3.one * 2.6f;

        SpriteRenderer renderer = body.AddComponent<SpriteRenderer>();
        renderer.sprite = icon;
        renderer.sortingOrder = 6;

        GameAudio.Play(AudioEvent.NukeFall, center);
    }

    void Update()
    {
        if (exploded)
        {
            return;
        }

        time += Time.deltaTime;
        float u = Mathf.Clamp01(time / FallSeconds);

        // pocisk przyspiesza ku ziemi
        missile.localPosition = new Vector3(0f, FallHeight * (1f - u * u), 0f);

        float blink = Mathf.Repeat(time * Mathf.Lerp(3f, 14f, u), 1f);
        marker.color = new Color(1f, 0.15f, 0.1f, Mathf.Lerp(0.18f, 0.5f, blink));

        if (u >= 1f)
        {
            Explode();
        }
    }

    void Explode()
    {
        exploded = true;

        GridMap map = GridMap.Instance;
        Vector3 center = map.GridToWorld(target);
        GameAudio.Play(AudioEvent.NukeExplode, center);

        // zwycięstwo atomówką: MatchManager wie, kto ją zrzucił, zanim zaczną ginąć postacie
        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.NoteNuke(ownerSlot);
        }

        for (int x = -BlastRadius; x <= BlastRadius; x++)
        {
            for (int y = -BlastRadius; y <= BlastRadius; y++)
            {
                Vector2Int cell = target + new Vector2Int(x, y);
                TileType type = map.GetTileType(cell);

                // ramka areny, filary i fontanna zostają
                if (type == TileType.SolidWall || type == TileType.Pillar || type == TileType.Landmark)
                {
                    continue;
                }

                if (TileRules.IsDestructible(type))
                {
                    map.SmashBlock(cell);
                }

                if (BombRegistry.TryGet(cell, out Bomb other))
                {
                    other.Trigger();
                }

                if (bombPrefab != null && bombPrefab.flamePrefab != null)
                {
                    Flame flame = Instantiate(bombPrefab.flamePrefab);
                    flame.Init(cell);
                }
            }
        }

        // błysk i fala uderzeniowa
        PickupFx.Spawn(PickupArt.Ripple(), center + Vector3.down * 0.2f, 1.5f, 14f, new Color(1f, 0.85f, 0.4f, 0.9f), 0.7f, 7);
        PickupFx.Spawn(PickupArt.Ripple(), center + Vector3.down * 0.2f, 0.6f, 9f, new Color(1f, 0.4f, 0.15f, 0.8f), 0.5f, 7);

        Destroy(gameObject);
    }
}
