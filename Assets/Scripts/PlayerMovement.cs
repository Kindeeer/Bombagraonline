using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum Facing
{
    Down,
    Up,
    Left,
    Right
}

public class PlayerMovement : MonoBehaviour
{
    public Vector2Int startCell = new Vector2Int(1, 1);
    public float baseSpeed = 5f;

    [Header("Sterowanie")]
    [Tooltip("Zaznaczone: klawiatura. Odznaczone: kierunek podaje bot (aiInput).")]
    public bool useKeyboard = true;
    [HideInInspector] public Vector2Int aiInput;

    [Tooltip("Gracze i boty nie mogą wchodzić jeden w drugiego (wyjść z nałożenia można).")]
    public bool playersBlockEachOther = true;

    const float HalfSize = 0.5f;

    [Header("Skręcanie")]
    [Tooltip("Przy dwóch wciśniętych klawiszach: jak blisko środka pola trzeba być, żeby skręcić w nowszy kierunek (0.5 = zawsze).")]
    [Range(0f, 0.5f)] public float turnTolerance = 0.25f;
    [Tooltip("Jak blisko sąsiedniego pola trzeba być, żeby postać sama ześlizgnęła się w wolne przejście obok (0 = wcale, 0.5 = zawsze).")]
    [Range(0f, 0.5f)] public float cornerAssist = 0.3f;

    public Vector2Int currentCell;

    // Tryb debug (menu pod TAB): postać przechodzi przez bloki, ściany, bomby i innych graczy, ale zostaje w obrębie planszy.
    [HideInInspector] public bool noclip;

    public Facing Facing { get; private set; } = Facing.Down;
    public bool IsMoving { get; private set; }

    const float Edge = 0.0001f;
    const float Aligned = 0.001f;

    Vector2Int previousInput = Vector2Int.zero;
    bool newestIsHorizontal = true;

    void Start()
    {
        currentCell = startCell;
        transform.position = GridMap.Instance.GridToWorld(startCell);
    }

    void Update()
    {
        // na starcie rundy (do napisu GO!) postać stoi w miejscu
        if (MatchManager.InputLocked)
        {
            IsMoving = false;
            return;
        }

        Vector2 position = transform.position;
        Vector2Int input = ReadInput();
        UpdateNewestAxis(input);

        IsMoving = false;
        currentCell = GridMap.Instance.WorldToGrid(position);

        // Brak inputu - postać stoi
        if (input == Vector2Int.zero)
        {
            return;
        }

        Vector2Int direction = ChooseDirection(position, input);
        float distance = CalculateSpeed() * Mathf.Min(Time.deltaTime, 0.05f);
        Vector2 startPosition = position;

        Move(ref position, direction, distance);

        bool moved = (position - startPosition).sqrMagnitude > 0.000001f;
        bool twoKeys = input.x != 0 && input.y != 0;

        // dwa klawisze naraz (np. W + A), a wybrany kierunek jest zablokowany: próbujemy drugiej osi,
        // żeby postać nie utknęła w miejscu
        if (!moved && twoKeys)
        {
            Vector2Int other = direction.x != 0 ? new Vector2Int(0, input.y) : new Vector2Int(input.x, 0);
            Move(ref position, other, distance);
            moved = (position - startPosition).sqrMagnitude > 0.000001f;

            if (moved)
            {
                direction = other;
            }
        }

        // postać stoi, bo obie strony są zablokowane: sprite i tak obraca się w stronę ostatnio wciśniętego klawisza
        if (!moved && twoKeys)
        {
            direction = newestIsHorizontal ? new Vector2Int(input.x, 0) : new Vector2Int(0, input.y);
        }

        Facing = ToFacing(direction);
        IsMoving = moved;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        currentCell = GridMap.Instance.WorldToGrid(position);

        // przedmiot "Kopanie bomb": idąc w bombę, której nie da się ominąć, postać ją odkopuje
        if (!moved)
        {
            TryKickBomb(position, direction);
        }
    }

    void TryKickBomb(Vector2 position, Vector2Int direction)
    {
        PlayerPickups pickups = PickupsOf();

        if (pickups == null || !pickups.CanKick || direction == Vector2Int.zero)
        {
            return;
        }

        // postać musi stać mniej więcej na środku pola, żeby kopnąć bombę prosto przed sobą
        if (Mathf.Abs(LateralOffset(position, direction)) > 0.3f)
        {
            return;
        }

        Vector2Int target = GridMap.Instance.WorldToGrid(position) + direction;

        if (BombRegistry.TryGet(target, out Bomb bomb))
        {
            bomb.Kick(direction);
        }
    }

    // "Ostatni klawisz wygrywa", ale przy dwóch klawiszach tylko wtedy, gdy da się tam od razu skręcić.
    // Inaczej postać idzie dalej starym kierunkiem i skręca sama, gdy dojdzie do wolnego przejścia.
    Vector2Int ChooseDirection(Vector2 position, Vector2Int input)
    {
        Vector2Int horizontal = new Vector2Int(input.x, 0);
        Vector2Int vertical = new Vector2Int(0, input.y);

        if (input.x == 0) return vertical;
        if (input.y == 0) return horizontal;

        Vector2Int newest = newestIsHorizontal ? horizontal : vertical;
        Vector2Int older = newestIsHorizontal ? vertical : horizontal;

        return CanTurn(position, newest) ? newest : older;
    }

    bool CanTurn(Vector2 position, Vector2Int direction)
    {
        if (Mathf.Abs(LateralOffset(position, direction)) > turnTolerance)
        {
            return false;
        }

        Vector2Int cell = GridMap.Instance.WorldToGrid(position);
        return !IsSolid(cell + direction, position);
    }

    void UpdateNewestAxis(Vector2Int input)
    {
        if (input.x != 0 && previousInput.x == 0)
        {
            newestIsHorizontal = true;
        }

        if (input.y != 0 && previousInput.y == 0)
        {
            newestIsHorizontal = false;
        }

        previousInput = input;
    }

    // Odległość od środka pola w osi prostopadłej do ruchu.
    static float LateralOffset(Vector2 position, Vector2Int direction)
    {
        float lateral = direction.x != 0 ? position.y : position.x;
        return lateral - (Mathf.Floor(lateral) + 0.5f);
    }

    // Wolna droga prosto: postać idzie od razu, także gdy stoi między polami (np. na otwartym miejscu po rozbitej blokadzie).
    // Dopiero gdy coś blokuje, ześlizguje się na środek wolnego przejścia (z prędkością chodzenia) i idzie dalej.
    void Move(ref Vector2 position, Vector2Int direction, float distance)
    {
        if (noclip)
        {
            Vector2 free = position + new Vector2(direction.x, direction.y) * distance;
            position = new Vector2(
                Mathf.Clamp(free.x, HalfSize, GridMap.Width - HalfSize),
                Mathf.Clamp(free.y, HalfSize, GridMap.Height - HalfSize));
            return;
        }

        Vector2 straight = position + new Vector2(direction.x, direction.y) * distance;

        if (!Collides(straight, position))
        {
            position = straight;
            return;
        }

        bool horizontal = direction.x != 0;
        float lateral = horizontal ? position.y : position.x;
        float offset = LateralOffset(position, direction);
        float lane = lateral - offset;

        if (Mathf.Abs(offset) > Aligned)
        {
            if (!TryFindLane(position, direction, offset, ref lane))
            {
                return;
            }

            float toLane = lane - lateral;
            float slide = Mathf.Min(distance, Mathf.Abs(toLane));
            lateral += Mathf.Sign(toLane) * slide;
            distance -= slide;

            if (Mathf.Abs(lane - lateral) > Aligned)
            {
                position = horizontal ? new Vector2(position.x, lateral) : new Vector2(lateral, position.y);
                return;
            }
        }

        position = horizontal ? new Vector2(position.x, lane) : new Vector2(lane, position.y);

        if (distance > 0f)
        {
            MoveAlong(ref position, direction, distance);
        }
    }

    // Przejście przed własnym polem albo (pomoc na rogach) przed polem obok, na które postać już zachodzi.
    bool TryFindLane(Vector2 position, Vector2Int direction, float offset, ref float lane)
    {
        Vector2Int cell = GridMap.Instance.WorldToGrid(position);

        if (!IsSolid(cell + direction, position))
        {
            return true;
        }

        if (Mathf.Abs(offset) < 0.5f - cornerAssist)
        {
            return false;
        }

        int side = offset > 0f ? 1 : -1;
        Vector2Int sideCell = direction.x != 0 ? cell + new Vector2Int(0, side) : cell + new Vector2Int(side, 0);

        if (IsSolid(sideCell, position) || IsSolid(sideCell + direction, position))
        {
            return false;
        }

        lane += side;
        return true;
    }

    bool MoveAlong(ref Vector2 position, Vector2Int direction, float distance)
    {
        Vector2 start = position;
        Vector2 step = new Vector2(direction.x, direction.y);
        Vector2 target = start + step * distance;

        if (!Collides(target, start))
        {
            position = target;
            return true;
        }

        float free = 0f;
        float blocked = distance;

        for (int i = 0; i < 8; i++)
        {
            float middle = (free + blocked) * 0.5f;

            if (Collides(start + step * middle, start))
            {
                blocked = middle;
            }
            else
            {
                free = middle;
            }
        }

        position = start + step * free;
        return free > 0.00001f;
    }

    // Inny żywy gracz lub bot blokuje wejście w jego ciało, ale kto już na nim stoi, może się odsunąć
    // (tak samo jak przy bombie). Margines 0.98 sprawia, że postacie stojące obok siebie się nie zakleszczają.
    bool BlockedByPlayer(Vector2 position, Vector2 current)
    {
        MatchManager match = MatchManager.Instance;

        if (!playersBlockEachOther || match == null)
        {
            return false;
        }

        foreach (Combatant other in match.Slots)
        {
            if (other == null || !other.IsAlive || other.health.transform == transform)
            {
                continue;
            }

            Vector2 otherPosition = other.health.transform.position;

            bool overlapsNow = Mathf.Abs(position.x - otherPosition.x) < 0.98f && Mathf.Abs(position.y - otherPosition.y) < 0.98f;
            bool overlappedBefore = Mathf.Abs(current.x - otherPosition.x) < 0.98f && Mathf.Abs(current.y - otherPosition.y) < 0.98f;

            if (overlapsNow && !overlappedBefore)
            {
                return true;
            }
        }

        return false;
    }

    bool Collides(Vector2 position, Vector2 current)
    {
        if (BlockedByPlayer(position, current))
        {
            return true;
        }

        GetCellRange(position, out int minX, out int maxX, out int minY, out int maxY);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                if (GridMap.Instance.IsWall(cell) || BombBlocks(cell, position, current))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Bomba blokuje ruch. Wyjątek: kto jeszcze stoi na bombie (właśnie ją postawił), może z niej zejść,
    // ale tylko "na zewnątrz": ruch, który zwiększałby nachodzenie na bombę (np. powrót przez nią), jest zablokowany.
    // Dzięki temu nie da się przejść przez bombę, gdy postać zdąży zejść tylko do połowy.
    static bool BombBlocks(Vector2Int cell, Vector2 position, Vector2 current)
    {
        if (!BombRegistry.HasBomb(cell))
        {
            return false;
        }

        if (!Overlaps(current, cell))
        {
            return true;
        }

        return Penetration(position, cell) > Penetration(current, cell) + 0.0001f;
    }

    // Pole powierzchni, na którym ciało postaci (kwadrat o boku 1) zachodzi na pole.
    static float Penetration(Vector2 position, Vector2Int cell)
    {
        float overlapX = Mathf.Max(0f, 1f - Mathf.Abs(position.x - (cell.x + 0.5f)));
        float overlapY = Mathf.Max(0f, 1f - Mathf.Abs(position.y - (cell.y + 0.5f)));
        return overlapX * overlapY;
    }

    bool IsSolid(Vector2Int cell, Vector2 current)
    {
        if (GridMap.Instance.IsWall(cell))
        {
            return true;
        }

        return BombRegistry.HasBomb(cell) && !Overlaps(current, cell);
    }

    static bool Overlaps(Vector2 position, Vector2Int cell)
    {
        GetCellRange(position, out int minX, out int maxX, out int minY, out int maxY);
        return cell.x >= minX && cell.x <= maxX && cell.y >= minY && cell.y <= maxY;
    }

    static void GetCellRange(Vector2 position, out int minX, out int maxX, out int minY, out int maxY)
    {
        minX = Mathf.FloorToInt(position.x - HalfSize + Edge);
        maxX = Mathf.FloorToInt(position.x + HalfSize - Edge);
        minY = Mathf.FloorToInt(position.y - HalfSize + Edge);
        maxY = Mathf.FloorToInt(position.y + HalfSize - Edge);
    }

    static Facing ToFacing(Vector2Int direction)
    {
        if (direction.x > 0) return Facing.Right;
        if (direction.x < 0) return Facing.Left;
        if (direction.y > 0) return Facing.Up;
        return Facing.Down;
    }

    Vector2Int ReadInput()
    {
        if (!useKeyboard)
        {
            return aiInput;
        }

        // klawiatura ma pierwszeństwo, a gdy nic nie wciśnięto, czytamy joystick ekranowy
        Vector2Int keys = ReadKeyboard();
        Vector2Int result = keys != Vector2Int.zero ? keys : TouchInput.Move;

        // klątwa "zawroty głowy": sterowanie jest odwrócone
        PlayerPickups pickups = PickupsOf();

        if (result != Vector2Int.zero && pickups != null && pickups.ReverseControls)
        {
            result = -result;
        }

        return result;
    }

    PlayerPickups cachedPickups;

    // Komponent przedmiotów tej postaci (dodaje go PlayerStats, więc szukamy leniwie).
    PlayerPickups PickupsOf()
    {
        if (cachedPickups == null)
        {
            cachedPickups = GetComponent<PlayerPickups>();
        }

        return cachedPickups;
    }

    Vector2Int ReadKeyboard()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return Vector2Int.zero;
        }

        int x = 0;
        int y = 0;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1;

        return new Vector2Int(x, y);
#else
        return new Vector2Int((int)Input.GetAxisRaw("Horizontal"), (int)Input.GetAxisRaw("Vertical"));
#endif
    }

    float CalculateSpeed()
    {
        TileType tile = GridMap.Instance.GetTileType(currentCell);

        if (tile == TileType.HoneyPuddle)
        {
            return baseSpeed * 0.5f;
        }

        if (tile == TileType.SodaPuddle)
        {
            return baseSpeed * 1.6f;
        }

        return baseSpeed;
    }
}