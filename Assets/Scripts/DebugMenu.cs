using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiKit;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

// Menu do testowania pod klawiszem TAB, w stylu sklepu: kategorie po lewej, duże przyciski po prawej, obsługa samą myszką.
// Kategorie: Cheaty (noclip, nieśmiertelność...), Przedmioty (każdy przedmiot na polu obok albo od razu w ręce),
// Statystyki, Runda, Kamera i czas.
// Tworzy się samo (nie trzeba nic dodawać do sceny) i działa tylko w edytorze oraz w buildach "Development Build".
// Ustawienia (noclip, nieśmiertelność, zamrożenie botów) zostają po restarcie rundy.
public class DebugMenu : MonoBehaviour
{
    // Boty stoją w miejscu i nic nie robią (czyta to BotBrain).
    public static bool FreezeBots { get; private set; }

    static bool noclip;
    static bool godMode;
    static DebugMenu instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        FreezeBots = false;
        noclip = false;
        godMode = false;
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (!Debug.isDebugBuild || instance != null)
        {
            return;
        }

        GameObject go = new GameObject("DebugMenu");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DebugMenu>();
    }

    // ---------- Wygląd ----------

    static readonly Color PanelColor = new Color(0.09f, 0.11f, 0.17f, 0.98f);
    static readonly Color SidebarColor = new Color(0.06f, 0.07f, 0.11f, 1f);
    static readonly Color RowColor = new Color(0.16f, 0.19f, 0.27f, 1f);
    static readonly Color IconBoxColor = new Color(0.08f, 0.1f, 0.15f, 1f);
    static readonly Color BlueColor = new Color(0.25f, 0.55f, 0.95f, 1f);
    static readonly Color GreenColor = new Color(0.22f, 0.72f, 0.38f, 1f);
    static readonly Color GrayColor = new Color(0.34f, 0.37f, 0.45f, 1f);
    static readonly Color RedColor = new Color(0.86f, 0.31f, 0.31f, 1f);
    static readonly Color MutedText = new Color(0.68f, 0.72f, 0.8f, 1f);

    static readonly float[] TimeScales = { 0.25f, 0.5f, 1f, 2f, 3f };

    class Page
    {
        public GameObject root;
        public RectTransform content;
        public Image tabImage;
    }

    bool open;
    bool pauseWhenOpen = true;
    float userTimeScale = 1f;
    float fps = 60f;

    GameObject overlay;
    TextMeshProUGUI badge;
    TextMeshProUGUI infoText;
    Page itemsPage;
    int currentPage;
    PickupTable table;
    ArenaCamera arenaCamera;

    readonly List<Page> pages = new List<Page>();
    readonly List<Action> refreshers = new List<Action>();

    void Start()
    {
        BuildCanvas();
    }

    void Update()
    {
        if (Time.unscaledDeltaTime > 0f)
        {
            fps = Mathf.Lerp(fps, 1f / Time.unscaledDeltaTime, 0.05f);
        }

        if (TabPressed())
        {
            SetOpen(!open);
        }

        Combatant me = Player();

        if (me != null && me.health != null)
        {
            PlayerMovement movement = me.health.GetComponent<PlayerMovement>();

            if (movement != null)
            {
                movement.noclip = noclip;
            }

            me.health.godMode = godMode;
        }

        float wanted = (open && pauseWhenOpen) || MainMenu.PauseOpen ? 0f : userTimeScale;

        if (!Mathf.Approximately(Time.timeScale, wanted))
        {
            Time.timeScale = wanted;
        }

        UpdateBadge();

        if (open)
        {
            foreach (Action refresh in refreshers)
            {
                refresh();
            }
        }
    }

    void OnDisable()
    {
        if (instance == this)
        {
            Time.timeScale = 1f;
            instance = null;
        }
    }

    static bool TabPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        // TAB w edytorze Unity przesuwa fokus na kontrolki okna (np. Simulatora), a potem Spacja je wciska,
        // dlatego menu otwiera też F1 i klawisz ` (nie ruszają fokusu)
        return keyboard != null
            && (keyboard.tabKey.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame || keyboard.backquoteKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.BackQuote);
#endif
    }

    void SetOpen(bool value)
    {
        open = value;

        if (open)
        {
            EnsureEventSystem();
            table = FindTable();
            RebuildItems();
            SelectPage(currentPage);
        }

        if (overlay != null)
        {
            overlay.SetActive(open);
        }
    }

    static PickupTable FindTable()
    {
        PickupSpawner spawner = PickupSpawner.Instance;

        if (spawner != null && spawner.table != null)
        {
            return spawner.table;
        }

        return Resources.Load<PickupTable>(PickupTable.ResourcePath);
    }

    static Combatant Player()
    {
        MatchManager match = MatchManager.Instance;
        return match != null ? match.Slots[0] : null;
    }

    static PlayerStats Stats()
    {
        Combatant me = Player();
        return me != null && me.health != null ? me.health.GetComponent<PlayerStats>() : null;
    }

    // ---------- Budowa interfejsu ----------

    void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("DebugCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        canvasObject.AddComponent<GraphicRaycaster>();

        // mała etykieta w rogu, gdy któryś cheat jest włączony (widać ją też przy zamkniętym menu)
        badge = NewText(canvasObject.transform, "", 30f, new Color(1f, 0.9f, 0.3f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        badge.outlineWidth = 0.25f;
        badge.outlineColor = new Color32(0, 0, 0, 255);
        badge.rectTransform.anchorMin = new Vector2(0f, 1f);
        badge.rectTransform.anchorMax = new Vector2(0f, 1f);
        badge.rectTransform.pivot = new Vector2(0f, 1f);
        badge.rectTransform.anchoredPosition = new Vector2(20f, -14f);
        badge.rectTransform.sizeDelta = new Vector2(1200f, 50f);
        badge.gameObject.SetActive(false);

        BuildOverlay(canvasObject.transform);
        overlay.SetActive(false);
    }

    void BuildOverlay(Transform canvasTransform)
    {
        // przyciemnienie całego ekranu; blokuje też kliknięcia w interfejs gry pod spodem
        Image dim = NewImage("Overlay", canvasTransform, new Color(0f, 0f, 0f, 0.62f), false);
        Stretch(dim.rectTransform);
        overlay = dim.gameObject;

        Image panel = NewImage("Panel", overlay.transform, PanelColor);
        panel.rectTransform.anchorMin = new Vector2(0.03f, 0.04f);
        panel.rectTransform.anchorMax = new Vector2(0.97f, 0.96f);
        panel.rectTransform.offsetMin = Vector2.zero;
        panel.rectTransform.offsetMax = Vector2.zero;

        // nagłówek
        TextMeshProUGUI title = NewText(panel.transform, "MENU DEBUG", 56f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(36f, -14f);
        title.rectTransform.sizeDelta = new Vector2(520f, 90f);

        infoText = NewText(panel.transform, "", 30f, MutedText, TextAlignmentOptions.MidlineRight);
        infoText.rectTransform.anchorMin = new Vector2(0f, 1f);
        infoText.rectTransform.anchorMax = new Vector2(1f, 1f);
        infoText.rectTransform.pivot = new Vector2(0.5f, 1f);
        infoText.rectTransform.offsetMin = new Vector2(580f, -104f);
        infoText.rectTransform.offsetMax = new Vector2(-150f, -14f);

        Button close = NewButton(panel.transform, "X", RedColor, () => SetOpen(false), 48f);
        RectTransform closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-24f, -16f);
        closeRect.sizeDelta = new Vector2(100f, 86f);

        // lewa kolumna: kategorie
        Image sidebar = NewImage("Sidebar", panel.transform, SidebarColor);
        sidebar.rectTransform.anchorMin = new Vector2(0f, 0f);
        sidebar.rectTransform.anchorMax = new Vector2(0f, 1f);
        sidebar.rectTransform.pivot = new Vector2(0f, 0.5f);
        sidebar.rectTransform.offsetMin = new Vector2(20f, 20f);
        sidebar.rectTransform.offsetMax = new Vector2(400f, -120f);

        VerticalLayoutGroup tabs = sidebar.gameObject.AddComponent<VerticalLayoutGroup>();
        tabs.padding = new RectOffset(14, 14, 14, 14);
        tabs.spacing = 14f;
        tabs.childControlWidth = true;
        tabs.childControlHeight = true;
        tabs.childForceExpandWidth = true;
        tabs.childForceExpandHeight = false;

        // prawa strona: zawartość wybranej kategorii
        RectTransform contentArea = NewRect("Content", panel.transform);
        contentArea.anchorMin = Vector2.zero;
        contentArea.anchorMax = Vector2.one;
        contentArea.offsetMin = new Vector2(420f, 20f);
        contentArea.offsetMax = new Vector2(-20f, -120f);

        BuildCheatsPage(AddPage("Cheaty", sidebar.transform, contentArea));
        itemsPage = AddPage("Przedmioty", sidebar.transform, contentArea);
        BuildStatsPage(AddPage("Statystyki", sidebar.transform, contentArea));
        BuildRoundPage(AddPage("Runda", sidebar.transform, contentArea));
        BuildCameraPage(AddPage("Kamera i czas", sidebar.transform, contentArea));

        TextMeshProUGUI hint = NewText(sidebar.transform, "TAB, F1 albo ` zamyka menu", 26f, MutedText, TextAlignmentOptions.Center);
        hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;

        refreshers.Add(UpdateInfo);
    }

    Page AddPage(string tabName, Transform sidebar, RectTransform contentArea)
    {
        Page page = new Page();
        int index = pages.Count;

        Button tab = NewButton(sidebar, tabName, GrayColor, () => SelectPage(index), 40f);
        tab.gameObject.AddComponent<LayoutElement>().preferredHeight = 110f;
        page.tabImage = tab.GetComponent<Image>();

        RectTransform root = NewRect("Page_" + tabName, contentArea);
        Stretch(root);
        page.root = root.gameObject;

        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 70f;

        RectTransform viewport = NewRect("Viewport", root);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        // przezroczysty obraz sprawia, że da się przewijać także przeciągając po pustym miejscu
        Image catcher = viewport.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);

        RectTransform content = NewRect("List", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        VerticalLayoutGroup list = content.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 16f;
        list.padding = new RectOffset(0, 12, 0, 20);
        list.childControlWidth = true;
        list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        page.content = content;

        pages.Add(page);
        return page;
    }

    void SelectPage(int index)
    {
        currentPage = Mathf.Clamp(index, 0, pages.Count - 1);

        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].root.SetActive(i == currentPage);
            pages[i].tabImage.color = i == currentPage ? BlueColor : GrayColor;
        }
    }

    // ---------- Strony ----------

    void BuildCheatsPage(Page page)
    {
        AddToggleRow(page, "Noclip", "Chodzisz przez bloki, ściany i bomby (zostajesz w obrębie planszy).",
            () => noclip, value => noclip = value);
        AddToggleRow(page, "Nieśmiertelność", "Wybuchy ci nie szkodzą.",
            () => godMode, value => godMode = value);
        AddToggleRow(page, "Zamroź boty", "Boty stoją w miejscu i nic nie robią.",
            () => FreezeBots, value => FreezeBots = value);
        AddToggleRow(page, "Loguj zdarzenia audio", "Wypisuje w Console każde zdarzenie dźwiękowe (bez FMOD też). Pomaga podpiąć eventy w FMOD.",
            () => GameAudio.LogEvents, value => GameAudio.LogEvents = value);
        AddToggleRow(page, "Pauza przy otwartym menu", "Gra stoi, dopóki menu jest otwarte. Wyłącz, żeby oglądać efekty na żywo.",
            () => pauseWhenOpen, value => pauseWhenOpen = value);
    }

    void BuildStatsPage(Page page)
    {
        RectTransform effects = AddRow(page, "Efekty", "", out TextMeshProUGUI effectsTitle);
        AddRowButton(effects, "Wyczyść efekty", RedColor, () =>
        {
            Combatant me = Player();

            if (me != null && me.pickups != null)
            {
                me.pickups.ClearEffects();
            }
        }, 330f);

        refreshers.Add(() => effectsTitle.text = "Efekty: " + DescribeEffects());

        AddStatRow(page, "Bomby", () => Stats() != null ? Stats().maxBombs.ToString() : "-",
            () => Stats()?.AddBomb(-1), () => Stats()?.AddBomb(1));
        AddStatRow(page, "Zasięg wybuchu", () => Stats() != null ? Stats().blastRange.ToString() : "-",
            () => Stats()?.AddRange(-1), () => Stats()?.AddRange(1));
        AddStatRow(page, "Prędkość", () => PlayerSpeedText(),
            () => Stats()?.AddSpeed(-1), () => Stats()?.AddSpeed(1));
        AddStatRow(page, "Lont bomby (s)", () => Stats() != null ? Stats().fuseTime.ToString("0.0") : "-",
            () => Stats()?.AddFuse(-0.5f), () => Stats()?.AddFuse(0.5f));

        RectTransform max = AddRow(page, "Wszystko na MAX", "Bomby, zasięg i prędkość na maksimum.", out _);
        AddRowButton(max, "MAX", GreenColor, () =>
        {
            PlayerStats stats = Stats();

            if (stats != null)
            {
                stats.AddBomb(99);
                stats.AddRange(99);
                stats.AddSpeed(99);
            }
        }, 330f);
    }

    void BuildRoundPage(Page page)
    {
        AddActionRow(page, "Restart rundy", "Wczytuje scenę od nowa.", "Restart", BlueColor, () =>
        {
            SetOpen(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        });
        AddActionRow(page, "Menu główne", "Wraca do ekranu startowego (Play, Skins, Settings).", "Menu", GrayColor, () =>
        {
            SetOpen(false);
            MainMenu.ReturnToMenu();
        });
        AddActionRow(page, "Zabij boty", "Wszystkie boty giną od razu.", "Zabij", RedColor, KillBots);
        AddActionRow(page, "Rozwal wszystkie bloki", "Niszczy każdy blok do zniszczenia (przedmioty mogą wypaść).", "Rozwal", RedColor, SmashAllBlocks);
        AddActionRow(page, "Usuń przedmioty z planszy", "Znikają wszystkie bąbelki leżące na ziemi.", "Usuń", GrayColor, RemoveAllPickups);
    }

    void BuildCameraPage(Page page)
    {
        AddStripRow(page, "Kamera", "Widok planszy.", new[] { "Cała", "Średni", "Bliski" },
            index => SetCamera((ArenaCamera.ViewMode)index),
            () => CurrentCameraMode());

        string[] timeLabels = new string[TimeScales.Length];

        for (int i = 0; i < TimeScales.Length; i++)
        {
            timeLabels[i] = TimeScales[i].ToString("0.##") + "x";
        }

        AddStripRow(page, "Tempo gry", "Spowalnia albo przyspiesza całą grę.", timeLabels,
            index => userTimeScale = TimeScales[index],
            () => ClosestTimeScale());
    }

    void RebuildItems()
    {
        if (itemsPage == null)
        {
            return;
        }

        for (int i = itemsPage.content.childCount - 1; i >= 0; i--)
        {
            Destroy(itemsPage.content.GetChild(i).gameObject);
        }

        if (table == null || table.items.Count == 0)
        {
            RectTransform row = AddRow(itemsPage, "Brak tabeli przedmiotów",
                "Nie znalazłem Assets/Resources/PickupTable albo jest pusta.", out _);
            return;
        }

        AddActionRow(itemsPage, "Rozsyp po jednym z każdego", "Każdy przedmiot ląduje na losowym wolnym polu planszy.", "Rozsyp", GreenColor, SpawnOneOfEach);

        TextMeshProUGUI help = NewText(itemsPage.content, "Obok = leży na polu przed Tobą.   Weź = działa od razu.", 28f, MutedText, TextAlignmentOptions.MidlineLeft);
        help.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

        RectTransform grid = NewRect("Items", itemsPage.content);
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(390f, 292f);
        layout.spacing = new Vector2(20f, 20f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = ColumnCount();

        foreach (PickupDefinition definition in table.items)
        {
            if (definition != null)
            {
                AddItemCard(grid, definition);
            }
        }
    }

    // Ile kart mieści się obok siebie przy obecnych proporcjach ekranu (ekran ma szerokość 1080 * proporcje jednostek).
    static int ColumnCount()
    {
        float canvasWidth = 1080f * Screen.width / Mathf.Max(1, Screen.height);
        float available = canvasWidth * 0.94f - 420f - 20f - 12f;
        return Mathf.Clamp(Mathf.FloorToInt((available + 20f) / 410f), 1, 5);
    }

    void AddItemCard(Transform parent, PickupDefinition definition)
    {
        Image card = NewImage("Card_" + definition.name, parent, RowColor);

        VerticalLayoutGroup vertical = card.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(16, 16, 16, 16);
        vertical.spacing = 8f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        RectTransform head = NewRect("Head", card.transform);
        HorizontalLayoutGroup headLayout = head.gameObject.AddComponent<HorizontalLayoutGroup>();
        headLayout.spacing = 14f;
        headLayout.childAlignment = TextAnchor.MiddleLeft;
        headLayout.childControlWidth = true;
        headLayout.childControlHeight = true;
        headLayout.childForceExpandWidth = false;
        headLayout.childForceExpandHeight = false;
        head.gameObject.AddComponent<LayoutElement>().preferredHeight = 92f;

        Image iconBox = NewImage("IconBox", head, IconBoxColor);
        LayoutElement iconSize = iconBox.gameObject.AddComponent<LayoutElement>();
        iconSize.preferredWidth = 92f;
        iconSize.preferredHeight = 92f;

        Image icon = NewImage("Icon", iconBox.transform, Color.white, false);
        Stretch(icon.rectTransform, 8f);
        icon.sprite = definition.Icon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TextMeshProUGUI name = NewText(head, definition.displayName, 36f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        TextMeshProUGUI description = NewText(card.transform, definition.description, 24f, MutedText, TextAlignmentOptions.TopLeft);
        description.overflowMode = TextOverflowModes.Ellipsis;
        description.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

        RectTransform buttons = NewRect("Buttons", card.transform);
        HorizontalLayoutGroup buttonLayout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 12f;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childForceExpandHeight = true;
        buttons.gameObject.AddComponent<LayoutElement>().preferredHeight = 76f;

        NewButton(buttons, "Obok", BlueColor, () => SpawnNextToPlayer(definition), 34f);
        NewButton(buttons, "Weź", GreenColor, () => Take(definition), 34f);
    }

    // ---------- Klocki interfejsu: wiersze ----------

    // Wiersz z tytułem i opisem po lewej; przyciski dokłada się po prawej przez AddRowButton.
    RectTransform AddRow(Page page, string title, string description, out TextMeshProUGUI titleText)
    {
        Image row = NewImage("Row", page.content, RowColor);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;

        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(28, 20, 12, 12);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        RectTransform texts = NewRect("Texts", row.transform);
        VerticalLayoutGroup textLayout = texts.gameObject.AddComponent<VerticalLayoutGroup>();
        textLayout.childAlignment = TextAnchor.MiddleLeft;
        textLayout.childControlWidth = true;
        textLayout.childControlHeight = true;
        textLayout.childForceExpandWidth = true;
        textLayout.childForceExpandHeight = false;
        texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        titleText = NewText(texts, title, 40f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        if (!string.IsNullOrEmpty(description))
        {
            NewText(texts, description, 26f, MutedText, TextAlignmentOptions.TopLeft);
        }

        return (RectTransform)row.transform;
    }

    static Button AddRowButton(RectTransform row, string text, Color color, Action onClick, float width)
    {
        Button button = NewButton(row, text, color, onClick, 36f);
        LayoutElement size = button.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = width;
        size.minWidth = width;
        return button;
    }

    void AddToggleRow(Page page, string title, string description, Func<bool> get, Action<bool> set)
    {
        RectTransform row = AddRow(page, title, description, out _);
        Button button = AddRowButton(row, "", GrayColor, () => set(!get()), 260f);

        Image image = button.GetComponent<Image>();
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();

        refreshers.Add(() =>
        {
            bool on = get();
            image.color = on ? GreenColor : GrayColor;
            label.text = on ? "WŁĄCZONE" : "WYŁĄCZONE";
        });
    }

    void AddActionRow(Page page, string title, string description, string buttonText, Color color, Action action)
    {
        RectTransform row = AddRow(page, title, description, out _);
        AddRowButton(row, buttonText, color, action, 260f);
    }

    void AddStatRow(Page page, string title, Func<string> value, Action minus, Action plus)
    {
        RectTransform row = AddRow(page, title, "", out TextMeshProUGUI titleText);
        AddRowButton(row, "-", GrayColor, minus, 120f);
        AddRowButton(row, "+", GreenColor, plus, 120f);

        refreshers.Add(() => titleText.text = title + ": " + value());
    }

    // Wiersz z kilkoma przyciskami wyboru, z których jeden jest podświetlony.
    void AddStripRow(Page page, string title, string description, string[] labels, Action<int> pick, Func<int> selected)
    {
        RectTransform row = AddRow(page, title, description, out _);
        Image[] images = new Image[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            Button button = AddRowButton(row, labels[i], GrayColor, () => pick(index), 160f);
            images[i] = button.GetComponent<Image>();
        }

        refreshers.Add(() =>
        {
            int current = selected();

            for (int i = 0; i < images.Length; i++)
            {
                images[i].color = i == current ? BlueColor : GrayColor;
            }
        });
    }

    // ---------- Odświeżanie napisów ----------

    void UpdateBadge()
    {
        if (badge == null)
        {
            return;
        }

        string text = "";

        if (noclip) text += "NOCLIP   ";
        if (godMode) text += "GOD   ";
        if (FreezeBots) text += "BOTY STOP   ";
        if (!Mathf.Approximately(userTimeScale, 1f)) text += userTimeScale.ToString("0.##") + "x   ";

        bool show = text.Length > 0 && !open;
        badge.gameObject.SetActive(show);

        if (show)
        {
            badge.text = "DEBUG: " + text + "(F1 = menu)";
        }
    }

    void UpdateInfo()
    {
        Combatant me = Player();
        string where = "";

        if (me != null && me.health != null && GridMap.Instance != null)
        {
            Vector2Int cell = GridMap.Instance.WorldToGrid(me.health.transform.position);
            where = "pole " + cell.x + "," + cell.y + "    ";
        }

        infoText.text = where + fps.ToString("0") + " FPS";
    }

    static string DescribeEffects()
    {
        Combatant me = Player();
        PlayerPickups pickups = me != null ? me.pickups : null;

        if (pickups == null)
        {
            return "-";
        }

        string effects = "";

        if (pickups.ShieldCharges > 0) effects += "tarcza x" + pickups.ShieldCharges + ", ";
        if (pickups.CanKick) effects += "kopanie, ";
        if (pickups.IsInvincible) effects += "gwiazda, ";
        if (pickups.ReverseControls) effects += "odwrócone sterowanie, ";
        if (pickups.AutoBombs) effects += "biegunka bomb, ";
        if (pickups.HeldItem != null) effects += "w ręce: " + pickups.HeldItem.displayName + " x" + pickups.HeldUses + ", ";

        return effects.Length > 0 ? effects.Substring(0, effects.Length - 2) : "brak";
    }

    static string PlayerSpeedText()
    {
        Combatant me = Player();
        PlayerMovement movement = me != null && me.health != null ? me.health.GetComponent<PlayerMovement>() : null;
        return movement != null ? movement.baseSpeed.ToString("0.0") : "-";
    }

    int CurrentCameraMode()
    {
        if (arenaCamera == null)
        {
            arenaCamera = FindAnyObjectByType<ArenaCamera>();
        }

        return arenaCamera != null ? (int)arenaCamera.Mode : -1;
    }

    int ClosestTimeScale()
    {
        int best = 0;

        for (int i = 1; i < TimeScales.Length; i++)
        {
            if (Mathf.Abs(TimeScales[i] - userTimeScale) < Mathf.Abs(TimeScales[best] - userTimeScale))
            {
                best = i;
            }
        }

        return best;
    }

    // ---------- Akcje ----------

    static void SetCamera(ArenaCamera.ViewMode mode)
    {
        ArenaCamera camera = FindAnyObjectByType<ArenaCamera>();

        if (camera != null)
        {
            camera.SetMode(mode);
        }
    }

    static void Take(PickupDefinition definition)
    {
        Combatant me = Player();

        if (me != null && me.pickups != null)
        {
            me.pickups.Apply(definition);
        }
    }

    static void SpawnNextToPlayer(PickupDefinition definition)
    {
        Combatant me = Player();
        PickupSpawner spawner = PickupSpawner.Instance;
        GridMap map = GridMap.Instance;

        if (me == null || me.health == null || spawner == null || map == null)
        {
            Debug.LogWarning("DebugMenu: nie ma gracza albo PickupSpawner w scenie.");
            return;
        }

        PlayerMovement movement = me.health.GetComponent<PlayerMovement>();
        Vector2Int origin = map.WorldToGrid(me.health.transform.position);
        Vector2Int front = movement != null ? DirectionOf(movement.Facing) : Vector2Int.down;

        // najpierw pole przed postacią, potem pozostałe strony, na końcu pole o dwa dalej
        Vector2Int[] tries =
        {
            origin + front,
            origin + Vector2Int.right,
            origin + Vector2Int.left,
            origin + Vector2Int.up,
            origin + Vector2Int.down,
            origin + front * 2
        };

        foreach (Vector2Int cell in tries)
        {
            if (!map.IsWall(cell) && !BombRegistry.HasBomb(cell) && !PickupRegistry.Has(cell) && spawner.SpawnOnGround(definition, cell) != null)
            {
                return;
            }
        }

        if (spawner.TryFindFreeCell(out Vector2Int free))
        {
            spawner.SpawnOnGround(definition, free);
        }
    }

    void SpawnOneOfEach()
    {
        PickupSpawner spawner = PickupSpawner.Instance;

        if (spawner == null || table == null)
        {
            return;
        }

        foreach (PickupDefinition definition in table.items)
        {
            if (definition != null && spawner.TryFindFreeCell(out Vector2Int cell))
            {
                spawner.SpawnOnGround(definition, cell);
            }
        }
    }

    static void RemoveAllPickups()
    {
        foreach (PickupItem item in FindObjectsByType<PickupItem>())
        {
            item.Collect();
        }
    }

    static void KillBots()
    {
        MatchManager match = MatchManager.Instance;

        if (match == null)
        {
            return;
        }

        for (int slot = 1; slot < match.Slots.Length; slot++)
        {
            Combatant bot = match.Slots[slot];

            if (bot != null && bot.IsAlive)
            {
                bot.health.Kill();
            }
        }
    }

    static void SmashAllBlocks()
    {
        GridMap map = GridMap.Instance;

        if (map == null)
        {
            return;
        }

        for (int x = 1; x < GridMap.Width - 1; x++)
        {
            for (int y = 1; y < GridMap.Height - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                if (TileRules.IsDestructible(map.GetTileType(cell)))
                {
                    map.SmashBlock(cell);
                }
            }
        }
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
