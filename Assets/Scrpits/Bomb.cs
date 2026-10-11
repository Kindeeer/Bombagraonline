using System;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    public Flame flamePrefab;
    public float chainDelay = 0.08f;
    public float sortOffsetY = 0.02f;

    public event Action<Bomb> Exploded;
    public Vector2Int Cell { get; private set; }

    float timeLeft;
    int range;
    bool hasExploded;

    public void Init(Vector2Int cell, float fuseTime, int blastRange)
    {
        Cell = cell;
        timeLeft = fuseTime;
        range = blastRange;

        transform.position = GridMap.Instance.GridToWorld(cell) + Vector3.up * sortOffsetY;
        GridMap.Instance.SetBomb(cell, true);
        BombRegistry.Register(this);
    }

    void Update()
    {
        if (hasExploded)
        {
            return;
        }

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            Explode();
        }
    }

    void OnDestroy()
    {
        BombRegistry.Unregister(this);
    }

    public void Trigger()
    {
        if (hasExploded)
        {
            return;
        }

        timeLeft = Mathf.Min(timeLeft, chainDelay);
    }

    void Explode()
    {
        hasExploded = true;

        BombRegistry.Unregister(this);
        GridMap.Instance.SetBomb(Cell, false);

        BlastResult blast = BlastCalculator.Calculate(Cell, range);

        foreach (Vector2Int cell in blast.flameCells)
        {
            Flame flame = Instantiate(flamePrefab);
            flame.Init(cell);
        }

        foreach (Vector2Int cell in blast.blocksToDestroy)
        {
            GridMap.Instance.DestroyBlock(cell);
        }

        foreach (Bomb other in blast.bombsToTrigger)
        {
            other.Trigger();
        }

        Exploded?.Invoke(this);
        Destroy(gameObject);
    }
}