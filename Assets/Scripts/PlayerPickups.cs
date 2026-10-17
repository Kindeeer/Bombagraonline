using System.Collections.Generic;
using UnityEngine;

// Zbiera bąbelki z pola, na którym stoi gracz, i stosuje efekty z PickupDefinition.
// Przedmioty NextBomb i Special trafiają do slotu Buff (jeden naraz), a Spację obsługuje BombController.
[RequireComponent(typeof(PlayerStats))]
public class PlayerPickups : MonoBehaviour
{
    class TimedBuff
    {
        public PickupDefinition definition;
        public float endTime;
        public int bombs;
        public int range;
        public int speed;
    }

    // Slot Buff: jeden trzymany przedmiot i liczba pozostałych użyć.
    public PickupDefinition HeldItem { get; private set; }
    public int HeldUses { get; private set; }

    public bool HasSpecial => HeldUses > 0 && HeldItem != null && HeldItem.special != SpecialItem.None;
    public bool HasNextBombBuff => HeldUses > 0 && HeldItem != null && HeldItem.special == SpecialItem.None;

    PlayerStats stats;
    PlayerMovement movement;
    BombController bombs;
    readonly List<TimedBuff> timedBuffs = new List<TimedBuff>();

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        movement = GetComponent<PlayerMovement>();
        bombs = GetComponent<BombController>();
    }

    void Update()
    {
        ExpireTimedBuffs();

        // po śmierci (PlayerHealth wyłącza ruch) nie zbieramy przedmiotów
        if (movement == null || !movement.enabled || GridMap.Instance == null)
        {
            return;
        }

        CollectUnderPlayer();
    }

    void CollectUnderPlayer()
    {
        Vector2Int cell = GridMap.Instance.WorldToGrid(transform.position);

        if (!PickupRegistry.TryGet(cell, out PickupItem item) || !item.CanCollect)
        {
            return;
        }

        Apply(item.Definition);
        item.Collect();
    }

    public void Apply(PickupDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        if (definition.IsHeld)
        {
            Hold(definition);
        }
        else if (definition.mode == PickupMode.Timed)
        {
            TimedBuff buff = new TimedBuff();
            buff.definition = definition;
            buff.endTime = Time.time + definition.durationSeconds;
            buff.bombs = stats.AddBomb(definition.extraBombs);
            buff.range = stats.AddRange(definition.extraRange);
            buff.speed = stats.AddSpeed(definition.speedSteps);
            timedBuffs.Add(buff);
        }
        else
        {
            stats.AddBomb(definition.extraBombs);
            stats.AddRange(definition.extraRange);
            stats.AddSpeed(definition.speedSteps);
        }

        Sprite icon = definition.Icon;

        if (icon != null)
        {
            PickupFx.Spawn(icon, transform.position + Vector3.up * 0.8f, 0.6f, 1.1f,
                Color.white, 0.6f, 5, null, Vector3.up * 0.8f);
        }
    }

    // Ten sam przedmiot dokłada użycia, inny zastępuje to, co było w slocie.
    void Hold(PickupDefinition definition)
    {
        if (HeldItem == definition && HeldUses > 0)
        {
            HeldUses += definition.uses;
        }
        else
        {
            HeldItem = definition;
            HeldUses = definition.uses;
        }
    }

    void ConsumeHeldUse()
    {
        HeldUses--;

        if (HeldUses <= 0)
        {
            HeldItem = null;
            HeldUses = 0;
        }
    }

    void ExpireTimedBuffs()
    {
        for (int i = timedBuffs.Count - 1; i >= 0; i--)
        {
            TimedBuff buff = timedBuffs[i];

            if (Time.time < buff.endTime)
            {
                continue;
            }

            stats.AddBomb(-buff.bombs);
            stats.AddRange(-buff.range);
            stats.AddSpeed(-buff.speed);
            timedBuffs.RemoveAt(i);
        }
    }

    // Najnowszy aktywny efekt czasowy (do wyświetlenia w slocie Buff).
    public bool TryGetTimedBuff(out PickupDefinition definition, out float secondsLeft)
    {
        if (timedBuffs.Count == 0)
        {
            definition = null;
            secondsLeft = 0f;
            return false;
        }

        TimedBuff latest = timedBuffs[timedBuffs.Count - 1];
        definition = latest.definition;
        secondsLeft = Mathf.Max(0f, latest.endTime - Time.time);
        return true;
    }

    // ---------- Wywoływane przez BombController (Spacja) ----------

    // Premia z buffa NextBomb dla następnej bomby.
    public void GetNextBombBonus(out int rangeBonus, out bool overLimit)
    {
        rangeBonus = HasNextBombBuff ? HeldItem.extraRange : 0;
        overLimit = HasNextBombBuff && HeldItem.extraBombs > 0;
    }

    public void ConsumeNextBombBuff()
    {
        if (HasNextBombBuff)
        {
            ConsumeHeldUse();
        }
    }

    public bool UseSpecial()
    {
        if (!HasSpecial || movement == null || GridMap.Instance == null)
        {
            return false;
        }

        Vector2Int cell = GridMap.Instance.WorldToGrid(transform.position);
        Vector2Int direction = DirectionOf(movement.Facing);

        bool used = HeldItem.special == SpecialItem.Hammer
            ? UseHammer(cell, direction)
            : UseRocket(cell, direction);

        if (used)
        {
            ConsumeHeldUse();
        }

        return used;
    }

    bool UseHammer(Vector2Int cell, Vector2Int direction)
    {
        Vector2Int target = cell + direction;

        if (!GridMap.Instance.SmashBlock(target))
        {
            return false;
        }

        PickupFx.Spawn(HeldItem.Icon, GridMap.Instance.GridToWorld(target) + Vector3.up * 0.5f,
            0.6f, 1.4f, Color.white, 0.35f, 5);
        return true;
    }

    bool UseRocket(Vector2Int cell, Vector2Int direction)
    {
        if (bombs == null || bombs.bombPrefab == null)
        {
            return false;
        }

        Rocket.Launch(cell, direction, HeldItem.rocketBlastRange, bombs.bombPrefab, HeldItem.Icon, transform);
        return true;
    }

    static Vector2Int DirectionOf(Facing facing)
    {
        switch (facing)
        {
            case Facing.Up: return Vector2Int.up;
            case Facing.Left: return Vector2Int.left;
            case Facing.Right: return Vector2Int.right;
            default: return Vector2Int.down;
        }
    }
}
