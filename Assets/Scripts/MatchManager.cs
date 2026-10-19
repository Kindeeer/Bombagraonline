using System;
using System.Collections;
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

    [Header("Menu główne")]
    [Tooltip("Po wciśnięciu Play w edytorze najpierw pokazuje się menu główne (Play, Skins, Settings). Odznacz, żeby od razu grać.")]
    public bool showMainMenu = true;

    [Header("Chaos (długa runda)")]
    [Tooltip("Gdy runda trwa długo, włącza się CHAOS: DOUBLE BUFFS. Slot Buff mieści wtedy dwa przedmioty zamiast jednego.")]
    public bool chaosEnabled = true;
    [Tooltip("Po ilu sekundach gry (bez READY i pauzy) zaczyna się CHAOS.")]
    public float chaosAfterSeconds = 75f;
    public Color chaosColor = new Color(1f, 0.4f, 0.9f);

    [Header("Start rundy")]
    [Tooltip("Na starcie widać całą arenę z napisem READY, a po GO! kamera przybliża się i gra rusza.")]
    public bool playIntro = true;
    public float readySeconds = 1.3f;
    public float goSeconds = 0.8f;
    public Color readyColor = new Color(1f, 0.93f, 0.4f);
    public Color goColor = new Color(0.45f, 1f, 0.5f);

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

    // true od startu rundy do napisu GO!: gracz i boty stoją w miejscu i nie stawiają bomb
    // Sterowanie jest też zablokowane, gdy otwarta jest pauza (zębatka).
    public static bool InputLocked
    {
        get => introLocked || MainMenu.PauseOpen;
        private set => introLocked = value;
    }

    static bool introLocked;

    // true od momentu, gdy w rundzie włączył się CHAOS (DOUBLE BUFFS): slot Buff mieści dwa przedmioty.
    public static bool ChaosActive { get; private set; }

    float roundClock;
    int nukeOwner = -1;
    float nukeWindowUntil;
    float nukeGraceUntil;

    // Atomówka właśnie wybuchła. Przez chwilę nie rozstrzygamy rundy (postacie giną po kolei), a jeśli zginą wszyscy,
    // wygrywa ten, kto ją zrzucił.
    public void NoteNuke(int ownerSlot)
    {
        nukeOwner = ownerSlot;
        nukeWindowUntil = Time.time + 4f;
        nukeGraceUntil = Time.time + 1.2f;
    }

    string banner = "";
    GUIStyle bannerStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Array.Clear(scores, 0, scores.Length);
        InputLocked = false;
        ChaosActive = false;
    }

    public static int GetScore(int slot)
    {
        return slot >= 0 && slot < MaxSlots ? scores[slot] : 0;
    }

    public static string NameOf(Combatant combatant)
    {
        return combatant.isBot ? "BOT " + combatant.slot : "YOU";
    }

    // Grafiki gracza ze slotu z obiektu GameArt (null, gdy w scenie nie ma GameArt).
    static PlayerSprites SpritesFor(int slot)
    {
        GameArt art = GameArt.Instance;
        return art != null ? art.PlayerFor(slot) : null;
    }

    // Wpisuje w postać grafiki z GameArt: cztery kierunki i cień. Puste pola zostawiają dotychczasowy wygląd.
    void ApplyArt(GameObject character, int slot)
    {
        GameArt art = GameArt.Instance;

        if (art == null)
        {
            return;
        }

        PlayerVisual visual = character.GetComponentInChildren<PlayerVisual>();
        PlayerSprites sprites = art.PlayerFor(slot);

        if (visual != null && sprites != null)
        {
            visual.SetSprites(sprites);
        }

        if (art.playerShadow != null)
        {
            foreach (SpriteRenderer renderer in character.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.name == "Shadow")
                {
                    renderer.sprite = art.playerShadow;
                }
            }
        }
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
            InputLocked = false;
            ChaosActive = false;
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

        ChaosActive = false;
        roundClock = 0f;
        Register(human, 0, false);

        // menu główne: boty pojawiają się dopiero po wyborze trybu (liczbę wybiera gracz), a skiny po wyborze skina
        bool menu = showMainMenu && MainMenu.ShouldShow;

        if (menu)
        {
            MainMenu.Open();
        }
        else
        {
            SpawnBots(human, MainMenu.GameStarted ? MainMenu.BotCount : botCount);
            ApplyLooks();
        }

        if (menu || playIntro)
        {
            StartCoroutine(IntroRoutine(human, menu));
        }
    }

    void SpawnBots(PlayerHealth human, int count)
    {
        for (int slot = 1; slot <= count && slot < MaxSlots; slot++)
        {
            SpawnBot(human, slot);
        }
    }

    // Wpisuje w każdą postać jej skin (GameArt) i odcień slotu bota. Wołane przy starcie i po wyborze w menu.
    void ApplyLooks()
    {
        foreach (Combatant combatant in Slots)
        {
            if (combatant == null || combatant.health == null)
            {
                continue;
            }

            GameObject character = combatant.health.gameObject;
            ApplyArt(character, combatant.slot);

            if (!combatant.isBot)
            {
                continue;
            }

            // bot z własnymi grafikami (GameArt) wygląda jak jego zwierzak, bez nich dostaje odcień slotu
            PlayerSprites botSprites = SpritesFor(combatant.slot);

            if (botSprites == null || !botSprites.HasSprites)
            {
                Color tint = Color.Lerp(Color.white, slotColors[combatant.slot], botTintStrength);

                foreach (SpriteRenderer sprite in character.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sprite.name != "Shadow")
                    {
                        sprite.color = tint;
                    }
                }
            }
        }
    }

    // Najpierw (jeśli jest menu główne) czeka na Play, potem READY na widoku całej areny, potem GO!:
    // kamera się przybliża, a gracze i boty mogą ruszyć.
    IEnumerator IntroRoutine(PlayerHealth human, bool waitForMenu)
    {
        InputLocked = true;

        ArenaCamera arenaCamera = FindAnyObjectByType<ArenaCamera>();

        if (arenaCamera != null)
        {
            arenaCamera.SetMode(ArenaCamera.ViewMode.Whole, true);
        }

        if (waitForMenu)
        {
            while (MainMenu.IsOpen)
            {
                yield return null;
            }

            SpawnBots(human, MainMenu.BotCount);
            ApplyLooks();
        }

        if (!playIntro)
        {
            InputLocked = false;

            if (arenaCamera != null)
            {
                arenaCamera.SetMode(ArenaCamera.ViewMode.Medium);
            }

            yield break;
        }

        RoundIntro intro = RoundIntro.Create();

        // jedna klatka na ustawienie kamery i postaci
        yield return null;
        GameAudio.Play(AudioEvent.RoundReady);
        yield return intro.Show("READY", readyColor, readySeconds);

        InputLocked = false;

        if (arenaCamera != null)
        {
            arenaCamera.SetMode(ArenaCamera.ViewMode.Medium);
        }

        GameAudio.Play(AudioEvent.RoundGo);
        yield return intro.Show("GO!", goColor, goSeconds);
        Destroy(intro.gameObject);
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

        // prędkość bota liczy PlayerStats (buty + mnożnik), żeby buty nie kasowały mnożnika
        PlayerStats botStats = copy.GetComponent<PlayerStats>();
        botStats.speedMultiplier = botSpeedMultiplier;
        botStats.RefreshSpeed();

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

        // zegar rundy liczy tylko czas gry (bez menu, READY i pauzy); po jego upływie włącza się CHAOS
        if (!InputLocked)
        {
            roundClock += Time.deltaTime;

            if (chaosEnabled && !ChaosActive && roundClock >= chaosAfterSeconds)
            {
                StartChaos();
            }
        }

        // tuż po wybuchu atomówki czekamy, aż wszystkie ofiary zginą
        if (Time.time < nukeGraceUntil)
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
            Combatant winner = last;

            // atomówka: kto ją zrzucił i zabił wszystkich, wygrywa nawet jeśli sam zginął
            if (alive == 0 && nukeOwner >= 0 && nukeOwner < Slots.Length && Slots[nukeOwner] != null && Time.time < nukeWindowUntil)
            {
                winner = Slots[nukeOwner];
            }

            EndRound(winner, true);
        }
    }

    void StartChaos()
    {
        ChaosActive = true;
        GameAudio.Play(AudioEvent.ChaosStart);
        StartCoroutine(ChaosBanner());
    }

    IEnumerator ChaosBanner()
    {
        RoundIntro banner = RoundIntro.Create();
        yield return banner.Show("DOUBLE BUFFS,\nCHAOS!", chaosColor, 2.6f, 150f);
        Destroy(banner.gameObject);
    }

    void EndRound(Combatant winner, bool scorePoint)
    {
        RoundOver = true;

        if (winner != null && scorePoint)
        {
            scores[winner.slot]++;
        }

        GameAudio.Play(winner == null ? AudioEvent.RoundDraw : AudioEvent.RoundWin);
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