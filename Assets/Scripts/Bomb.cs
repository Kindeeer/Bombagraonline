using System.Collections.Generic;
using System;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    public Flame flamePrefab;
    public float chainDelay = 0.08f;
    public float sortOffsetY = 0.02f;
    [Tooltip("O ile podnosimy kolejność rysowania bomby, żeby sortowała się z blokami i postaciami według osi Y, a nie leżała pod blokami.")]
    public int sortingBoost = 1;

    bool sortingApplied;

    public event Action<Bomb> Exploded;
    public Vector2Int Cell { get; private set; }
    public int Range => range;
    public float TimeLeft => timeLeft;

    float timeLeft;
    int range;
    bool hasExploded;

    public void Init(Vector2Int cell, float fuseTime, int blastRange)
    {
        if (!sortingApplied)
        {
            foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                spriteRenderer.sortingOrder += sortingBoost;
            }

            sortingApplied = true;

            // grafika bomby z GameArt (puste = grafika z prefabu)
            GameArt art = GameArt.Instance;
            SpriteRenderer mainRenderer = GetComponent<SpriteRenderer>();

            Sprite bombSprite = art != null ? art.BombSprite : null;

            if (bombSprite != null && mainRenderer != null)
            {
                mainRenderer.sprite = bombSprite;
            }
        }

        Cell = cell;
        timeLeft = fuseTime;
        range = blastRange;

        transform.position = GridMap.Instance.GridToWorld(cell) + Vector3.up * sortOffsetY;
        GridMap.Instance.SetBomb(cell, true);
        BombRegistry.Register(this);
    }

    void Update()
    {
        if (hasExploded)
        {
            return;
        }

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            Explode();
            return;
        }

        UpdateSlide();
    }

    // ---------- Odkopywanie (przedmiot "Kopanie bomb") ----------

    [Tooltip("Prędkość odkopniętej bomby w polach na sekundę.")]
    public float kickSpeed = 7f;

    Vector2Int slideDirection;
    bool sliding;
    Vector3 slideFrom;
    Vector3 slideTo;
    float slideProgress;

    // Odkopuje bombę w podanym kierunku. Jedzie pole po polu, aż napotka przeszkodę, postać albo ogień.
    public void Kick(Vector2Int direction)
    {
        if (hasExploded || direction == Vector2Int.zero)
        {
            return;
        }

        slideDirection = direction;

        if (!sliding)
        {
            TryStartSlideStep();
        }
    }

    void TryStartSlideStep()
    {
        Vector2Int next = Cell + slideDirection;

        if (!CanSlideInto(next))
        {
            slideDirection = Vector2Int.zero;
            sliding = false;
            return;
        }

        // pole logicznie zmienia się od razu, a grafika dojeżdża do niego płynnie
        GridMap.Instance.SetBomb(Cell, false);
        BombRegistry.Unregister(this);
        Cell = next;
        GridMap.Instance.SetBomb(Cell, true);
        BombRegistry.Register(this);

        slideFrom = transform.position;
        slideTo = GridMap.Instance.GridToWorld(Cell) + Vector3.up * sortOffsetY;
        slideProgress = 0f;
        sliding = true;
    }

    // Wolne pole bez bomby, ściany, bloku i ognia, na którym nie stoi żadna żywa postać.
    bool CanSlideInto(Vector2Int cell)
    {
        GridMap map = GridMap.Instance;

        if (!map.CanEnter(cell) || map.IsOnFire(cell))
        {
            return false;
        }

        MatchManager match = MatchManager.Instance;

        if (match != null)
        {
            Vector3 center = map.GridToWorld(cell);

            foreach (Combatant combatant in match.Slots)
            {
                if (combatant == null || !combatant.IsAlive)
                {
                    continue;
                }

                Vector3 offset = combatant.health.transform.position - center;

                if (Mathf.Abs(offset.x) < 0.9f && Mathf.Abs(offset.y) < 0.9f)
                {
                    return false;
                }
            }
        }

        return true;
    }

    void UpdateSlide()
    {
        if (!sliding)
        {
            return;
        }

        slideProgress += kickSpeed * Time.deltaTime;
        transform.position = Vector3.Lerp(slideFrom, slideTo, Mathf.Clamp01(slideProgress));

        if (slideProgress >= 1f)
        {
            sliding = false;

            if (slideDirection != Vector2Int.zero)
            {
                TryStartSlideStep();
            }
        }
    }

    void OnDestroy()
    {
        BombRegistry.Unregister(this);
    }

    public void Trigger()
    {
        if (hasExploded)
        {
            return;
        }

        timeLeft = Mathf.Min(timeLeft, chainDelay);
    }

    void Explode()
    {
        hasExploded = true;

        BombRegistry.Unregister(this);
        GridMap.Instance.SetBomb(Cell, false);

        BlastResult blast = BlastCalculator.Calculate(Cell, range);

        foreach (Vector2Int cell in blast.flameCells)
        {
            Flame flame = Instantiate(flamePrefab);
            flame.Init(cell);
        }

        foreach (Vector2Int cell in blast.blocksToDestroy)
        {
            GridMap.Instance.DestroyBlock(cell, this);
        }

        foreach (Bomb other in blast.bombsToTrigger)
        {
            other.Trigger();
        }

        Exploded?.Invoke(this);
        Destroy(gameObject);
    }
}

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

    // Wszystkie bomby na planszy (do liczenia pól zagrożonych przez boty).
    public static Dictionary<Vector2Int, Bomb>.ValueCollection All => bombs.Values;

    public static bool HasBomb(Vector2Int cell)
    {
        return bombs.ContainsKey(cell);
    }

    public static bool TryGet(Vector2Int cell, out Bomb bomb)
    {
        return bombs.TryGetValue(cell, out bomb);
    }
}

public class BlastResult
{
    public readonly List<Vector2Int> flameCells = new List<Vector2Int>();
    public readonly List<Vector2Int> blocksToDestroy = new List<Vector2Int>();
    public readonly List<Bomb> bombsToTrigger = new List<Bomb>();

    public void Clear()
    {
        flameCells.Clear();
        blocksToDestroy.Clear();
        bombsToTrigger.Clear();
    }
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
        Calculate(origin, range, result);
        return result;
    }

    // Wersja bez alokacji: czyści i wypełnia podany wynik. Używa jej AI botów kilka razy na sekundę.
    public static void Calculate(Vector2Int origin, int range, BlastResult result)
    {
        result.Clear();
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
    }
}
