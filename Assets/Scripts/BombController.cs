using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerStats))]
public class BombController : MonoBehaviour
{
    public Bomb bombPrefab;

    // co ile sekund klątwa "biegunka bomb" zostawia bombę
    const float AutoBombInterval = 0.9f;
    float autoBombTimer;

    PlayerStats stats;
    PlayerPickups pickups;
    PlayerMovement movement;
    int activeBombs;

    // Czy gracz ma wolny slot na bombę (używają tego boty).
    public bool HasFreeBomb => activeBombs < stats.maxBombs;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        movement = GetComponent<PlayerMovement>();

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
        // do napisu GO! nie można stawiać bomb
        if (MatchManager.InputLocked)
        {
            return;
        }

        // podczas celowania atomówką przycisk akcji (ATOM na ekranie, Spacja na klawiaturze) zrzuca ją w wybrane miejsce
        if (movement != null && movement.Frozen)
        {
            if (movement.useKeyboard && (PlaceKeyPressed() || TouchInput.ConsumeBomb()))
            {
                NukeTargeting.ConfirmActive();
            }

            return;
        }

        // klątwa "biegunka bomb": postać sama zostawia bombę co chwilę
        if (pickups != null && pickups.AutoBombs)
        {
            autoBombTimer -= Time.deltaTime;

            if (autoBombTimer <= 0f)
            {
                autoBombTimer = AutoBombInterval;
                PlaceBomb();
            }
        }

        // boty nie używają klawiatury, dla nich bombę stawia BotBrain przez PlaceBomb()
        bool keyboard = movement == null || movement.useKeyboard;

        if (keyboard && (PlaceKeyPressed() || TouchInput.ConsumeBomb()))
        {
            OnActionPressed();
        }
    }

    // Spacja albo przycisk na ekranie: przedmiot specjalny ze slotu Buff, a jeśli go nie ma, bomba (z premią z buffa NextBomb).
    void OnActionPressed()
    {
        if (pickups != null && pickups.HasSpecial)
        {
            pickups.UseSpecial();
            return;
        }

        PlaceBomb();
    }

    // Stawia bombę (z premią z buffa NextBomb). Używają jej Spacja i boty. Zwraca true, gdy bomba stanęła.
    public bool PlaceBomb()
    {
        int rangeBonus = 0;
        bool overLimit = false;

        if (pickups != null)
        {
            pickups.GetNextBombBonus(out rangeBonus, out overLimit);
        }

        bool atLimit = activeBombs >= stats.maxBombs;

        if (atLimit && !overLimit)
        {
            return false;
        }

        // bomba ponad limit nie zajmuje miejsca w limicie
        bool countsTowardLimit = !atLimit;

        if (!TryPlaceBomb(stats.blastRange + rangeBonus, countsTowardLimit))
        {
            return false;
        }

        // buff zużywa się tylko wtedy, gdy coś dał: dłuższy wybuch albo bombę ponad limit
        if (pickups != null && (rangeBonus != 0 || !countsTowardLimit))
        {
            pickups.ConsumeNextBombBuff();
        }

        return true;
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
