using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerMovement : MonoBehaviour
{
    public Vector2Int startCell = new Vector2Int(1, 1);
    public float speed = 5f;

    public Vector2Int currentCell;
    Vector2Int targetCell;
    bool isMoving = false;

    void Start()
    {
        currentCell = startCell;
        targetCell = startCell;
        transform.position = new Vector3(startCell.x, startCell.y, 0);
    }

    void Update()
    {
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
            Vector3 targetPosition = new Vector3(targetCell.x, targetCell.y, 0);
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

            if (transform.position == targetPosition)
            {
                currentCell = targetCell;
                isMoving = false;
            }
        }
    }

    Vector2Int ReadDirection()
    {
        bool left, right, up, down;

#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k == null)
        {
            return Vector2Int.zero;
        }
        left = k.leftArrowKey.isPressed || k.aKey.isPressed;
        right = k.rightArrowKey.isPressed || k.dKey.isPressed;
        up = k.upArrowKey.isPressed || k.wKey.isPressed;
        down = k.downArrowKey.isPressed || k.sKey.isPressed;
#else
        left = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        right = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        up = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
        down = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
#endif

        if (up) return Vector2Int.up;
        if (down) return Vector2Int.down;
        if (left) return Vector2Int.left;
        if (right) return Vector2Int.right;
        return Vector2Int.zero;
    }
}
