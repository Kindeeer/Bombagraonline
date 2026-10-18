using UnityEngine;

// Jeden przedmiot do zebrania z bąbelka. Nowy przedmiot: Create > Bomb Game > Pickup,
// ustaw efekty i dodaj go do listy w PickupTable (Assets/Resources).
[CreateAssetMenu(fileName = "Pickup", menuName = "Bomb Game/Pickup", order = 0)]
public class PickupDefinition : ScriptableObject
{
    [Header("Info")]
    public string displayName = "New Pickup";
    [TextArea(2, 4)] public string description;

    [Header("Look")]
    [Tooltip("Sprite shown in the bubble and in the Buff slot. Empty = Placeholder Icon.")]
    public Sprite icon;
    [Tooltip("Used when Icon is empty.")]
    public PickupIcon placeholderIcon = PickupIcon.Bomb;
    [Tooltip("Bubble color (white = no change).")]
    public Color bubbleTint = Color.white;

    [Header("Stat Effect (negative values = bad pickup)")]
    [Tooltip("Permanent: forever.\nNextBomb: goes to the Buff slot and applies to the next bomb (only Extra Bombs and Extra Range).\nTimed: active right away for Duration Seconds.")]
    public PickupMode mode = PickupMode.NextBomb;
    [Tooltip("Permanent/Timed: more bombs at once. NextBomb: the next bomb can be placed over the limit.")]
    public int extraBombs;
    [Tooltip("How many cells longer the blast is.")]
    public int extraRange;
    [Tooltip("Speed steps (step size is set in PlayerStats). Permanent/Timed only.")]
    public int speedSteps;
    [Tooltip("Zmiana długości lontu bomb w sekundach (ujemna = bomby wybuchają szybciej). Permanent/Timed only.")]
    public float fuseChange;

    [Header("Efekt specjalny (tarcza, kopanie bomb, klątwy)")]
    [Tooltip("Permanent: tarcza i kopanie bomb. Timed: gwiazda, odwrócone sterowanie, automatyczne bomby.")]
    public PickupEffect effect = PickupEffect.None;
    [Tooltip("Timed only.")]
    [Min(0.1f)] public float durationSeconds = 8f;

    [Header("Special (Space uses it instead of placing a bomb)")]
    public SpecialItem special = SpecialItem.None;
    [Tooltip("Rocket only. 0 = hits just the obstacle and the cell before it (safe). Above 0 = bomb-like blast that can also hit you.")]
    [Min(0)] public int rocketBlastRange;

    [Header("Uses (NextBomb and Special)")]
    [Min(1)] public int uses = 1;

    [Header("Spawn Weight (higher = more often, 0 = never)")]
    [Min(0)] public int weightFromBlocks = 10;
    [Min(0)] public int weightFromFountain = 10;

    // Najpierw grafika z obiektu GameArt (dla wbudowanych ikon), potem własna ikona tej definicji, na końcu zastępcza.
    public Sprite Icon
    {
        get
        {
            GameArt art = GameArt.Instance;
            Sprite fromArt = art != null ? art.IconFor(placeholderIcon) : null;

            if (fromArt != null)
            {
                return fromArt;
            }

            return icon != null ? icon : PickupArt.Icon(placeholderIcon);
        }
    }

    // Trafia do slotu Buff (zamiast działać od razu).
    public bool IsHeld => special != SpecialItem.None || mode == PickupMode.NextBomb;
}

// Przedmiot specjalny trzymany w slocie Buff i używany Spacją zamiast bomby.
public enum SpecialItem
{
    None,
    Hammer,
    Rocket,
    Teleport
}

// Efekty poza zwykłymi statystykami.
public enum PickupEffect
{
    None,
    // chroni przed jednym wybuchem
    Shield,
    // przez jakiś czas nic nie robi obrażeń
    Invincibility,
    // postać odkopuje bomby, idąc w ich stronę
    Kick,
    // klątwa: sterowanie jest odwrócone
    ReverseControls,
    // klątwa: postać sama zostawia bomby
    AutoBombs
}

// Jak długo działa efekt przedmiotu (dla przedmiotów bez Special).
public enum PickupMode
{
    // Na stałe, od razu po podniesieniu.
    Permanent,
    // Trafia do slotu Buff i działa na następną postawioną bombę.
    NextBomb,
    // Działa od razu przez określony czas.
    Timed
}

// Ikona zastępcza rysowana z kodu, gdy przedmiot nie ma własnego obrazka.
public enum PickupIcon
{
    None,
    Bomb,
    Range,
    Speed,
    Hammer,
    Rocket,
    Skull,
    Shield,
    Star,
    Boot,
    Bolt,
    Clock,
    Swirl,
    Dizzy,
    MegaBomb,
    BombStorm
}
