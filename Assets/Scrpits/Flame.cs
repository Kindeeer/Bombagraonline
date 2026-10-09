using UnityEngine;

public class Flame : MonoBehaviour
{
    Vector2Int cell;
    float timeLeft;

    public void Setup(Vector2Int flameCell, float duration)
    {
        cell = flameCell;
        timeLeft = duration;
        GridMap.Instance.AddFire(cell);
    }

    void Update()
    {
        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            GridMap.Instance.RemoveFire(cell);
            Destroy(gameObject);
        }
    }
}
