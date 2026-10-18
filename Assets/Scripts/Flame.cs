using UnityEngine;

public class Flame : MonoBehaviour
{
    public float duration = 0.45f;
    public float sortOffsetY = 0.02f;
    // Płomień sortuje się z blokami i postaciami według osi Y (ta sama kolejność rysowania co oni), więc stojąca niżej
    // postać zasłania płomień z pola nad sobą, a nie odwrotnie.
    const int SortingBoost = 1;

    Vector2Int cell;
    float timeLeft;
    bool active;
    bool sortingApplied;

    public void Init(Vector2Int flameCell)
    {
        if (!sortingApplied)
        {
            foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                spriteRenderer.sortingOrder += SortingBoost;
            }

            sortingApplied = true;

            // grafika ognia z GameArt (puste = grafika z prefabu)
            GameArt art = GameArt.Instance;
            SpriteRenderer mainRenderer = GetComponent<SpriteRenderer>();

            Sprite flameSprite = art != null ? art.FlameSprite : null;

            if (flameSprite != null && mainRenderer != null)
            {
                mainRenderer.sprite = flameSprite;
            }
        }

        cell = flameCell;
        timeLeft = duration;
        active = true;

        transform.position = GridMap.Instance.GridToWorld(cell) + Vector3.up * sortOffsetY;
        GridMap.Instance.AddFire(cell);
    }

    void Update()
    {
        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            Extinguish();
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        Extinguish();
    }

    void Extinguish()
    {
        if (!active)
        {
            return;
        }

        active = false;

        if (GridMap.Instance != null)
        {
            GridMap.Instance.RemoveFire(cell);
        }
    }
}