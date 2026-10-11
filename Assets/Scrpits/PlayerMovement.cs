using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerMovement : MonoBehaviour
{
    public Vector2Int startCell = new Vector2Int(1, 1);
    public float baseSpeed = 5f;

    public Vector2Int currentCell;
    Vector2Int targetCell;
    bool isMoving = false;

    void Start()
    {
        currentCell = startCell;
        targetCell = startCell;
        // Użycie GridToWorld centruje gracza na pierwszej kratce
        transform.position = GridMap.Instance.GridToWorld(startCell);
    }

    void Update()
    {
        float currentSpeed = CalculateSpeed();

        if (!isMoving)
        {
            Vector2Int direction = ReadDirection();

            if (direction != Vector2Int.zero)
            {
                Vector2Int nextCell = currentCell + direction;

                if (GridMap.Instance.CanEnter(nextCell))
                {
                    targetCell = nextCell;
                    isMoving = true;
                }
            }
        }

        if (isMoving)
        {
            // Prowadź postać do wycentrowanego punktu docelowego
            Vector3 targetPosition = GridMap.Instance.GridToWorld(targetCell);
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, currentSpeed * Time.deltaTime);

            if (transform.position == targetPosition)
            {
                currentCell = targetCell;
                isMoving = false;
            }
        }
    }

    Vector2Int ReadDirection()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y = 1;
            else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y = -1;
            else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x = -1;
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x = 1;
        }
#else
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        if (input.x != 0) return new Vector2Int((int)Mathf.Sign(input.x), 0);
        if (input.y != 0) return new Vector2Int(0, (int)Mathf.Sign(input.y));
        return Vector2Int.zero;
    }

    float CalculateSpeed()
    {
        TileType tile = GridMap.Instance.GetTileType(currentCell);

        if (tile == TileType.HoneyPuddle)
        {
            return baseSpeed * 0.5f; // Spowolnienie 50%
        }
        if (tile == TileType.SodaPuddle)
        {
            return baseSpeed * 1.6f; // Przyspieszenie 60%
        }

        return baseSpeed;
    }
}