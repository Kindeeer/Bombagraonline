using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Jeden uczestnik rundy: człowiek albo bot w danym slocie (0 = gracz, 1-3 = boty).
public class Combatant
{
    public PlayerHealth health;
    public PlayerPickups pickups;
    public int slot;
    public bool isBot;

    public bool IsAlive => health != null && !health.IsDead;
}

// Zarządza rundą: dokłada boty, liczy żywych, przyznaje punkt zwycięzcy i restartuje scenę.
// Punkty trzymane są w polu statycznym, więc przeżywają restart sceny (znikają po wyjściu z gry).
public class MatchManager : MonoBehaviour
{
    public static MatchManager Instance { get; private set; }

    public const int MaxSlots = 4;

    [Header("Boty")]
    [Range(0, 3)] public int botCount = 3;
    [Tooltip("Mnożnik prędkości botów względem gracza (1 = tak samo szybkie).")]
    [Min(0.1f)] public float botSpeedMultiplier = 1f;
    [Tooltip("Jak często bot ryzykuje: ignoruje własne bomby i rusza prosto na przeciwnika (0 = ostrożny, 1 = kamikadze).")]
    [Range(0f, 1f)] public float botRecklessness = 0.25f;

    [Header("Runda")]
    [Tooltip("Tyle sekund widać wynik rundy, zanim zacznie się następna.")]
    public float restartDelay = 3f;

    [Header("Kolory slotów (karta w HUD i odcień botów)")]
    public Color[] slotColors =
    {
        new Color(0.30f, 0.78f, 0.35f),
        new Color(1f, 0.86f, 0.25f),
        new Color(1f, 0.38f, 0.38f),
        new Color(0.38f, 0.60f, 1f)
    };
    [Tooltip("Jak mocno odcień slotu zabarwia sprite bota (tymczasowo, do czasu własnych sprite'ów botów).")]
    [Range(0f, 1f)] public float botTintStrength = 0.6f;

    static readonly int[] scores = new int[MaxSlots];

    public Combatant[] Slots { get; } = new Combatant[MaxSlots];
    public bool RoundOver { get; private set; }

    string banner = "";
    GUIStyle bannerStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Array.Clear(scores, 0, scores.Length);
    }

    public static int GetScore(int slot)
    {
        return slot >= 0 && slot < MaxSlots ? scores[slot] : 0;
    }

    public static string NameOf(Combatant combatant)
    {
        return combatant.isBot ? "BOT " + combatant.slot : "YOU";
    }

    void OnEnable()
    {
        Instance = this;
    }

    void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        PlayerHealth human = FindAnyObjectByType<PlayerHealth>();

        if (human == null)
        {
            Debug.LogError("MatchManager: w scenie nie ma gracza (PlayerHealth).", this);
            enabled = false;
            return;
        }

        Register(human, 0, false);

        for (int slot = 1; slot <= botCount && slot < MaxSlots; slot++)
        {
            SpawnBot(human, slot);
        }
    }

    void Register(PlayerHealth health, int slot, bool isBot)
    {
        Combatant combatant = new Combatant();
        combatant.health = health;
        combatant.pickups = health.GetComponent<PlayerPickups>();
        combatant.slot = slot;
        combatant.isBot = isBot;
        Slots[slot] = combatant;
    }

    // Bot to kopia gracza (te same komponenty i statystyki), który zamiast klawiatury słucha BotBrain.
    void SpawnBot(PlayerHealth human, int slot)
    {
        GridMap map = GridMap.Instance;
        GameObject copy = Instantiate(human.gameObject, human.transform.parent);
        copy.name = "Bot " + slot;

        PlayerMovement movement = copy.GetComponent<PlayerMovement>();
        movement.useKeyboard = false;
        movement.startCell = map.spawnPositions[slot % map.spawnPositions.Length];
        movement.baseSpeed *= botSpeedMultiplier;

        Color tint = Color.Lerp(Color.white, slotColors[slot], botTintStrength);

        foreach (SpriteRenderer sprite in copy.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sprite.name != "Shadow")
            {
                sprite.color = tint;
            }
        }

        BotBrain brain = copy.AddComponent<BotBrain>();
        brain.recklessness = botRecklessness;

        Register(copy.GetComponent<PlayerHealth>(), slot, true);
    }

    void Update()
    {
        if (RoundOver)
        {
            return;
        }

        int total = 0;
        int alive = 0;
        Combatant last = null;

        foreach (Combatant combatant in Slots)
        {
            if (combatant == null)
            {
                continue;
            }

            total++;

            if (combatant.IsAlive)
            {
                alive++;
                last = combatant;
            }
        }

        // bez botów gra jak dawniej: śmierć gracza restartuje scenę, bez punktów
        if (total <= 1)
        {
            if (alive == 0)
            {
                EndRound(null, false);
            }

            return;
        }

        if (alive <= 1)
        {
            EndRound(last, true);
        }
    }

    void EndRound(Combatant winner, bool scorePoint)
    {
        RoundOver = true;

        if (winner != null && scorePoint)
        {
            scores[winner.slot]++;
        }

        banner = winner == null ? (scorePoint ? "DRAW" : "") : NameOf(winner) + " WINS THE ROUND";
        Invoke(nameof(Restart), restartDelay);
    }

    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Tymczasowy napis wyniku rundy (do czasu zrobienia go w Canvasie).
    void OnGUI()
    {
        if (!RoundOver || banner.Length == 0)
        {
            return;
        }

        if (bannerStyle == null)
        {
            bannerStyle = new GUIStyle(GUI.skin.label);
            bannerStyle.alignment = TextAnchor.MiddleCenter;
            bannerStyle.fontStyle = FontStyle.Bold;
        }

        bannerStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.08f);
        Rect area = new Rect(0f, Screen.height * 0.35f, Screen.width, Screen.height * 0.2f);

        bannerStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(area.x + 3f, area.y + 3f, area.width, area.height), banner, bannerStyle);
        bannerStyle.normal.textColor = Color.white;
        GUI.Label(area, banner, bannerStyle);
    }
}