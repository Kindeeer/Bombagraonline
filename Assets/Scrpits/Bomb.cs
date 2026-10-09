using UnityEngine;

public class Bomb : MonoBehaviour
{
    Vector2Int cell;
    float timeLeft;
    int range;

    public void Setup(Vector2Int bombCell, float fuseTime, int blastRange)
    {
        cell = bombCell;
        timeLeft = fuseTime;
        range = blastRange;
        GridMap.Instance.SetBomb(cell, true);
    }

    void Update()
    {
        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            Explode();
        }
    }

    void Explode()
    {
        GridMap.Instance.SetBomb(cell, false);
        SpawnFlame(cell);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int direction in directions)
        {
            for (int i = 1; i <= range; i++)
            {
                Vector2Int flameCell = cell + direction * i;

                if (GridMap.Instance.IsWall(flameCell))
                {
                    break;
                }

                SpawnFlame(flameCell);
            }
        }

        Destroy(gameObject);
    }

    void SpawnFlame(Vector2Int flameCell)
    {
        GameObject flame = new GameObject("Flame");
        flame.transform.position = new Vector3(flameCell.x, flameCell.y, 0);
        flame.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

        SpriteRenderer sr = flame.AddComponent<SpriteRenderer>();
        sr.sprite = GridMap.Instance.squareSprite;
        sr.color = new Color(1f, 0.5f, 0f);
        sr.sortingOrder = 4;

        flame.AddComponent<Flame>().Setup(flameCell, 0.4f);
    }
}
