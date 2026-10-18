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
        public float fuse;
        public PickupEffect effect;
    }

    // Slot Buff: jeden trzymany przedmiot i liczba pozostałych użyć.
    public PickupDefinition HeldItem { get; private set; }
    public int HeldUses { get; private set; }

    public bool HasSpecial => HeldUses > 0 && HeldItem != null && HeldItem.special != SpecialItem.None;
    public bool HasNextBombBuff => HeldUses > 0 && HeldItem != null && HeldItem.special == SpecialItem.None;

    // ---------- Efekty specjalne ----------

    // Ile wybuchów jeszcze wytrzyma tarcza.
    public int ShieldCharges { get; private set; }
    // Czy postać odkopuje bomby, idąc w ich stronę.
    public bool CanKick { get; private set; }
    // Klątwy: odwrócone sterowanie i samoczynne stawianie bomb.
    public bool ReverseControls => reverseCount > 0;
    public bool AutoBombs => autoBombCount > 0;
    public bool IsInvincible => Time.time < invincibleUntil;

    float invincibleUntil;
    // krótka nietykalność po zniszczeniu tarczy, żeby ten sam płomień nie zabił postaci zaraz potem
    float protectedUntil;
    int reverseCount;
    int autoBombCount;

    SpriteRenderer shieldRing;
    SpriteRenderer bodyRenderer;
    Color bodyBaseColor = Color.white;
    bool wasInvincible;

    PlayerStats stats;
    PlayerMovement movement;
    BombController bombs;
    readonly List<TimedBuff> timedBuffs = new List<TimedBuff>();

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        movement = GetComponent<PlayerMovement>();
        bombs = GetComponent<BombController>();

        PlayerHealth health = GetComponent<PlayerHealth>();
        bodyRenderer = health != null ? health.body : null;
    }

    void Update()
    {
        UpdateEffectVisuals();
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
            buff.fuse = stats.AddFuse(definition.fuseChange);
            buff.effect = definition.effect;
            StartTimedEffect(definition.effect, definition.durationSeconds);
            timedBuffs.Add(buff);
        }
        else
        {
            stats.AddBomb(definition.extraBombs);
            stats.AddRange(definition.extraRange);
            stats.AddSpeed(definition.speedSteps);
            stats.AddFuse(definition.fuseChange);
            ApplyPermanentEffect(definition.effect);
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
            stats.AddFuse(-buff.fuse);
            EndTimedEffect(buff.effect);
            timedBuffs.RemoveAt(i);
        }
    }

    // ---------- Efekty specjalne: start, koniec, ochrona ----------

    void StartTimedEffect(PickupEffect effect, float seconds)
    {
        switch (effect)
        {
            case PickupEffect.Invincibility:
                invincibleUntil = Mathf.Max(invincibleUntil, Time.time + seconds);
                break;
            case PickupEffect.ReverseControls:
                reverseCount++;
                break;
            case PickupEffect.AutoBombs:
                autoBombCount++;
                break;
            case PickupEffect.Shield:
                ShieldCharges = Mathf.Min(2, ShieldCharges + 1);
                break;
            case PickupEffect.Kick:
                CanKick = true;
                break;
        }
    }

    void EndTimedEffect(PickupEffect effect)
    {
        switch (effect)
        {
            case PickupEffect.ReverseControls:
                reverseCount = Mathf.Max(0, reverseCount - 1);
                break;
            case PickupEffect.AutoBombs:
                autoBombCount = Mathf.Max(0, autoBombCount - 1);
                break;
        }
    }

    void ApplyPermanentEffect(PickupEffect effect)
    {
        switch (effect)
        {
            case PickupEffect.Shield:
                ShieldCharges = Mathf.Min(2, ShieldCharges + 1);
                break;
            case PickupEffect.Kick:
                CanKick = true;
                break;
        }
    }

    // Czy postać przetrwa trafienie wybuchem (nietykalność, krótka ochrona po tarczy albo tarcza, która się wtedy zużywa).
    // PlayerHealth wywołuje to co klatkę, gdy postać stoi w ogniu.
    public bool AbsorbHit()
    {
        if (IsInvincible || Time.time < protectedUntil)
        {
            return true;
        }

        if (ShieldCharges > 0)
        {
            ShieldCharges--;
            protectedUntil = Time.time + 1.2f;
            return true;
        }

        return false;
    }

    // Bańka tarczy wokół postaci i tęczowe barwy w czasie nietykalności.
    void UpdateEffectVisuals()
    {
        if (bodyRenderer == null)
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            bodyRenderer = health != null ? health.body : null;
        }

        if (bodyRenderer == null)
        {
            return;
        }

        bool protectedNow = ShieldCharges > 0 || Time.time < protectedUntil;

        if (protectedNow && shieldRing == null)
        {
            GameObject ring = new GameObject("ShieldRing");
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            ring.transform.localScale = Vector3.one * 1.7f;

            shieldRing = ring.AddComponent<SpriteRenderer>();
            shieldRing.sprite = PickupArt.Bubble();
            shieldRing.sortingLayerID = bodyRenderer.sortingLayerID;
            shieldRing.sortingOrder = bodyRenderer.sortingOrder + 1;
        }

        if (shieldRing != null)
        {
            // po śmierci bańka znika, przy braku tarczy też, a w czasie ochrony po zużyciu tarczy miga
            bool dead = movement == null || !movement.enabled;
            bool visible = protectedNow && !dead && (ShieldCharges > 0 || Mathf.Repeat(Time.time * 10f, 1f) > 0.5f);
            shieldRing.enabled = visible;
            shieldRing.color = new Color(0.55f, 0.85f, 1f, 0.55f);
        }

        // nietykalność: kolory przechodzą przez tęczę, a po jej końcu wracają do zwykłego odcienia
        if (IsInvincible)
        {
            if (!wasInvincible)
            {
                bodyBaseColor = bodyRenderer.color;
                wasInvincible = true;
            }

            bodyRenderer.color = Color.HSVToRGB(Mathf.Repeat(Time.time * 2f, 1f), 0.45f, 1f);
        }
        else if (wasInvincible)
        {
            bodyRenderer.color = bodyBaseColor;
            wasInvincible = false;
        }
    }

    // Zdejmuje wszystkie efekty: czasowe (cofając ich premie), tarczę, kopanie, klątwy i przedmiot z ręki (menu debug).
    public void ClearEffects()
    {
        foreach (TimedBuff buff in timedBuffs)
        {
            stats.AddBomb(-buff.bombs);
            stats.AddRange(-buff.range);
            stats.AddSpeed(-buff.speed);
            stats.AddFuse(-buff.fuse);
        }

        timedBuffs.Clear();
        ShieldCharges = 0;
        CanKick = false;
        invincibleUntil = 0f;
        protectedUntil = 0f;
        reverseCount = 0;
        autoBombCount = 0;
        HeldItem = null;
        HeldUses = 0;
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

        bool used;

        switch (HeldItem.special)
        {
            case SpecialItem.Hammer:
                used = UseHammer(cell, direction);
                break;
            case SpecialItem.Teleport:
                used = UseTeleport();
                break;
            default:
                used = UseRocket(cell, direction);
                break;
        }

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

    // Zamienia się miejscami z losowym żywym przeciwnikiem. Oboje lądują na środku swojego nowego pola.
    bool UseTeleport()
    {
        MatchManager match = MatchManager.Instance;

        if (match == null)
        {
            return false;
        }

        List<Combatant> others = new List<Combatant>();

        foreach (Combatant other in match.Slots)
        {
            if (other != null && other.IsAlive && other.health != null && other.health.transform != transform)
            {
                others.Add(other);
            }
        }

        if (others.Count == 0)
        {
            return false;
        }

        Transform target = others[Random.Range(0, others.Count)].health.transform;
        GridMap map = GridMap.Instance;

        Vector3 myCenter = map.GridToWorld(map.WorldToGrid(transform.position));
        Vector3 theirCenter = map.GridToWorld(map.WorldToGrid(target.position));

        transform.position = new Vector3(theirCenter.x, theirCenter.y, transform.position.z);
        target.position = new Vector3(myCenter.x, myCenter.y, target.position.z);

        PickupFx.Spawn(HeldItem.Icon, transform.position + Vector3.up * 0.6f, 0.6f, 1.6f, Color.white, 0.5f, 5);
        PickupFx.Spawn(HeldItem.Icon, target.position + Vector3.up * 0.6f, 0.6f, 1.6f, Color.white, 0.5f, 5);
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
