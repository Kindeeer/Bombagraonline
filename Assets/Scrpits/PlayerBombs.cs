using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerBombs : MonoBehaviour
{
    public float fuseTime = 2f;
    public int range = 2;

    PlayerMovement movement;
    Bomb currentBomb;

    void Start()
    {
        movement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (SpacePressed() && currentBomb == null)
        {
            currentBomb = PlaceBomb(movement.currentCell);
        }
    }

    bool SpacePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    Bomb PlaceBomb(Vector2Int cell)
    {
        GameObject bombObject = new GameObject("Bomb");
        bombObject.transform.position = new Vector3(cell.x, cell.y, 0);
        bombObject.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

        SpriteRenderer sr = bombObject.AddComponent<SpriteRenderer>();
        sr.sprite = GridMap.Instance.squareSprite;
        sr.color = Color.black;
        sr.sortingOrder = 5;

        Bomb bomb = bombObject.AddComponent<Bomb>();
        bomb.Setup(cell, fuseTime, range);
        return bomb;
    }
}
