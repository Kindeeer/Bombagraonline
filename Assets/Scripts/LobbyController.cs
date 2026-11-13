using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiKit;

// Poczekalnia multiplayer: mały pokój w arenie, w którym gracze chodzą, zanim host zacznie rundę.
// Na dole widać kod pokoju, po prawej host ustawia zasady (arena, suwaki, drop przedmiotów, zapisy).
// START zapisuje ustawienia w GameRules i wczytuje arenę od nowa, a runda gra już według nich.
// Cały wygląd budowany jest z kodu na osobnym Canvasie, więc nie ma nic do ustawiania w scenie.
public class LobbyController : MonoBehaviour
{
    static readonly RectInt Room = new RectInt(3, 3, 11, 9);
    const float PanelStart = 0.70f;
    public const float PanelStartFraction = PanelStart;

    static readonly Color PanelColor = new Color(0.28f, 0.09f, 0.21f, 0.97f);
    static readonly Color RowColor = new Color(0.97f, 0.84f, 0.68f, 0.95f);
    static readonly Color Dark = new Color(0.15f, 0.12f, 0.15f);

    PlayerHealth human;
    Camera arenaCamera;
    ArenaCamera arena;
    RectTransform codeBar;
    RectTransform root;
    RectTransform nickRect;
    RectTransform advancedBox;
    TextMeshProUGUI rulesSummary;
    readonly Dictionary<string, RemoteAvatar> avatars = new Dictionary<string, RemoteAvatar>();
    float publishAt;

    class RemoteAvatar
    {
        public string id;
        public GameObject body;
        public Transform visual;
        public SpriteRenderer renderer;
        public PlayerSprites look;
        public RectTransform nick;
        public Vector3 target;
        public bool hasTarget;
        public Facing facing;
        public bool moving;
        public float walk;
    }

    float sendTimer;
    PlayerMovement humanMovement;

    static readonly Vector2Int[] RemoteSpots =
    {
        new Vector2Int(6, 7), new Vector2Int(10, 7), new Vector2Int(8, 9), new Vector2Int(8, 5)
    };
    RectTransform leaveRect;
    RectTransform content;
    TextMeshProUGUI toast;
    TextMeshProUGUI modeLabel;
    TextMeshProUGUI advancedToggleLabel;
    TextMeshProUGUI hintText;
    readonly List<Action> refreshers = new List<Action>();
    readonly List<Image> slotImages = new List<Image>();
    bool saveMode;
    bool dirty;
    float hintUntil;

    LobbySettings Settings => LobbyState.Settings;

    public static void Begin(PlayerHealth player)
    {
        GameObject go = new GameObject("Lobby");
        go.AddComponent<LobbyController>().Init(player);
    }

    void Init(PlayerHealth player)
    {
        human = player;
        GridMap map = GridMap.Instance;

        // pokój poczekalni zamiast areny, bez fontanny i bomb
        map.BuildLobbyRoom(Room);

        ChocolateFountain fountain = FindAnyObjectByType<ChocolateFountain>();

        if (fountain != null)
        {
            fountain.gameObject.SetActive(false);
        }

        human.transform.position = map.GridToWorld(new Vector2Int(Room.xMin + Room.width / 2, Room.yMin + Room.height / 2));
        humanMovement = human.GetComponent<PlayerMovement>();

        arena = FindAnyObjectByType<ArenaCamera>();
        arenaCamera = arena != null ? arena.GetComponent<Camera>() : null;

        // HUD: prawy panel (przyciski gry) znika, bo zasłania go panel zasad; karty graczy odświeżają się z nickiem
        GameHud hud = FindAnyObjectByType<GameHud>();

        if (hud != null)
        {
            if (hud.rightPanel != null)
            {
                hud.rightPanel.gameObject.SetActive(false);
            }

            hud.ForceRefresh();
        }

        BuildUi();
        RefreshAll();
        hintUntil = Time.unscaledTime + 9f;

        if (!string.IsNullOrEmpty(LobbyState.Notice))
        {
            ShowToast(LobbyState.Notice);
        }

        RefreshOnline();
    }

    void OnDestroy()
    {
        SaveIfDirty();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveIfDirty();
        }
    }

    void SaveIfDirty()
    {
        if (dirty)
        {
            LobbyState.SaveLast();
            dirty = false;
        }
    }

    void Changed()
    {
        dirty = true;

        // zasady hosta lecą do pozostałych graczy chwilę po ostatniej zmianie (żeby nie wysyłać ich co ułamek sekundy)
        if (OnlineSession.Connected && LobbyState.IsHost)
        {
            publishAt = Time.unscaledTime + 0.8f;
        }
    }

    void LateUpdate()
    {
        PollOnline();

        // nick nad głową gracza
        if (nickRect != null && arenaCamera != null && human != null)
        {
            Vector3 screen = arenaCamera.WorldToScreenPoint(human.transform.position + Vector3.up * 1.15f);
            nickRect.position = screen;
        }

        UpdateAvatars();

        FitToArenaWindow();

        if (hintText != null && hintUntil > 0f && Time.unscaledTime > hintUntil)
        {
            hintText.gameObject.SetActive(false);
            hintUntil = 0f;
        }
    }

    // Panel zasad zasłania prawy bok ekranu, więc okno z areną kończy się przed nim (plansza jest wycentrowana między panelami),
    // a kod, podpowiedzi i przycisk WYJDŹ trzymają się środka tego okna.
    void FitToArenaWindow()
    {
        if (arena == null || arenaCamera == null || Screen.width == 0)
        {
            return;
        }

        float rightFraction = 1f - PanelStart;

        if (Mathf.Abs(arena.rightPanel - rightFraction) > 0.001f)
        {
            arena.SetSidePanels(arena.leftPanel, rightFraction);
        }

        Rect view = arenaCamera.pixelRect;
        float left = view.xMin / Screen.width;
        float right = view.xMax / Screen.width;
        float margin = (right - left) * 0.04f;

        if (codeBar != null)
        {
            Anchor(codeBar, left + margin, 0.02f, right - margin, 0.16f);
        }

        Anchor(toast.rectTransform, left + margin, 0.17f, right - margin, 0.24f);
        Anchor(hintText.rectTransform, left + margin, 0.82f, right - margin, 0.93f);
        Anchor(leaveRect, left + margin, 0.92f, left + margin + (right - left) * 0.34f, 0.99f);

    }

    // ---------- Gracze online ----------

    void PollOnline()
    {
        if (!OnlineSession.Connected)
        {
            return;
        }

        if (OnlineSession.TakeEnded())
        {
            // host zamknął pokój
            Leave();
            return;
        }

        if (OnlineSession.TakeRefresh())
        {
            RefreshOnline();
        }

        if (publishAt > 0f && Time.unscaledTime >= publishAt)
        {
            publishAt = 0f;
            OnlineSession.PublishRules(Settings);
        }
    }

    // Wysyła swój ruch i płynnie przesuwa postacie pozostałych graczy do ich ostatnich znanych pozycji.
    void UpdateAvatars()
    {
        if (OnlineSession.Connected && LobbyNet.Ready && humanMovement != null)
        {
            LobbyNet.EnsureHandler(Unity.Netcode.NetworkManager.Singleton);
            OnlineMatch.EnsureHandlers();
            sendTimer -= Time.unscaledDeltaTime;

            if (sendTimer <= 0f)
            {
                sendTimer = 0.06f;
                LobbyNet.Send(human.transform.position, humanMovement.Facing, humanMovement.IsMoving);
            }
        }

        foreach (RemoteAvatar avatar in avatars.Values)
        {
            if (avatar.body == null)
            {
                continue;
            }

            if (LobbyNet.States.TryGetValue(avatar.id, out RemoteState state))
            {
                avatar.target = new Vector3(state.position.x, state.position.y, 0f);
                avatar.hasTarget = true;
                avatar.facing = state.facing;
                avatar.moving = state.moving;
            }

            if (avatar.hasTarget)
            {
                avatar.body.transform.position = Vector3.Lerp(avatar.body.transform.position, avatar.target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            }

            // ta sama kolejność rysowania względem ścian co u własnej postaci
            Vector3 place = avatar.body.transform.position;
            avatar.body.transform.position = new Vector3(place.x, place.y, PlayerVisual.SortDepth(place.y));

            ShowAvatarFrame(avatar);

            if (arenaCamera != null && avatar.nick != null)
            {
                avatar.nick.position = arenaCamera.WorldToScreenPoint(avatar.body.transform.position + Vector3.up * 1.15f);
            }
        }
    }

    // Kierunek patrzenia i kołysanie przy chodzeniu (te same zasady co u własnej postaci).
    static void ShowAvatarFrame(RemoteAvatar avatar)
    {
        if (avatar.look == null || avatar.renderer == null)
        {
            return;
        }

        Sprite sprite;
        bool flip = false;

        switch (avatar.facing)
        {
            case Facing.Up:
                sprite = avatar.look.Back;
                break;
            case Facing.Left:
                sprite = avatar.look.Left != null ? avatar.look.Left : avatar.look.Right;
                flip = avatar.look.Left == null;
                break;
            case Facing.Right:
                sprite = avatar.look.Right;
                break;
            default:
                sprite = avatar.look.Front;
                break;
        }

        avatar.renderer.sprite = sprite != null ? sprite : avatar.look.Front;
        avatar.renderer.flipX = flip;

        float wave = 0f;

        if (avatar.moving)
        {
            avatar.walk += Time.deltaTime * 16f;
            wave = Mathf.Sin(avatar.walk);
        }
        else
        {
            avatar.walk = 0f;
        }

        float lean = avatar.facing == Facing.Right ? -6f : avatar.facing == Facing.Left ? 6f : 0f;
        float tilt = avatar.moving ? (avatar.facing == Facing.Up || avatar.facing == Facing.Down ? wave * 7f : lean + wave * 3f) : 0f;

        avatar.visual.localPosition = new Vector3(0f, -0.12f + Mathf.Abs(wave) * 0.05f, 0f);
        avatar.visual.localScale = new Vector3(1f + wave * wave * 0.05f, 1f - wave * wave * 0.05f, 1f);
        avatar.visual.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }

    // Odświeża listę graczy, postacie pozostałych graczy w pokoju i (u gości) podsumowanie zasad hosta.
    void RefreshOnline()
    {
        if (!OnlineSession.Connected)
        {
            return;
        }

        List<OnlinePlayer> players = OnlineSession.Players();
        SyncAvatars(players);

        if (!LobbyState.IsHost)
        {
            LobbySettings rules = OnlineSession.ReadRules();

            if (rules != null)
            {
                LobbyState.RemoteRules = rules;

                if (rulesSummary != null)
                {
                    rulesSummary.text = Summarize(rules);
                }
            }
        }
    }

    // Pozostali gracze stoją w pokoju (ich ruch po sieci dojdzie w następnym kroku).
    void SyncAvatars(List<OnlinePlayer> players)
    {
        GameArt art = GameArt.Instance;
        GridMap map = GridMap.Instance;

        if (art == null || map == null)
        {
            return;
        }

        HashSet<string> present = new HashSet<string>();
        int spot = 0;

        foreach (OnlinePlayer player in players)
        {
            if (player.isMe)
            {
                continue;
            }

            present.Add(player.id);
            RemoteAvatar avatar;

            if (!avatars.TryGetValue(player.id, out avatar))
            {
                avatar = new RemoteAvatar { id = player.id };
                avatars[player.id] = avatar;

                avatar.body = new GameObject("Remote_" + player.nick);
                avatar.body.transform.position = map.GridToWorld(RemoteSpots[spot % RemoteSpots.Length]);

                GameObject shadow = new GameObject("Shadow");
                shadow.transform.SetParent(avatar.body.transform, false);
                SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
                shadowRenderer.sprite = art.playerShadow;
                shadowRenderer.sortingOrder = 0;

                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(avatar.body.transform, false);
                visual.transform.localPosition = Vector3.down * 0.12f;
                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 1;
                renderer.spriteSortPoint = SpriteSortPoint.Pivot;
                avatar.visual = visual.transform;
                avatar.renderer = renderer;

                TextMeshProUGUI nick = NewText(root, player.nick, 44f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                nick.name = "RemoteNick";
                nick.outlineWidth = 0.3f;
                nick.outlineColor = new Color32(30, 15, 25, 255);
                avatar.nick = nick.rectTransform;
                avatar.nick.anchorMin = avatar.nick.anchorMax = Vector2.zero;
                avatar.nick.pivot = new Vector2(0.5f, 0f);
                avatar.nick.sizeDelta = new Vector2(500f, 70f);
            }

            int skinIndex = Mathf.Max(0, Array.FindIndex(art.players, p => p.label == player.skin));
            PlayerSprites look = art.SkinLook(skinIndex, player.chroma);
            avatar.look = look;

            if (look != null && avatar.visual != null)
            {
                WorldChromaFx emitter = avatar.visual.GetComponent<WorldChromaFx>();
                EnchantGlint glint = avatar.visual.GetComponent<EnchantGlint>();

                if (emitter == null && look.fx.HasParticles)
                {
                    emitter = avatar.visual.gameObject.AddComponent<WorldChromaFx>();
                }

                if (glint == null && look.fx.glint)
                {
                    glint = avatar.visual.gameObject.AddComponent<EnchantGlint>();
                }

                if (emitter != null)
                {
                    emitter.Configure(avatar.renderer, look.fx);
                }

                if (glint != null)
                {
                    glint.Configure(avatar.renderer, look.fx);
                }
            }
            avatar.nick.GetComponent<TextMeshProUGUI>().text = player.nick;
            spot++;
        }

        // gracze, którzy wyszli
        List<string> gone = new List<string>();

        foreach (string id in avatars.Keys)
        {
            if (!present.Contains(id))
            {
                gone.Add(id);
            }
        }

        foreach (string id in gone)
        {
            Destroy(avatars[id].body);

            if (avatars[id].nick != null)
            {
                Destroy(avatars[id].nick.gameObject);
            }

            avatars.Remove(id);
        }
    }

    static string Summarize(LobbySettings rules)
    {
        return Loc.P("Arena: ", "Arena: ") + (rules.arena < 0 ? Loc.P("losowa", "random") : (rules.arena + 1).ToString())
            + "\n" + Loc.P("Prędkość: x", "Speed: x") + rules.speed.ToString("0.0")
            + "\n" + Loc.P("Wielkość postaci: x", "Character size: x") + rules.size.ToString("0.0")
            + "\n" + Loc.P("Szansa na przedmiot: x", "Item drop chance: x") + rules.dropChance.ToString("0.0")
            + "\n" + Loc.P("Lont bomby: x", "Bomb fuse: x") + rules.fuse.ToString("0.0")
            + "\n" + Loc.P("Bomby / zasięg na start: ", "Starting bombs / range: ") + rules.startBombs + " / " + rules.startRange
            + "\n" + Loc.P("Ilość bloków: x", "Blocks: x") + rules.blocks.ToString("0.0")
            + "\n" + Loc.P("CHAOS po: ", "CHAOS after: ") + (rules.chaosSeconds >= LobbySettings.ChaosOff ? "OFF" : Mathf.RoundToInt(rules.chaosSeconds) + "s");
    }

    // ---------- Wygląd ----------

    void BuildUi()
    {
        GameObject canvasObject = new GameObject("LobbyCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        MenuBuilder.SetupCanvas(canvasObject);
        canvasObject.GetComponent<Canvas>().sortingOrder = 120;
        root = (RectTransform)canvasObject.transform;

        EnsureEventSystem();

        BuildNickLabel();
        BuildCodeBar();
        BuildTopBar();
        BuildSettingsPanel();
    }

    void BuildNickLabel()
    {
        TextMeshProUGUI nick = NewText(root, PlayerProfile.Nick, 44f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        nick.name = "Nick";
        nick.outlineWidth = 0.3f;
        nick.outlineColor = new Color32(30, 15, 25, 255);
        nickRect = nick.rectTransform;
        nickRect.anchorMin = nickRect.anchorMax = Vector2.zero;
        nickRect.pivot = new Vector2(0.5f, 0f);
        nickRect.sizeDelta = new Vector2(500f, 70f);
    }

    // Kod pokoju na dole: duże litery i przycisk kopiowania.
    void BuildCodeBar()
    {
        Image bar = NewImage("CodeBar", root, new Color(0.18f, 0.06f, 0.14f, 0.92f));
        codeBar = bar.rectTransform;
        Anchor(codeBar, 0.285f, 0.02f, 0.685f, 0.16f);
        bar.raycastTarget = false;

        TextMeshProUGUI caption = NewText(bar.transform, Loc.P("KOD POCZEKALNI", "LOBBY CODE"), 34f,
            new Color(1f, 0.85f, 0.6f), TextAlignmentOptions.Center, FontStyles.Bold);
        Anchor(caption.rectTransform, 0.03f, 0.64f, 0.97f, 0.98f);

        TextMeshProUGUI code = NewText(bar.transform, LobbyState.Code, 96f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        code.characterSpacing = 18f;
        code.name = "Code";
        Anchor(code.rectTransform, 0.03f, 0.04f, 0.70f, 0.70f);

        Button copy = NewButton(bar.transform, Loc.P("KOPIUJ", "COPY"), MenuBuilder.OrangeColor, () =>
        {
            GUIUtility.systemCopyBuffer = LobbyState.Code;
            ShowToast(Loc.P("Skopiowano kod", "Code copied"));
        }, 38f, true);
        Anchor((RectTransform)copy.transform, 0.73f, 0.14f, 0.97f, 0.62f);

        toast = NewText(root, "", 36f, new Color(1f, 0.9f, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
        toast.outlineWidth = 0.25f;
        toast.outlineColor = new Color32(30, 15, 25, 255);
        Anchor(toast.rectTransform, 0.285f, 0.17f, 0.685f, 0.24f);

        hintText = NewText(root, Loc.P(
            "POCZEKALNIA: możesz tu chodzić. Host ustawia zasady po prawej, START zaczyna rundę.",
            "LOBBY: you can walk around here. The host sets the rules on the right, START begins the round."),
            32f, Color.white, TextAlignmentOptions.Center);
        hintText.outlineWidth = 0.25f;
        hintText.outlineColor = new Color32(30, 15, 25, 255);
        Anchor(hintText.rectTransform, 0.285f, 0.82f, 0.685f, 0.93f);
    }

    void BuildTopBar()
    {
        Button leave = NewButton(root, Loc.P("WYJDŹ", "LEAVE"), MenuBuilder.PlumColor, Leave, 40f, true);
        leaveRect = (RectTransform)leave.transform;
        Anchor(leaveRect, 0.285f, 0.92f, 0.43f, 0.99f);
    }

    void ShowToast(string text)
    {
        toast.text = text;
        CancelInvoke(nameof(ClearToast));
        Invoke(nameof(ClearToast), 2.5f);
    }

    void ClearToast()
    {
        if (toast != null)
        {
            toast.text = "";
        }
    }

    void BuildSettingsPanel()
    {
        Image panel = NewImage("SettingsPanel", root, PanelColor);
        Anchor(panel.rectTransform, PanelStart, 0f, 1f, 1f);

        TextMeshProUGUI title = NewText(panel.transform, Loc.P("ZASADY GRY", "GAME RULES"), 56f, MenuBuilder.CardColor,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Anchor(title.rectTransform, 0.03f, 0.92f, 0.97f, 0.99f);

        bool host = LobbyState.IsHost;

        if (!host)
        {
            TextMeshProUGUI wait = NewText(panel.transform, Loc.P("Czekasz, aż host zacznie rundę.", "Waiting for the host to start."),
                40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Anchor(wait.rectTransform, 0.05f, 0.78f, 0.95f, 0.90f);

            rulesSummary = NewText(panel.transform, Loc.P("Zasady hosta pojawią się tu za chwilę.", "The host's rules will show up here."),
                36f, MenuBuilder.CardColor, TextAlignmentOptions.TopLeft);
            Anchor(rulesSummary.rectTransform, 0.07f, 0.10f, 0.95f, 0.76f);
            return;
        }

        // przewijana lista ustawień
        RectTransform scrollRoot = NewRect("Scroll", panel.transform);
        Anchor(scrollRoot, 0.03f, 0.15f, 0.97f, 0.915f);

        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 50f;

        RectTransform viewport = NewRect("Viewport", scrollRoot);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.padding = new RectOffset(6, 6, 6, 24);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;

        BuildArenaRow();
        BuildSliders();
        BuildPresets();
        BuildAdvanced();

        Button start = NewButton(panel.transform, "START", MenuBuilder.OrangeColor, StartMatch, 78f, true);
        Anchor((RectTransform)start.transform, 0.06f, 0.02f, 0.94f, 0.13f);
    }

    // ---------- Wiersze ustawień ----------

    RectTransform NewRow(float height)
    {
        Image row = NewImage("Row", content, RowColor);
        row.raycastTarget = false;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return row.rectTransform;
    }

    void BuildArenaRow()
    {
        RectTransform row = NewRow(150f);

        TextMeshProUGUI label = NewText(row, Loc.P("ARENA", "ARENA"), 36f, Dark, TextAlignmentOptions.Center, FontStyles.Bold);
        Anchor(label.rectTransform, 0.05f, 0.62f, 0.95f, 0.98f);

        TextMeshProUGUI value = NewText(row, "", 48f, Dark, TextAlignmentOptions.Center, FontStyles.Bold);
        Anchor(value.rectTransform, 0.26f, 0.06f, 0.74f, 0.62f);

        Button left = NewButton(row, "<", MenuBuilder.PlumColor, () => ChangeArena(-1), 56f, true);
        Anchor((RectTransform)left.transform, 0.04f, 0.08f, 0.24f, 0.60f);
        Button right = NewButton(row, ">", MenuBuilder.PlumColor, () => ChangeArena(1), 56f, true);
        Anchor((RectTransform)right.transform, 0.76f, 0.08f, 0.96f, 0.60f);

        refreshers.Add(() => value.text = Settings.arena < 0 ? Loc.P("LOSOWA", "RANDOM") : (Settings.arena + 1).ToString());
    }

    void ChangeArena(int delta)
    {
        GridMap map = GridMap.Instance;
        int count = map != null && map.layouts != null ? Mathf.Max(1, map.layouts.Length) : 20;

        // -1 (losowa), potem 0 ... count - 1 i z powrotem do losowej
        int next = Settings.arena + delta;

        if (next < -1)
        {
            next = count - 1;
        }
        else if (next >= count)
        {
            next = -1;
        }

        Settings.arena = next;
        Changed();
        RefreshAll();
    }

    class SliderSpec
    {
        public string polish;
        public string english;
        public float min;
        public float max;
        public bool whole;
        public Func<LobbySettings, float> get;
        public Action<LobbySettings, float> set;
        public Func<float, string> format;
    }

    void BuildSliders()
    {
        Func<float, string> times = v => "x" + v.ToString("0.0");
        Func<float, string> whole = v => Mathf.RoundToInt(v).ToString();

        SliderSpec[] specs =
        {
            new SliderSpec { polish = "Prędkość graczy", english = "Player speed", min = 0.6f, max = 1.8f, format = times,
                get = s => s.speed, set = (s, v) => s.speed = v },
            new SliderSpec { polish = "Wielkość postaci", english = "Character size", min = 0.7f, max = 1.5f, format = times,
                get = s => s.size, set = (s, v) => s.size = v },
            new SliderSpec { polish = "Szansa na przedmiot z bloku", english = "Item drop chance", min = 0f, max = 3f, format = times,
                get = s => s.dropChance, set = (s, v) => s.dropChance = v },
            new SliderSpec { polish = "Długość lontu bomby", english = "Bomb fuse length", min = 0.5f, max = 1.6f, format = times,
                get = s => s.fuse, set = (s, v) => s.fuse = v },
            new SliderSpec { polish = "Bomby na start", english = "Starting bombs", min = 1f, max = 4f, whole = true, format = whole,
                get = s => s.startBombs, set = (s, v) => s.startBombs = Mathf.RoundToInt(v) },
            new SliderSpec { polish = "Zasięg wybuchu na start", english = "Starting blast range", min = 1f, max = 6f, whole = true, format = whole,
                get = s => s.startRange, set = (s, v) => s.startRange = Mathf.RoundToInt(v) },
            new SliderSpec { polish = "Ilość bloków na arenie", english = "Blocks on the arena", min = 0.3f, max = 1.4f, format = times,
                get = s => s.blocks, set = (s, v) => s.blocks = v },
            new SliderSpec { polish = "CHAOS zaczyna się po", english = "CHAOS starts after", min = 30f, max = LobbySettings.ChaosOff, whole = true,
                format = v => v >= LobbySettings.ChaosOff ? "OFF" : Mathf.RoundToInt(v) + "s",
                get = s => s.chaosSeconds, set = (s, v) => s.chaosSeconds = Mathf.Round(v / 5f) * 5f },
            new SliderSpec { polish = "Boty (zastępują graczy offline)", english = "Bots (stand in for players offline)", min = 0f, max = 3f, whole = true, format = whole,
                get = s => s.bots, set = (s, v) => s.bots = Mathf.RoundToInt(v) }
        };

        foreach (SliderSpec spec in specs)
        {
            AddSlider(spec);
        }
    }

    void AddSlider(SliderSpec spec)
    {
        RectTransform row = NewRow(135f);

        TextMeshProUGUI label = NewText(row, Loc.P(spec.polish, spec.english), 32f, Dark, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Anchor(label.rectTransform, 0.05f, 0.58f, 0.70f, 0.98f);

        TextMeshProUGUI value = NewText(row, "", 38f, new Color(0.65f, 0.25f, 0.05f), TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        Anchor(value.rectTransform, 0.62f, 0.58f, 0.95f, 0.98f);

        Slider slider = NewSlider(row, spec.min, spec.max, spec.whole, MenuBuilder.OrangeColor);
        Anchor((RectTransform)slider.transform, 0.03f, 0.04f, 0.97f, 0.62f);

        slider.onValueChanged.AddListener(v =>
        {
            spec.set(Settings, v);
            value.text = spec.format(spec.get(Settings));
            Changed();
        });

        refreshers.Add(() =>
        {
            slider.SetValueWithoutNotify(Mathf.Clamp(spec.get(Settings), spec.min, spec.max));
            value.text = spec.format(spec.get(Settings));
        });
    }

    // ---------- Zapisy ----------

    void BuildPresets()
    {
        RectTransform row = NewRow(300f);

        TextMeshProUGUI label = NewText(row, Loc.P("ZAPISY USTAWIEŃ", "SAVED RULES"), 32f, Dark, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Anchor(label.rectTransform, 0.05f, 0.80f, 0.95f, 0.98f);

        // sześć miejsc na zapisy w dwóch rzędach
        for (int slot = 0; slot < LobbyPresets.Slots; slot++)
        {
            int index = slot;
            Button button = NewButton(row, (slot + 1).ToString(), MenuBuilder.DisabledColor, () => PresetClicked(index), 54f, true);
            float column = slot % 3;
            bool upper = slot < 3;
            Anchor((RectTransform)button.transform, 0.04f + column * 0.32f, upper ? 0.56f : 0.30f,
                0.34f + column * 0.32f, upper ? 0.78f : 0.52f);
            slotImages.Add(button.GetComponent<Image>());
        }

        Button mode = NewButton(row, "", MenuBuilder.PlumColor, () =>
        {
            saveMode = !saveMode;
            RefreshPresetButtons();
        }, 32f, true);
        modeLabel = mode.GetComponentInChildren<TextMeshProUGUI>();
        Anchor((RectTransform)mode.transform, 0.04f, 0.02f, 0.50f, 0.26f);

        Button reset = NewButton(row, Loc.P("DOMYŚLNE", "DEFAULTS"), MenuBuilder.ChocolateColor, () =>
        {
            LobbyState.ReplaceSettings(new LobbySettings());
            RefreshAll();
            Changed();
            ShowToast(Loc.P("Przywrócono domyślne zasady", "Default rules restored"));
        }, 32f, true);
        Anchor((RectTransform)reset.transform, 0.54f, 0.02f, 0.96f, 0.26f);

        refreshers.Add(RefreshPresetButtons);
    }

    void RefreshPresetButtons()
    {
        for (int slot = 0; slot < slotImages.Count; slot++)
        {
            slotImages[slot].color = LobbyPresets.Has(slot) ? new Color(0.30f, 0.62f, 0.35f) : MenuBuilder.DisabledColor;
        }

        if (modeLabel != null)
        {
            modeLabel.text = saveMode ? Loc.P("TRYB: ZAPISZ", "MODE: SAVE") : Loc.P("TRYB: WCZYTAJ", "MODE: LOAD");
        }
    }

    void PresetClicked(int slot)
    {
        if (saveMode)
        {
            LobbyPresets.Save(slot, Settings.Clone());
            RefreshPresetButtons();
            ShowToast(Loc.P("Zapisano w miejscu " + (slot + 1), "Saved in slot " + (slot + 1)));
            return;
        }

        LobbySettings loaded = LobbyPresets.Load(slot);

        if (loaded == null)
        {
            ShowToast(Loc.P("Miejsce " + (slot + 1) + " jest puste", "Slot " + (slot + 1) + " is empty"));
            return;
        }

        LobbyState.ReplaceSettings(loaded);
        RefreshAll();
        Changed();
        ShowToast(Loc.P("Wczytano zapis " + (slot + 1), "Loaded slot " + (slot + 1)));
    }

    // ---------- Zaawansowane: dropy z bloków i fontanny ----------

    void BuildAdvanced()
    {
        RectTransform toggleRow = NewRow(100f);
        toggleRow.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        Button toggle = NewButton(toggleRow, "", MenuBuilder.PlumColor, () =>
        {
            advancedBox.gameObject.SetActive(!advancedBox.gameObject.activeSelf);
            RefreshAdvancedLabel();
        }, 36f, true);
        Stretch((RectTransform)toggle.transform);
        advancedToggleLabel = toggle.GetComponentInChildren<TextMeshProUGUI>();

        advancedBox = NewRect("Advanced", content);
        VerticalLayoutGroup layout = advancedBox.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        advancedBox.gameObject.AddComponent<LayoutElement>();

        PickupTable table = Resources.Load<PickupTable>(PickupTable.ResourcePath);

        TextMeshProUGUI hint = NewText(advancedBox, Loc.P(
            "Waga przedmiotu = jak często wypada. 0 = nigdy. Pierwsza liczba: z bloków, druga: z fontanny.",
            "Item weight = how often it drops. 0 = never. First number: from blocks, second: from the fountain."),
            28f, Color.white, TextAlignmentOptions.Center);
        hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;

        if (table != null)
        {
            foreach (PickupDefinition item in table.items)
            {
                if (item != null)
                {
                    AddWeightRow(item);
                }
            }
        }

        advancedBox.gameObject.SetActive(false);
        RefreshAdvancedLabel();
    }

    void RefreshAdvancedLabel()
    {
        if (advancedToggleLabel != null)
        {
            string open = advancedBox != null && advancedBox.gameObject.activeSelf ? "  ^" : "  v";
            advancedToggleLabel.text = Loc.P("ZAAWANSOWANE: DROPY", "ADVANCED: DROPS") + open;
        }
    }

    void AddWeightRow(PickupDefinition item)
    {
        Image row = NewImage("Weight_" + item.name, advancedBox, RowColor);
        row.raycastTarget = false;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 130f;

        TextMeshProUGUI name = NewText(row.transform, Loc.Pickup(item), 32f, Dark, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Anchor(name.rectTransform, 0.04f, 0.55f, 0.96f, 0.98f);

        TextMeshProUGUI blocks = AddStepper(row.transform, 0.03f, 0.47f,
            () => Settings.WeightFromBlocks(item), v => Settings.SetWeights(item, v, Settings.WeightFromFountain(item)));
        TextMeshProUGUI fountain = AddStepper(row.transform, 0.52f, 0.96f,
            () => Settings.WeightFromFountain(item), v => Settings.SetWeights(item, Settings.WeightFromBlocks(item), v));

        refreshers.Add(() =>
        {
            blocks.text = Settings.WeightFromBlocks(item).ToString();
            fountain.text = Settings.WeightFromFountain(item).ToString();
        });
    }

    // Mała liczba z przyciskami - i +. Zwraca napis z wartością.
    TextMeshProUGUI AddStepper(Transform parent, float xMin, float xMax, Func<int> get, Action<int> set)
    {
        TextMeshProUGUI value = NewText(parent, "", 40f, Dark, TextAlignmentOptions.Center, FontStyles.Bold);
        float width = xMax - xMin;
        Anchor(value.rectTransform, xMin + width * 0.30f, 0.06f, xMax - width * 0.30f, 0.54f);

        Button minus = NewButton(parent, "-", MenuBuilder.PlumColor, null, 44f, false);
        Anchor((RectTransform)minus.transform, xMin, 0.08f, xMin + width * 0.28f, 0.52f);
        Button plus = NewButton(parent, "+", MenuBuilder.PlumColor, null, 44f, false);
        Anchor((RectTransform)plus.transform, xMax - width * 0.28f, 0.08f, xMax, 0.52f);

        WireClick(minus, () => Step(get, set, value, -1), true);
        WireClick(plus, () => Step(get, set, value, 1), true);
        return value;
    }

    void Step(Func<int> get, Action<int> set, TextMeshProUGUI value, int direction)
    {
        int current = get();
        int step = current >= 20 ? 5 : 1;
        set(Mathf.Clamp(current + direction * step, 0, 100));
        value.text = get().ToString();
        Changed();
    }

    // ---------- Akcje ----------

    void RefreshAll()
    {
        foreach (Action refresh in refreshers)
        {
            refresh();
        }
    }

    void StartMatch()
    {
        SaveIfDirty();
        LobbyState.SaveLast();

        // przez internet: host zaczyna rundę dla wszystkich graczy z pokoju naraz
        if (OnlineSession.Connected && LobbyState.IsHost && LobbyNet.Ready)
        {
            GameAudio.Play(AudioEvent.RoundGo);
            OnlineMatch.HostStart(Settings);
            return;
        }

        GameRules.Use(Settings);
        MainMenu.SetBotCount(Settings.bots);
        LobbyState.End();

        GameAudio.Play(AudioEvent.RoundGo);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Leave()
    {
        SaveIfDirty();
        LobbyState.End();
        GameRules.Reset();
        OnlineSession.LeaveAsync();
        MainMenu.ReturnToMenu();
    }
}
