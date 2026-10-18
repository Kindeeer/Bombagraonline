using UnityEngine;

// Rakieta leci prosto przed siebie do pierwszej przeszkody (ściana, blok, bomba).
// Wybuch 0: płomień tylko na polu przed przeszkodą i na samym bloku, więc strzelający jest bezpieczny.
// Wybuch powyżej 0: zwykła bomba o tym zasięgu w miejscu trafienia (może trafić też strzelającego).
public class Rocket : MonoBehaviour
{
    const float Speed = 11f;
    const float FuseAfterLanding = 0.02f;

    Transform shooter;
    Vector2Int start;
    Vector2Int direction;
    Vector2Int landing;
    int blastRange;
    Bomb bombPrefab;

    Vector3 from;
    Vector3 to;
    float time;
    float duration;
    float puffTimer;

    public static void Launch(Vector2Int start, Vector2Int direction, int blastRange, Bomb bombPrefab, Sprite sprite, Transform shooter)
    {
        GameObject go = new GameObject("Rocket");
        Rocket rocket = go.AddComponent<Rocket>();
        rocket.Begin(start, direction, blastRange, bombPrefab, sprite, shooter);
    }

    void Begin(Vector2Int startCell, Vector2Int flyDirection, int range, Bomb prefab, Sprite sprite, Transform shooterTransform)
    {
        shooter = shooterTransform;
        start = startCell;
        direction = flyDirection;
        blastRange = range;
        bombPrefab = prefab;

        Vector2Int cell = start;

        for (int i = 0; i < Mathf.Max(GridMap.Width, GridMap.Height); i++)
        {
            Vector2Int next = cell + direction;

            if (!GridMap.Instance.CanEnter(next))
            {
                break;
            }

            cell = next;
        }

        landing = cell;

        from = GridMap.Instance.GridToWorld(start);
        to = GridMap.Instance.GridToWorld(landing) + new Vector3(direction.x, direction.y, 0f) * 0.4f;
        duration = Mathf.Max(0.05f, Vector3.Distance(from, to) / Speed);

        transform.position = from;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        transform.localScale = Vector3.one * 1.2f;

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 4;
    }

    void Update()
    {
        time += Time.deltaTime;
        float u = Mathf.Clamp01(time / duration);

        Vector3 path = Vector3.Lerp(from, to, u);
        transform.position = path + Vector3.up * 0.2f;

        if (TryHitPlayer(path))
        {
            Destroy(gameObject);
            return;
        }

        puffTimer -= Time.deltaTime;

        if (puffTimer <= 0f)
        {
            puffTimer = 0.04f;
            Vector3 behind = -new Vector3(direction.x, direction.y, 0f) * 0.3f;

            PickupFx.Spawn(PickupArt.Drop(), transform.position + behind, 0.22f, 0.05f,
                new Color(1f, 0.7f, 0.2f, 0.9f), 0.3f, 3);
        }

        if (u >= 1f)
        {
            Explode();
        }
    }

    // Rakieta trafia pierwszego napotkanego gracza lub bota (poza strzelającym) i wybucha na jego polu.
    bool TryHitPlayer(Vector3 path)
    {
        MatchManager match = MatchManager.Instance;

        if (match == null)
        {
            return false;
        }

        foreach (Combatant other in match.Slots)
        {
            if (other == null || !other.IsAlive || other.health.transform == shooter)
            {
                continue;
            }

            Vector3 position = other.health.transform.position;

            if (Vector2.Distance(path, position) >= 0.55f)
            {
                continue;
            }

            landing = GridMap.Instance.WorldToGrid(position);

            if (blastRange > 0)
            {
                BigExplosion();
            }
            else
            {
                SpawnFlame(landing);
            }

            return true;
        }

        return false;
    }

    void Explode()
    {
        if (blastRange > 0)
        {
            BigExplosion();
        }
        else
        {
            SmallImpact();
        }

        Destroy(gameObject);
    }

    void BigExplosion()
    {
        if (BombRegistry.TryGet(landing, out Bomb existing))
        {
            existing.Trigger();
        }
        else if (bombPrefab != null)
        {
            Bomb bomb = Instantiate(bombPrefab);
            bomb.Init(landing, FuseAfterLanding, blastRange);
        }
    }

    void SmallImpact()
    {
        GridMap map = GridMap.Instance;
        Vector2Int obstacle = landing + direction;

        // pole, na którym stoi strzelający, zostaje bez ognia
        if (landing != start)
        {
            SpawnFlame(landing);
        }

        if (BombRegistry.TryGet(obstacle, out Bomb bomb))
        {
            bomb.Trigger();
        }
        else if (TileRules.IsDestructible(map.GetTileType(obstacle)))
        {
            SpawnFlame(obstacle);
            map.DestroyBlock(obstacle, this);
        }
    }

    void SpawnFlame(Vector2Int cell)
    {
        if (bombPrefab == null || bombPrefab.flamePrefab == null)
        {
            return;
        }

        Flame flame = Instantiate(bombPrefab.flamePrefab);
        flame.Init(cell);
    }
}
