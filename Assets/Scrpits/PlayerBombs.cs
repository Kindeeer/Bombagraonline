using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerBombs : MonoBehaviour
{
    public float fuseTime = 2f;
    public int range = 2;

    [Header("Tekstura Bomby z Figmy")]
    public Sprite bombSprite;

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
        bombObject.transform.position = GridMap.Instance.GridToWorld(cell);

        // Skala 1f odpowiada rozmiarowi 1 kafelka przy odpowiednim PPU
        bombObject.transform.localScale = new Vector3(1f, 1f, 1f);

        SpriteRenderer sr = bombObject.AddComponent<SpriteRenderer>();

        // Użyj tekstury z Figmy (jeśli przypisana) lub czarnego kwadratu z GridMap
        sr.sprite = bombSprite != null ? bombSprite : GridMap.Instance.squareSprite;

        // Color.white zachowuje oryginalne, ładne kolory z Figmy!
        sr.color = Color.white;
        sr.sortingOrder = 5;

        Bomb bomb = bombObject.AddComponent<Bomb>();
        bomb.Setup(cell, fuseTime, range);
        return bomb;
    }
}