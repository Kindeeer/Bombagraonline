using UnityEngine;

public class Flame : MonoBehaviour
{
    public float duration = 0.45f;
    public float sortOffsetY = 0.02f;

    Vector2Int cell;
    float timeLeft;
    bool active;

    public void Init(Vector2Int flameCell)
    {
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