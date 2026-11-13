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

    // runda online: cudzy gracz (postać ruszana z sieci), jego nick i identyfikator w sesji
    public bool remote;
    public string onlineId;
    public string nick;

    // wyszedł z gry w trakcie rundy online
    public bool left;

    // zabójstwa w tej rundzie (wszystkie) i te, za które są monety (prawdziwi gracze)
    public int kills;
    public int humanKills;

    // drużyna w trybie 2 na 2 (0 = niebiescy, 1 = czerwoni, -1 = gra bez drużyn)
    public int team = Teams.None;

    public bool IsAlive => !left && health != null && !health.IsDead;
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

    // Numer slotu postaci (-1, gdy nie bierze udziału w rundzie).
    public int SlotOf(PlayerHealth health)
    {
        if (health != null)
        {
            foreach (Combatant combatant in Slots)
            {
                if (combatant != null && combatant.health == health)
                {
                    return combatant.slot;
                }
            }
        }

        return -1;
    }

    // Czy ten uczestnik wygrał rundę: sam zwycięzca albo (w drużynach) każdy z jego drużyny, także ten, kto zginął.
    public static bool IsWinner(Combatant combatant, Combatant winner)
    {
        return winner != null && combatant != null && (combatant == winner || Teams.Allies(combatant, winner));
    }

    public static string NameOf(Combatant combatant)
    {
        if (combatant.remote && !string.IsNullOrEmpty(combatant.nick))
        {
            return combatant.nick;
        }

        return combatant.isBot ? Loc.F("hud.bot", combatant.slot) : (GameRules.Custom || LobbyState.InLobby ? PlayerProfile.Nick : Loc.T("hud.you"));
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

        bool online = OnlineMatch.Active;

        // licznik monet u góry ekranu
        if (GetComponent<CoinHud>() == null)
        {
            gameObject.AddComponent<CoinHud>();
        }


        if (online)
        {
            StartOnline(human);
        }

        // runda z poczekalni: czas do CHAOS z ustawień hosta (największa wartość = CHAOS wyłączony)
        if (GameRules.Custom)
        {
            chaosEnabled = GameRules.Current.chaosSeconds < LobbySettings.ChaosOff;
            chaosAfterSeconds = GameRules.Current.chaosSeconds;
        }

        // dymki emotek nad głowami graczy
        if (GetComponent<EmoteBubbles>() == null)
        {
            gameObject.AddComponent<EmoteBubbles>();
        }

        // strzałki z buźkami graczy, którzy są poza kadrem
        if (FindAnyObjectByType<OffscreenIndicators>() == null)
        {
            gameObject.AddComponent<OffscreenIndicators>();
        }

        // menu główne: boty pojawiają się dopiero po wyborze trybu (liczbę wybiera gracz), a skiny po wyborze skina
        bool menu = showMainMenu && MainMenu.ShouldShow && !online;

        if (menu)
        {
            MainMenu.Open();
        }
        else
        {
            SpawnBots(human, online ? 0 : (MainMenu.GameStarted ? MainMenu.BotCount : botCount));
            ApplyLooks();
        }

        if (menu || playIntro)
        {
            StartCoroutine(IntroRoutine(human, menu));
        }
    }

    // Runda online: ja stoję w swoim rogu, a każdy pozostały gracz dostaje kopię postaci ruszaną pozycją z sieci.
    void StartOnline(PlayerHealth human)
    {
        OnlineMatch.BombPrefab = human.GetComponent<BombController>().bombPrefab;
        GridMap map = GridMap.Instance;

        foreach (OnlineSlot info in OnlineMatch.Players)
        {
            Vector2Int spawn = map.spawnPositions[info.spawn % map.spawnPositions.Length];

            if (info.isMe)
            {
                Slots[0].onlineId = info.id;
                Slots[0].nick = info.nick;
                human.GetComponent<PlayerMovement>().WarpTo(spawn);
                continue;
            }

            if (info.slot <= 0 || info.slot >= MaxSlots)
            {
                continue;
            }

            GameObject copy = Instantiate(human.gameObject, human.transform.parent);
            copy.name = "Player " + info.nick;

            PlayerMovement movement = copy.GetComponent<PlayerMovement>();
            movement.useKeyboard = false;
            movement.networkDriven = true;
            movement.remoteId = info.id;
            movement.WarpTo(spawn);

            // cudzą postać kontroluje jej właściciel: bomby stawia przez sieć, a śmierć ogłasza sam
            copy.GetComponent<BombController>().enabled = false;
            copy.GetComponent<PlayerHealth>().remoteControlled = true;

            Register(copy.GetComponent<PlayerHealth>(), info.slot, false);
            Slots[info.slot].remote = true;
            Slots[info.slot].onlineId = info.id;
            Slots[info.slot].nick = info.nick;
        }

        if (GetComponent<OnlineLink>() == null)
        {
            gameObject.AddComponent<OnlineLink>();
        }
    }

    // Cudzy gracz zginął (przyszła wiadomość albo wyszedł z gry).
    public void OnlineDead(string playerId)
    {
        foreach (Combatant combatant in Slots)
        {
            if (combatant != null && combatant.remote && combatant.onlineId == playerId && combatant.health != null && !combatant.health.IsDead)
            {
                combatant.health.Kill();
            }
        }
    }

    // Postać zginęła: zabójstwo dostaje ten, czyja bomba podpaliła pole chwilę wcześniej (nie liczy się własna bomba).
    // Moneta za zabicie należy się tylko za prawdziwego gracza, nie za bota.
    public void NotifyDeath(PlayerHealth victimHealth)
    {
        Combatant victim = null;

        foreach (Combatant combatant in Slots)
        {
            if (combatant != null && combatant.health == victimHealth)
            {
                victim = combatant;
            }
        }

        // statystyki profilu: moja śmierć
        if (victim != null && victim.slot == 0 && !victim.remote)
        {
            PlayerProgress.Add(PlayerStat.Deaths);
        }

        int killerSlot = victimHealth.LastHitSlot;

        if (victim == null || killerSlot < 0 || killerSlot >= MaxSlots || killerSlot == victim.slot || Time.time - victimHealth.LastHitAt > 1.5f)
        {
            return;
        }

        Combatant killer = Slots[killerSlot];

        // zabicie kolegi z drużyny (przy włączonym "ogniu w swoich") nie liczy się do zabójstw ani monet
        if (killer == null || Teams.Allies(killer, victim))
        {
            return;
        }

        killer.kills++;

        // statystyki profilu: moje zabójstwa (gracze i boty liczone osobno)
        if (killer.slot == 0)
        {
            PlayerProgress.Add(victim.isBot ? PlayerStat.BotKills : PlayerStat.PlayerKills);
        }

        if (!victim.isBot)
        {
            killer.humanKills++;

            // monety dostaję tylko za moje własne zabójstwa
            if (killer.slot == 0)
            {
                PlayerWallet.AddCoins(PlayerWallet.PlayerKillReward);
                GameAudio.Play(AudioEvent.CoinGain);
            }
        }
    }

    // Gracz wyszedł z gry (Quit albo zerwane połączenie): jego postać znika z areny, a runda liczy go jako martwego.
    public void OnlineLeft(string playerId)
    {
        foreach (Combatant combatant in Slots)
        {
            if (combatant != null && combatant.remote && combatant.onlineId == playerId && !combatant.left)
            {
                combatant.left = true;

                if (combatant.health != null)
                {
                    combatant.health.gameObject.SetActive(false);
                }

                NoticeLeft(combatant.nick);
            }
        }
    }

    void NoticeLeft(string nick)
    {
        Debug.Log("Gracz wyszedł z gry: " + nick);
    }

    // Host ogłosił koniec rundy. Pusty identyfikator = remis.
    public void OnlineEnd(string winnerId)
    {
        if (RoundOver)
        {
            return;
        }

        Combatant winner = null;

        foreach (Combatant combatant in Slots)
        {
            if (combatant != null && !string.IsNullOrEmpty(winnerId) && combatant.onlineId == winnerId)
            {
                winner = combatant;
            }
        }

        EndRoundCore(winner, true);
    }

    void SpawnBots(PlayerHealth human, int count)
    {
        // każda runda losuje boty od nowa: inne zwierzaki, żadnych dwóch takich samych
        GameArt art = GameArt.Instance;

        if (art != null)
        {
            art.ShuffleBotSkins();
        }

        // tryb drużynowy bez internetu: boty zawsze dopełniają skład do czterech (ja i bot kontra dwa boty)
        if (Teams.Active && !OnlineMatch.Active)
        {
            count = MaxSlots - 1;
        }

        for (int slot = 1; slot <= count && slot < MaxSlots; slot++)
        {
            SpawnBot(human, slot);
        }
    }

    // Wpisuje w każdą postać jej skin (GameArt) i odcień slotu bota. Wołane przy starcie i po wyborze w menu.
    void ApplyLooks()
    {
        AssignTeams();

        // startowe statystyki, rozmiar i prędkość z ustawień poczekalni (puste w zwykłej grze)
        foreach (Combatant combatant in Slots)
        {
            if (combatant != null && combatant.health != null)
            {
                GameRules.ApplyToPlayer(combatant.health.gameObject, combatant.isBot ? botSpeedMultiplier : 1f);
            }
        }

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

    // Tryb drużynowy: wpisuje każdemu drużynę, kolor drużyny (karta w HUD, strzałki, wyniki) i pierścień pod stopami.
    // Bez internetu ja i pierwszy bot gramy razem przeciwko dwóm pozostałym botom (startujemy po lewej stronie areny),
    // a online drużyny rozdaje host i przysyła je razem ze startem rundy.
    void AssignTeams()
    {
        if (!Teams.Active || LobbyState.InLobby)
        {
            return;
        }

        int myTeam = LobbyState.MyTeam == 2 ? Teams.Red : Teams.Blue;

        foreach (Combatant combatant in Slots)
        {
            if (combatant == null || combatant.health == null)
            {
                continue;
            }

            if (OnlineMatch.Active)
            {
                OnlineSlot info = OnlineMatch.SlotInfo(combatant.slot);
                combatant.team = info != null ? info.team : Teams.None;
            }
            else
            {
                combatant.team = combatant.slot < 2 ? myTeam : 1 - myTeam;
            }

            if (combatant.team < 0)
            {
                continue;
            }

            Color color = Teams.ColorOf(combatant.team);

            if (combatant.slot < slotColors.Length)
            {
                slotColors[combatant.slot] = color;
            }

            TeamRing.Attach(combatant.health.gameObject, color);
        }

        GameHud hud = FindAnyObjectByType<GameHud>();

        if (hud != null)
        {
            hud.ForceRefresh();
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

            // multiplayer: najpierw poczekalnia, a rundę zaczyna host przyciskiem START
            if (LobbyState.InLobby)
            {
                InputLocked = false;
                ApplyLooks();
                LobbyController.Begin(human);

                if (arenaCamera != null)
                {
                    arenaCamera.SetMode(ArenaCamera.ViewMode.Medium, true);
                }

                yield break;
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
        yield return intro.Show(Loc.T("round.ready"), readyColor, readySeconds);

        InputLocked = false;

        if (arenaCamera != null)
        {
            arenaCamera.SetMode(ArenaCamera.ViewMode.Medium);
        }

        GameAudio.Play(AudioEvent.RoundGo);
        yield return intro.Show(Loc.T("round.go"), goColor, goSeconds);
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

        BombController bombs = health.GetComponent<BombController>();

        if (bombs != null)
        {
            bombs.ownerSlot = slot;
        }
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

        // runda online: koniec rundy ogłasza host, reszta czeka na jego wiadomość
        if (OnlineMatch.Active && !OnlineMatch.IsHost)
        {
            return;
        }

        // tuż po wybuchu atomówki czekamy, aż wszystkie ofiary zginą
        if (Time.time < nukeGraceUntil)
        {
            return;
        }

        int total = 0;
        int alive = 0;
        Combatant last = null;
        bool blueAlive = false;
        bool redAlive = false;
        bool anyTeam = false;

        foreach (Combatant combatant in Slots)
        {
            if (combatant == null)
            {
                continue;
            }

            total++;
            anyTeam |= combatant.team >= 0;

            if (combatant.IsAlive)
            {
                alive++;
                last = combatant;
                blueAlive |= combatant.team == Teams.Blue;
                redAlive |= combatant.team == Teams.Red;
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

        // tryb drużynowy: runda trwa, dopóki żyje ktoś z obu drużyn (wygrywa cała drużyna ostatniego żywego)
        int sidesAlive = Teams.Active && anyTeam ? (blueAlive ? 1 : 0) + (redAlive ? 1 : 0) : alive;

        if (sidesAlive <= 1)
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
        yield return banner.Show(Loc.T("round.chaos"), chaosColor, 2.6f, 150f);
        Destroy(banner.gameObject);
    }

    void EndRound(Combatant winner, bool scorePoint)
    {
        // runda online: host mówi pozostałym, kto wygrał (pusty identyfikator = remis)
        if (OnlineMatch.Active && OnlineMatch.IsHost && scorePoint)
        {
            OnlineMatch.SendEnd(winner != null ? winner.onlineId : "");
        }

        EndRoundCore(winner, scorePoint);
    }

    void EndRoundCore(Combatant winner, bool scorePoint)
    {
        RoundOver = true;

        // punkt za rundę: zwycięzca, a w drużynach każdy z wygranej drużyny
        if (winner != null && scorePoint)
        {
            foreach (Combatant combatant in Slots)
            {
                if (IsWinner(combatant, winner))
                {
                    scores[combatant.slot]++;
                }
            }
        }

        bool teamWin = winner != null && Teams.Active && winner.team >= 0;

        GameAudio.Play(winner == null ? AudioEvent.RoundDraw : AudioEvent.RoundWin);
        banner = winner == null ? (scorePoint ? Loc.T("round.draw") : "")
            : teamWin ? Teams.NameOf(winner.team) + Loc.P(" WYGRYWAJĄ RUNDĘ", " WINS THE ROUND")
            : Loc.F("round.wins", NameOf(winner));

        // moneta za rozegraną rundę (każdy gracz u siebie dostaje +1, także po przegranej)
        if (scorePoint)
        {
            PlayerWallet.AddCoins(PlayerWallet.RoundReward);
        }

        // profil: statystyki rundy, doświadczenie i ewentualny awans na wyższy poziom
        if (scorePoint && Slots[0] != null)
        {
            GameArt art = GameArt.Instance;
            string skin = art != null && art.SkinCount > 0 ? art.players[Mathf.Clamp(SkinSelection.Index, 0, art.SkinCount - 1)].label : "";
            Combatant me = Slots[0];

            PlayerProgress.RoundFinished(IsWinner(me, winner), me.humanKills, me.kills - me.humanKills, OnlineMatch.Active, skin);
        }

        // monety za wygraną rundę (tylko dla mnie: bot ani cudzy gracz nie dają monet; w drużynach liczy się wygrana drużyny)
        if (scorePoint && IsWinner(Slots[0], winner))
        {
            PlayerWallet.AddCoins(PlayerWallet.CurrentWinReward);
            GameAudio.Play(AudioEvent.CoinGain);
        }

        // okienko z wynikami (zabicia, wygrane, monety) zostaje na ekranie dłużej niż sam napis
        float delay = restartDelay;

        if (scorePoint)
        {
            RoundResults.Show(this, winner);
            delay = Mathf.Max(restartDelay, 6f);
        }

        // runda online: następną planszę zaczyna host (klienci dostają od niego wiadomość o starcie)
        if (!OnlineMatch.Active || OnlineMatch.IsHost)
        {
            Invoke(nameof(Restart), delay);
        }
    }

    void Restart()
    {
        if (OnlineMatch.Active)
        {
            OnlineMatch.HostNextRound();
            return;
        }

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