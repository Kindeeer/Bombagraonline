using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerStats))]
public class BombController : MonoBehaviour
{
    public Bomb bombPrefab;

    PlayerStats stats;
    int activeBombs;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();

        if (bombPrefab == null)
        {
            Debug.LogError("BombController: brakuje Bomb Prefab.", this);
        }
    }

    void Update()
    {
        if (PlaceKeyPressed())
        {
            TryPlaceBomb();
        }
    }

    bool PlaceKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    void TryPlaceBomb()
    {
        if (activeBombs >= stats.maxBombs)
        {
            return;
        }

        Vector2Int cell = GridMap.Instance.WorldToGrid(transform.position);

        if (BombRegistry.HasBomb(cell))
        {
            return;
        }

        Bomb bomb = Instantiate(bombPrefab);
        bomb.Init(cell, stats.fuseTime, stats.blastRange);
        bomb.Exploded += OnBombExploded;
        activeBombs++;
    }

    void OnBombExploded(Bomb bomb)
    {
        activeBombs--;
    }
}
