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

    const float HalfSize = 0.5f;

    [Header("Skręcanie")]
    [Tooltip("Przy dwóch wciśniętych klawiszach: jak blisko środka pola trzeba być, żeby skręcić w nowszy kierunek (0.5 = zawsze).")]
    [Range(0f, 0.5f)] public float turnTolerance = 0.25f;
    [Tooltip("Jak blisko sąsiedniego pola trzeba być, żeby postać sama ześlizgnęła się w wolne przejście obok (0 = wcale, 0.5 = zawsze).")]
    [Range(0f, 0.5f)] public float cornerAssist = 0.3f;

    public Vector2Int currentCell;

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
        Facing = ToFacing(direction);

        float distance = CalculateSpeed() * Mathf.Min(Time.deltaTime, 0.05f);
        Vector2 startPosition = position;

        Move(ref position, direction, distance);

        IsMoving = (position - startPosition).sqrMagnitude > 0.000001f;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        currentCell = GridMap.Instance.WorldToGrid(position);
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

    bool Collides(Vector2 position, Vector2 current)
    {
        GetCellRange(position, out int minX, out int maxX, out int minY, out int maxY);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (IsSolid(new Vector2Int(x, y), current))
                {
                    return true;
                }
            }
        }

        return false;
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

    static Vector2Int ReadInput()
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