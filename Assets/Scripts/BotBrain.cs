using System;
using UnityEngine;

// Pola zagrożone wybuchem: suma zasięgów wszystkich bomb na planszy. Liczona raz na klatkę dla wszystkich botów.
public static class DangerMap
{
    static readonly bool[] danger = new bool[GridMap.Width * GridMap.Height];
    static readonly BlastResult scratch = new BlastResult();
    static int builtFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Array.Clear(danger, 0, danger.Length);
        builtFrame = -1;
    }

    // Wymusza przeliczenie (np. zaraz po postawieniu bomby przez bota).
    public static void Invalidate()
    {
        builtFrame = -1;
    }

    public static bool IsDangerous(Vector2Int cell)
    {
        Build();

        if (cell.x < 0 || cell.y < 0 || cell.x >= GridMap.Width || cell.y >= GridMap.Height)
        {
            return true;
        }

        return danger[cell.y * GridMap.Width + cell.x] || GridMap.Instance.IsOnFire(cell);
    }

    static void Build()
    {
        if (builtFrame == Time.frameCount)
        {
            return;
        }

        builtFrame = Time.frameCount;
        Array.Clear(danger, 0, danger.Length);

        // reakcja łańcuchowa nic tu nie zmienia: każda bomba i tak jest na liście, a suma zasięgów to cały obszar
        foreach (Bomb bomb in BombRegistry.All)
        {
            BlastCalculator.Calculate(bomb.Cell, bomb.Range, scratch);

            foreach (Vector2Int cell in scratch.flameCells)
            {
                danger[cell.y * GridMap.Width + cell.x] = true;
            }
        }
    }
}

// Prosta sztuczna inteligencja bota, który chce zabić przeciwnika (także kosztem siebie).
// Decyzje podejmuje kilka razy na sekundę. Ruchem steruje przez PlayerMovement.aiInput,
// a bomby stawia przez BombController.PlaceBomb, więc bot podlega tym samym zasadom co gracz.
[RequireComponent(typeof(PlayerMovement), typeof(BombController))]
public class BotBrain : MonoBehaviour
{
    const int Width = GridMap.Width;
    const int Height = GridMap.Height;
    const int CellCount = Width * Height;
    const int LongestSide = Width > Height ? Width : Height;

    static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    [Header("Zachowanie")]
    [Tooltip("Co ile sekund bot podejmuje decyzję.")]
    public float thinkInterval = 0.12f;
    [Tooltip("Szansa, że w danej chwili bot jest w nastroju kamikadze (ignoruje własne bomby).")]
    [Range(0f, 1f)] public float recklessness = 0.25f;
    [Tooltip("Co ile sekund bot losuje nastrój od nowa.")]
    public float moodSeconds = 3f;
    [Tooltip("Do jak dalekich przedmiotów (w polach) bot się fatyguje.")]
    public int pickupSearchSteps = 5;
    [Tooltip("Przerwa po postawieniu bomby, zanim bot postawi kolejną.")]
    public float bombCooldown = 0.6f;
    [Tooltip("Tyle sekund po starcie bot nie stawia bomb (żeby nie wysadził się w rogu zanim się rozejrzy).")]
    public float spawnGrace = 1.5f;

    PlayerMovement movement;
    BombController bombs;
    PlayerStats stats;
    PlayerHealth health;
    PlayerPickups pickups;

    float specialCooldownUntil;
    float nextThink;
    float spawnTime;
    float moodUntil;
    bool reckless;
    float bombCooldownUntil;
    float wanderUntil;
    Vector2Int wanderDirection;
    Vector2Int lastCell = new Vector2Int(-1, -1);
    float lastCellTime;

    // bufory wyszukiwania w szerz (BFS), użyte ponownie, żeby nie tworzyć nowych tablic
    readonly int[] parent = new int[CellCount];
    readonly int[] distance = new int[CellCount];
    readonly int[] queue = new int[CellCount];
    readonly bool[] extraDanger = new bool[CellCount];
    readonly int[] escapeDistance = new int[CellCount];
    readonly int[] escapeQueue = new int[CellCount];
    readonly int[] enemyIndices = new int[MatchManager.MaxSlots];
    readonly BlastResult scratch = new BlastResult();
    int queueLength;
    int enemyCount;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        bombs = GetComponent<BombController>();
        stats = GetComponent<PlayerStats>();
        health = GetComponent<PlayerHealth>();
        pickups = GetComponent<PlayerPickups>();
        spawnTime = Time.time;
    }

    void Update()
    {
        // na starcie rundy (do napisu GO!) boty stoją w miejscu
        if (MatchManager.InputLocked || DebugMenu.FreezeBots)
        {
            movement.aiInput = Vector2Int.zero;
            return;
        }

        MatchManager match = MatchManager.Instance;

        if (health.IsDead || match == null || match.RoundOver)
        {
            movement.aiInput = Vector2Int.zero;
            return;
        }

        if (Time.time < nextThink)
        {
            return;
        }

        nextThink = Time.time + thinkInterval + UnityEngine.Random.Range(0f, 0.05f);
        Think(match);
    }

    void Think(MatchManager match)
    {
        GridMap map = GridMap.Instance;
        Vector2Int cell = map.WorldToGrid(transform.position);

        if (Time.time >= moodUntil)
        {
            moodUntil = Time.time + moodSeconds;
            reckless = UnityEngine.Random.value < recklessness;
        }

        if (cell != lastCell)
        {
            lastCell = cell;
            lastCellTime = Time.time;
        }

        CollectEnemies(match, map);

        // 1. Stoję na polu zagrożonym: zawsze uciekam na najbliższe bezpieczne
        if (DangerMap.IsDangerous(cell) && TryFlee(cell))
        {
            return;
        }

        // 2. Szukam drogi do celów
        Search(cell, !reckless);

        // 2b. Przedmiot specjalny ze slotu Buff (rakieta, młotek)
        if (TrySpecial(cell))
        {
            return;
        }

        // 3. Bomba: tylko gdy da się uciec. Nastrój kamikadze pozwala zaryzykować tylko wtedy,
        //    gdy przeciwnik stoi w linii wybuchu (wtedy bot może zginąć razem z nim).
        if (CanPlaceBombNow(cell))
        {
            bool enemyInLine = EnemyInLine(cell, stats.blastRange);
            bool wantsBomb = enemyInLine || (HasAdjacentBlock(cell) && !EnemyReachable());

            if (wantsBomb && (CanEscapeAfterBomb(cell) || (reckless && enemyInLine)) && bombs.PlaceBomb())
            {
                bombCooldownUntil = Time.time + bombCooldown;
                DangerMap.Invalidate();
                TryFlee(cell);
                return;
            }
        }

        // 4. Utknąłem (np. w wąskim przejściu na innym graczu): chwilę chodzę losowo, potem wracam do celu
        if (Time.time - lastCellTime > 1.5f && wanderUntil < Time.time)
        {
            wanderUntil = Time.time + 1f;
            wanderDirection = Vector2Int.zero;
            lastCellTime = Time.time;
        }

        if (Time.time < wanderUntil)
        {
            Wander(cell);
            return;
        }

        // 5. Idę do najbliższego celu
        MoveTowardGoal(cell);
    }

    // ---------- Przeciwnicy ----------

    void CollectEnemies(MatchManager match, GridMap map)
    {
        enemyCount = 0;

        foreach (Combatant other in match.Slots)
        {
            if (other == null || !other.IsAlive || other.health == health)
            {
                continue;
            }

            Vector2Int enemyCell = map.WorldToGrid(other.health.transform.position);
            enemyIndices[enemyCount++] = Index(enemyCell);
        }
    }

    bool IsEnemyAt(int index)
    {
        for (int i = 0; i < enemyCount; i++)
        {
            if (enemyIndices[i] == index)
            {
                return true;
            }
        }

        return false;
    }

    // Czy przeciwnik stoi w linii zasięgu bomby (bez ścian i bloków pomiędzy).
    bool EnemyInLine(Vector2Int cell, int range)
    {
        if (IsEnemyAt(Index(cell)))
        {
            return true;
        }

        foreach (Vector2Int direction in Directions)
        {
            for (int step = 1; step <= range; step++)
            {
                Vector2Int next = cell + direction * step;
                TileType tile = GridMap.Instance.GetTileType(next);

                if (TileRules.IsIndestructible(tile) || TileRules.IsDestructible(tile))
                {
                    break;
                }

                if (IsEnemyAt(Index(next)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // ---------- Wyszukiwanie w szerz (BFS) ----------

    bool CanWalk(Vector2Int cell, bool avoidDanger)
    {
        GridMap map = GridMap.Instance;

        if (!map.CanEnter(cell) || map.IsOnFire(cell))
        {
            return false;
        }

        return !avoidDanger || !DangerMap.IsDangerous(cell);
    }

    // Wypełnia distance, parent i queue (pola w kolejności od najbliższego) dla pól osiągalnych ze start.
    void Search(Vector2Int start, bool avoidDanger)
    {
        Array.Fill(distance, -1);

        int startIndex = Index(start);
        int head = 0;
        queueLength = 0;

        distance[startIndex] = 0;
        parent[startIndex] = -1;
        queue[queueLength++] = startIndex;

        while (head < queueLength)
        {
            int current = queue[head++];
            Vector2Int currentCell = FromIndex(current);

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = currentCell + direction;

                if (next.x < 0 || next.y < 0 || next.x >= Width || next.y >= Height)
                {
                    continue;
                }

                int nextIndex = Index(next);

                if (distance[nextIndex] >= 0 || !CanWalk(next, avoidDanger))
                {
                    continue;
                }

                distance[nextIndex] = distance[current] + 1;
                parent[nextIndex] = current;
                queue[queueLength++] = nextIndex;
            }
        }
    }

    // Pierwszy krok na drodze od start do goalIndex (z drzewa rodziców zbudowanego przez Search).
    Vector2Int FirstStep(int startIndex, int goalIndex)
    {
        int step = goalIndex;

        while (parent[step] != startIndex && parent[step] >= 0)
        {
            step = parent[step];
        }

        return FromIndex(step) - FromIndex(startIndex);
    }

    // ---------- Ucieczka ----------

    bool TryFlee(Vector2Int cell)
    {
        // uciekając wolno przechodzić przez pola zagrożone, byle nie płonące
        Search(cell, false);

        int startIndex = Index(cell);

        for (int i = 1; i < queueLength; i++)
        {
            int index = queue[i];

            if (!DangerMap.IsDangerous(FromIndex(index)))
            {
                movement.aiInput = FirstStep(startIndex, index);
                return true;
            }
        }

        movement.aiInput = Vector2Int.zero;
        return false;
    }

    // ---------- Przedmioty specjalne ----------

    // Rakieta: strzela, gdy przeciwnik stoi w linii bez przeszkód. Młotek: rozbija blok lub filar,
    // który odcina drogę do przeciwnika. Przed użyciem bot musi patrzeć w dobrą stronę, więc najpierw
    // się obraca (wystarczy, że poda kierunek ruchu), a używa przy następnej decyzji.
    bool TrySpecial(Vector2Int cell)
    {
        if (pickups == null)
        {
            pickups = GetComponent<PlayerPickups>();
        }

        if (pickups == null || !pickups.HasSpecial || Time.time < specialCooldownUntil || enemyCount == 0)
        {
            return false;
        }

        // zamiana miejsc: bot używa jej od czasu do czasu albo gdy stoi w niebezpiecznym miejscu
        if (pickups.HeldItem.special == SpecialItem.Teleport)
        {
            if (DangerMap.IsDangerous(cell) || UnityEngine.Random.value < 0.06f)
            {
                specialCooldownUntil = Time.time + 1f;
                return pickups.UseSpecial();
            }

            return false;
        }

        Vector2Int direction = pickups.HeldItem.special == SpecialItem.Rocket ? RocketTarget(cell) : HammerTarget(cell);

        if (direction == Vector2Int.zero)
        {
            return false;
        }

        if (movement.Facing != FacingOf(direction))
        {
            movement.aiInput = direction;
            return true;
        }

        specialCooldownUntil = Time.time + (pickups.UseSpecial() ? 0.8f : 1f);
        return true;
    }

    // Kierunek, w którym stoi przeciwnik w linii rakiety (zero = brak celu).
    Vector2Int RocketTarget(Vector2Int cell)
    {
        // duży wybuch rakiety (rocketBlastRange) mógłby trafić też bota, więc strzela tylko z dalszej odległości
        int minDistance = pickups.HeldItem.rocketBlastRange > 0 ? pickups.HeldItem.rocketBlastRange + 1 : 1;

        foreach (Vector2Int direction in Directions)
        {
            for (int step = 1; step < LongestSide; step++)
            {
                Vector2Int next = cell + direction * step;

                if (IsEnemyAt(Index(next)))
                {
                    if (step >= minDistance)
                    {
                        return direction;
                    }

                    break;
                }

                if (!GridMap.Instance.CanEnter(next))
                {
                    break;
                }
            }
        }

        return Vector2Int.zero;
    }

    // Kierunek bloku lub filaru przy bocie, który przybliża go do przeciwnika, gdy droga jest zamknięta.
    Vector2Int HammerTarget(Vector2Int cell)
    {
        if (EnemyReachable())
        {
            return Vector2Int.zero;
        }

        Vector2Int bestDirection = Vector2Int.zero;
        int bestDistance = int.MaxValue;

        foreach (Vector2Int direction in Directions)
        {
            TileType tile = GridMap.Instance.GetTileType(cell + direction);

            if (!TileRules.IsDestructible(tile) && tile != TileType.Pillar)
            {
                continue;
            }

            for (int i = 0; i < enemyCount; i++)
            {
                Vector2Int enemy = FromIndex(enemyIndices[i]);
                Vector2Int diff = enemy - (cell + direction);
                int distance = Mathf.Abs(diff.x) + Mathf.Abs(diff.y);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestDirection = direction;
                }
            }
        }

        return bestDirection;
    }

    static Facing FacingOf(Vector2Int direction)
    {
        if (direction.x > 0) return Facing.Right;
        if (direction.x < 0) return Facing.Left;
        if (direction.y > 0) return Facing.Up;
        return Facing.Down;
    }

    // ---------- Bomby ----------

    bool CanPlaceBombNow(Vector2Int cell)
    {
        return Time.time >= bombCooldownUntil
            && Time.time - spawnTime >= spawnGrace
            && bombs.HasFreeBomb
            && !BombRegistry.HasBomb(cell);
    }

    bool EnemyReachable()
    {
        for (int i = 0; i < queueLength; i++)
        {
            if (IsEnemyAt(queue[i]))
            {
                return true;
            }
        }

        return false;
    }

    static bool HasAdjacentBlock(Vector2Int cell)
    {
        foreach (Vector2Int direction in Directions)
        {
            if (TileRules.IsDestructible(GridMap.Instance.GetTileType(cell + direction)))
            {
                return true;
            }
        }

        return false;
    }

    // Czy po postawieniu bomby jest dokąd uciec, zanim wybuchnie (liczone w krokach, które bot zdąży zrobić).
    // Ma własne bufory, więc nie niszczy wyniku ostatniego Search.
    bool CanEscapeAfterBomb(Vector2Int cell)
    {
        Array.Clear(extraDanger, 0, extraDanger.Length);
        BlastCalculator.Calculate(cell, stats.blastRange, scratch);

        foreach (Vector2Int flame in scratch.flameCells)
        {
            extraDanger[Index(flame)] = true;
        }

        int maxSteps = Mathf.Max(2, Mathf.FloorToInt(movement.baseSpeed * stats.fuseTime * 0.7f));

        Array.Fill(escapeDistance, -1);

        int startIndex = Index(cell);
        int head = 0;
        int length = 0;
        escapeDistance[startIndex] = 0;
        escapeQueue[length++] = startIndex;

        while (head < length)
        {
            int current = escapeQueue[head++];
            Vector2Int currentCell = FromIndex(current);

            if (current != startIndex && !extraDanger[current] && !DangerMap.IsDangerous(currentCell))
            {
                return true;
            }

            if (escapeDistance[current] >= maxSteps)
            {
                continue;
            }

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = currentCell + direction;

                if (next.x < 0 || next.y < 0 || next.x >= Width || next.y >= Height)
                {
                    continue;
                }

                int nextIndex = Index(next);

                if (escapeDistance[nextIndex] >= 0 || !CanWalk(next, false))
                {
                    continue;
                }

                escapeDistance[nextIndex] = escapeDistance[current] + 1;
                escapeQueue[length++] = nextIndex;
            }
        }

        return false;
    }

    // ---------- Ruch ----------

    void MoveTowardGoal(Vector2Int cell)
    {
        int startIndex = Index(cell);
        int pickup = -1;
        int enemy = -1;
        int frontier = -1;
        int frontierChecks = 0;

        for (int i = 1; i < queueLength; i++)
        {
            int index = queue[i];

            if (pickup < 0 && distance[index] <= pickupSearchSteps && PickupRegistry.TryGet(FromIndex(index), out PickupItem item) && item.CanCollect)
            {
                pickup = index;
            }

            if (enemy < 0 && IsEnemyAt(index))
            {
                enemy = index;
            }

            if (frontier < 0 && frontierChecks < 8 && HasAdjacentBlock(FromIndex(index)))
            {
                // sprawdzamy tylko kilka najbliższych miejsc, bo każde sprawdzenie to osobne przeszukiwanie
                frontierChecks++;

                if (CanEscapeAfterBomb(FromIndex(index)))
                {
                    frontier = index;
                }
            }
        }

        // priorytet: przedmiot w pobliżu, potem przeciwnik, potem miejsce przy bloku do rozwalenia
        int goal = pickup >= 0 ? pickup : enemy >= 0 ? enemy : frontier;

        // stoję już przy bloku do rozwalenia, a bomby jeszcze nie mogę postawić: czekam, zamiast się kręcić
        if (goal == frontier && pickup < 0 && enemy < 0 && HasAdjacentBlock(cell) && CanEscapeAfterBomb(cell))
        {
            movement.aiInput = Vector2Int.zero;
            return;
        }

        if (goal >= 0)
        {
            movement.aiInput = FirstStep(startIndex, goal);
            return;
        }

        Wander(cell);
    }

    // Brak celu (albo utknięcie): idzie w jedną stronę, aż napotka przeszkodę, potem losuje nową.
    void Wander(Vector2Int cell)
    {
        bool stuck = Time.time - lastCellTime > 2f;

        if (stuck || wanderDirection == Vector2Int.zero || !CanWalk(cell + wanderDirection, true))
        {
            wanderDirection = Vector2Int.zero;
            int start = UnityEngine.Random.Range(0, Directions.Length);

            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int candidate = Directions[(start + i) % Directions.Length];

                if (CanWalk(cell + candidate, true))
                {
                    wanderDirection = candidate;
                    break;
                }
            }

            if (stuck)
            {
                lastCellTime = Time.time;
            }
        }

        movement.aiInput = wanderDirection;
    }

    static int Index(Vector2Int cell) => cell.y * Width + cell.x;

    static Vector2Int FromIndex(int index) => new Vector2Int(index % Width, index / Width);
}