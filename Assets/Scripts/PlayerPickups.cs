using System.Collections.Generic;
using UnityEngine;

// Zbiera bąbelki z pola, na którym stoi gracz, i stosuje efekty z PickupDefinition.
//
// ZASADA SLOTU BUFF
// - Stałe ulepszenia, które się kumulują (Tarcza, Buty), nie zajmują slotu: pokazują się w małych slotach pod Buffem.
// - Wszystko inne (przedmioty do użycia Spacją, przedmioty na następną bombę, kopanie bomb, efekty czasowe i klątwy)
//   zajmuje slot Buff. Slot ma jedno miejsce: nowy przedmiot zastępuje poprzedni, więc nie da się mieć np. dużego
//   wybuchu i kopania naraz. Gdy w rundzie włączy się CHAOS (DOUBLE BUFFS), slot ma dwa miejsca.
[RequireComponent(typeof(PlayerStats))]
public class PlayerPickups : MonoBehaviour
{
    class TimedBuff
    {
        public float endTime;
        public int bombs;
        public int range;
        public int speed;
        public float fuse;
        public PickupEffect effect;
    }

    // Jeden przedmiot w slocie Buff: do użycia (uses > 0) albo efekt czasowy (timed != null).
    class Occupant
    {
        public PickupDefinition definition;
        public int uses;
        public TimedBuff timed;
        public bool IsTimed => timed != null;
    }

    readonly List<Occupant> occupants = new List<Occupant>();

    // Ile przedmiotów mieści slot Buff: jeden, a w trybie CHAOS dwa.
    public int SlotCapacity => MatchManager.ChaosActive ? 2 : 1;

    // ---------- Widok slotu dla HUD i bota ----------

    public int OccupantCount => occupants.Count;

    // Przedmiot w slocie o danym numerze (0 = najstarszy). count > 0 to liczba użyć, count < 0 to sekundy do końca efektu.
    public bool TryGetOccupant(int index, out PickupDefinition definition, out int count)
    {
        if (index < 0 || index >= occupants.Count)
        {
            definition = null;
            count = 0;
            return false;
        }

        Occupant occupant = occupants[index];
        definition = occupant.definition;
        count = occupant.IsTimed ? -Mathf.CeilToInt(Mathf.Max(0f, occupant.timed.endTime - Time.time)) : occupant.uses;
        return true;
    }

    // Przedmiot, który Spacja zużyje albo który pokazuje przycisk akcji: najpierw specjalny (młotek, rakieta...),
    // potem przedmiot na następną bombę.
    public PickupDefinition HeldItem
    {
        get
        {
            Occupant occupant = SpecialOccupant() ?? NextBombOccupant();
            return occupant != null ? occupant.definition : null;
        }
    }

    public int HeldUses
    {
        get
        {
            Occupant occupant = SpecialOccupant() ?? NextBombOccupant();
            return occupant != null ? occupant.uses : 0;
        }
    }

    public bool HasSpecial => SpecialOccupant() != null;
    public bool HasNextBombBuff => NextBombOccupant() != null;

    Occupant SpecialOccupant()
    {
        return occupants.Find(o => !o.IsTimed && o.uses > 0 && o.definition.special != SpecialItem.None);
    }

    Occupant NextBombOccupant()
    {
        return occupants.Find(o => !o.IsTimed && o.uses > 0 && o.definition.special == SpecialItem.None && o.definition.mode == PickupMode.NextBomb);
    }

    // ---------- Efekty specjalne ----------

    // Ile wybuchów jeszcze wytrzyma tarcza (kumuluje się, nie zajmuje slotu Buff).
    public int ShieldCharges { get; private set; }

    // Kopanie bomb: przedmiot w slocie Buff (kilka kopnięć albo kopanie na czas).
    public bool CanKick => occupants.Exists(o => o.definition.effect == PickupEffect.Kick && (o.IsTimed || o.uses > 0));

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

        if (definition.IsHeld || definition.mode == PickupMode.Timed)
        {
            AddToSlot(definition);
        }
        else
        {
            // stałe ulepszenie: Tarcza, Buty i podobne
            stats.AddBomb(definition.extraBombs);
            stats.AddRange(definition.extraRange);
            stats.AddSpeed(definition.speedSteps);
            stats.AddFuse(definition.fuseChange);

            if (definition.effect == PickupEffect.Shield)
            {
                ShieldCharges = Mathf.Min(2, ShieldCharges + 1);
            }
        }

        GameAudio.Play(AudioEvent.PickupCollect, transform.position);

        // atomówka: po podniesieniu człowiek od razu staje i wybiera, gdzie ją zrzucić (bot robi to sam w BotBrain)
        if (definition.special == SpecialItem.Nuke && movement != null && movement.useKeyboard)
        {
            NukeTargeting.Begin(this, movement);
        }

        Sprite icon = definition.Icon;

        if (icon != null)
        {
            PickupFx.Spawn(icon, transform.position + Vector3.up * 0.8f, 0.6f, 1.1f,
                Color.white, 0.6f, 5, null, Vector3.up * 0.8f);
        }
    }

    // ---------- Slot Buff ----------

    // Ten sam przedmiot do użycia dokłada użycia. Inny zajmuje wolne miejsce, a gdy go nie ma, wypiera najstarszy.
    void AddToSlot(PickupDefinition definition)
    {
        if (definition.IsHeld)
        {
            Occupant same = occupants.Find(o => !o.IsTimed && o.definition == definition && o.uses > 0);

            if (same != null)
            {
                same.uses += definition.uses;
                return;
            }
        }
        else
        {
            // ten sam efekt czasowy zbierany ponownie zaczyna się od nowa
            for (int i = occupants.Count - 1; i >= 0; i--)
            {
                if (occupants[i].IsTimed && occupants[i].definition == definition)
                {
                    RemoveOccupant(occupants[i]);
                }
            }
        }

        while (occupants.Count >= SlotCapacity && occupants.Count > 0)
        {
            RemoveOccupant(occupants[0]);
        }

        Occupant occupant = new Occupant { definition = definition };

        if (definition.IsHeld)
        {
            occupant.uses = definition.uses;
        }
        else
        {
            TimedBuff buff = new TimedBuff();
            buff.endTime = Time.time + definition.durationSeconds;
            buff.bombs = stats.AddBomb(definition.extraBombs);
            buff.range = stats.AddRange(definition.extraRange);
            buff.speed = stats.AddSpeed(definition.speedSteps);
            buff.fuse = stats.AddFuse(definition.fuseChange);
            buff.effect = definition.effect;
            StartTimedEffect(definition.effect, definition.durationSeconds);
            occupant.timed = buff;
        }

        occupants.Add(occupant);
    }

    // Zdejmuje przedmiot ze slotu. Efekt czasowy cofa swoje premie.
    void RemoveOccupant(Occupant occupant)
    {
        if (occupant.IsTimed)
        {
            TimedBuff buff = occupant.timed;
            stats.AddBomb(-buff.bombs);
            stats.AddRange(-buff.range);
            stats.AddSpeed(-buff.speed);
            stats.AddFuse(-buff.fuse);
            EndTimedEffect(buff.effect);
        }

        occupants.Remove(occupant);
    }

    void ConsumeUse(Occupant occupant)
    {
        occupant.uses--;

        if (occupant.uses <= 0)
        {
            occupants.Remove(occupant);
        }
    }

    void ExpireTimedBuffs()
    {
        for (int i = occupants.Count - 1; i >= 0; i--)
        {
            Occupant occupant = occupants[i];

            if (occupant.IsTimed && Time.time >= occupant.timed.endTime)
            {
                RemoveOccupant(occupant);
            }
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
        }
    }

    void EndTimedEffect(PickupEffect effect)
    {
        switch (effect)
        {
            case PickupEffect.Invincibility:
                invincibleUntil = 0f;
                break;
            case PickupEffect.ReverseControls:
                reverseCount = Mathf.Max(0, reverseCount - 1);
                break;
            case PickupEffect.AutoBombs:
                autoBombCount = Mathf.Max(0, autoBombCount - 1);
                break;
        }
    }

    // Zużywa jedno kopnięcie (wołane po udanym kopnięciu bomby). Kopanie na czas nic nie zużywa.
    public void ConsumeKick()
    {
        Occupant kick = occupants.Find(o => !o.IsTimed && o.uses > 0 && o.definition.effect == PickupEffect.Kick);

        if (kick != null)
        {
            ConsumeUse(kick);
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
            GameAudio.Play(AudioEvent.ShieldBreak, transform.position);
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

    // Zdejmuje wszystkie efekty: slot Buff (cofając premie efektów czasowych), tarczę i klątwy (menu debug).
    public void ClearEffects()
    {
        for (int i = occupants.Count - 1; i >= 0; i--)
        {
            RemoveOccupant(occupants[i]);
        }

        ShieldCharges = 0;
        invincibleUntil = 0f;
        protectedUntil = 0f;
        reverseCount = 0;
        autoBombCount = 0;
    }

    // ---------- Wywoływane przez BombController (Spacja) ----------

    // Premia z buffa NextBomb dla następnej bomby.
    public void GetNextBombBonus(out int rangeBonus, out bool overLimit)
    {
        Occupant occupant = NextBombOccupant();
        rangeBonus = occupant != null ? occupant.definition.extraRange : 0;
        overLimit = occupant != null && occupant.definition.extraBombs > 0;
    }

    public void ConsumeNextBombBuff()
    {
        Occupant occupant = NextBombOccupant();

        if (occupant != null)
        {
            ConsumeUse(occupant);
        }
    }

    public bool UseSpecial()
    {
        Occupant occupant = SpecialOccupant();

        if (occupant == null || movement == null || GridMap.Instance == null)
        {
            return false;
        }

        Vector2Int cell = GridMap.Instance.WorldToGrid(transform.position);
        Vector2Int direction = DirectionOf(movement.Facing);
        PickupDefinition item = occupant.definition;

        bool used;

        switch (item.special)
        {
            case SpecialItem.Hammer:
                used = UseHammer(item, cell, direction);
                break;
            case SpecialItem.Teleport:
                used = UseTeleport(item);
                break;
            case SpecialItem.Nuke:
                // atomówka zużywa się dopiero po zrzuceniu (LaunchNuke), a Spacja tylko włącza celowanie
                NukeTargeting.Begin(this, movement);
                used = false;
                break;
            default:
                used = UseRocket(item, cell, direction);
                break;
        }

        if (used)
        {
            ConsumeUse(occupant);
        }

        return used;
    }

    bool UseHammer(PickupDefinition item, Vector2Int cell, Vector2Int direction)
    {
        Vector2Int target = cell + direction;

        if (!GridMap.Instance.SmashBlock(target))
        {
            return false;
        }

        PickupFx.Spawn(item.Icon, GridMap.Instance.GridToWorld(target) + Vector3.up * 0.5f,
            0.6f, 1.4f, Color.white, 0.35f, 5);
        return true;
    }

    // Zamienia się miejscami z losowym żywym przeciwnikiem. Oboje lądują na środku swojego nowego pola.
    bool UseTeleport(PickupDefinition item)
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

        GameAudio.Play(AudioEvent.Teleport, transform.position);
        transform.position = new Vector3(theirCenter.x, theirCenter.y, transform.position.z);
        target.position = new Vector3(myCenter.x, myCenter.y, target.position.z);

        PickupFx.Spawn(item.Icon, transform.position + Vector3.up * 0.6f, 0.6f, 1.6f, Color.white, 0.5f, 5);
        PickupFx.Spawn(item.Icon, target.position + Vector3.up * 0.6f, 0.6f, 1.6f, Color.white, 0.5f, 5);
        return true;
    }

    bool UseRocket(PickupDefinition item, Vector2Int cell, Vector2Int direction)
    {
        if (bombs == null || bombs.bombPrefab == null)
        {
            return false;
        }

        Rocket.Launch(cell, direction, item.rocketBlastRange, bombs.bombPrefab, item.Icon, transform);
        return true;
    }

    // ---------- Atomówka ----------

    // Zrzuca atomówkę na podane pole (wołane po kliknięciu w mapę albo przez bota) i zużywa przedmiot.
    public bool LaunchNuke(Vector2Int target)
    {
        Occupant occupant = occupants.Find(o => !o.IsTimed && o.uses > 0 && o.definition.special == SpecialItem.Nuke);

        if (occupant == null || bombs == null || bombs.bombPrefab == null)
        {
            return false;
        }

        int slot = -1;
        MatchManager match = MatchManager.Instance;

        if (match != null)
        {
            for (int i = 0; i < match.Slots.Length; i++)
            {
                if (match.Slots[i] != null && match.Slots[i].pickups == this)
                {
                    slot = i;
                }
            }
        }

        Nuke.Drop(target, slot, bombs.bombPrefab, occupant.definition.Icon);
        ConsumeUse(occupant);
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
