using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerStats))]
public class BombController : MonoBehaviour
{
    public Bomb bombPrefab;

    PlayerStats stats;
    PlayerPickups pickups;
    int activeBombs;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();

        if (bombPrefab == null)
        {
            Debug.LogError("BombController: brakuje Bomb Prefab.", this);
        }
    }

    void Start()
    {
        // PlayerPickups dokłada PlayerStats w Awake, więc szukamy go dopiero tutaj
        pickups = GetComponent<PlayerPickups>();
    }

    void Update()
    {
        if (PlaceKeyPressed())
        {
            OnActionPressed();
        }
    }

    // Spacja: przedmiot specjalny ze slotu Buff, a jeśli go nie ma, bomba (z premią z buffa NextBomb).
    void OnActionPressed()
    {
        if (pickups != null && pickups.HasSpecial)
        {
            pickups.UseSpecial();
            return;
        }

        int rangeBonus = 0;
        bool overLimit = false;

        if (pickups != null)
        {
            pickups.GetNextBombBonus(out rangeBonus, out overLimit);
        }

        bool atLimit = activeBombs >= stats.maxBombs;

        if (atLimit && !overLimit)
        {
            return;
        }

        // bomba ponad limit nie zajmuje miejsca w limicie
        bool countsTowardLimit = !atLimit;

        if (!TryPlaceBomb(stats.blastRange + rangeBonus, countsTowardLimit))
        {
            return;
        }

        // buff zużywa się tylko wtedy, gdy coś dał: dłuższy wybuch albo bombę ponad limit
        if (pickups != null && (rangeBonus != 0 || !countsTowardLimit))
        {
            pickups.ConsumeNextBombBuff();
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

    bool TryPlaceBomb(int range, bool countsTowardLimit)
    {
        Vector2Int cell = GridMap.Instance.WorldToGrid(transform.position);

        if (BombRegistry.HasBomb(cell))
        {
            return false;
        }

        Bomb bomb = Instantiate(bombPrefab);
        bomb.Init(cell, stats.fuseTime, Mathf.Max(1, range));

        if (countsTowardLimit)
        {
            bomb.Exploded += OnBombExploded;
            activeBombs++;
        }

        return true;
    }

    void OnBombExploded(Bomb bomb)
    {
        activeBombs--;
    }
}
