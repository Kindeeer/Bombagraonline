using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiKit;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Menu główne i pauza: Play (Solo / Multi, wybór liczby graczy), Skins, Settings (Sound, Music), a w grze zębatka
// z pauzą (Resume, Settings, Quit).
//
// Skrypt tylko podpina akcje pod obiekty UI i pokazuje właściwe strony. Wygląd to zwykły Canvas w Hierarchii
// (obiekt MainMenuCanvas z tym komponentem).
// Gdy takiego Canvasu nie ma w scenie, menu buduje się samo w locie (ten sam wygląd, tylko bez edycji w Hierarchii).
// Obiekty szukane są po nazwach (Page_*, Button_*, Row_*), więc ich nazw nie zmieniaj, a resztę (pozycje, kolory,
// sprite'y, napisy) możesz edytować dowolnie.
// Grafiki z GameArt (sekcja "Menu główne": tło, logo, przyciski, karty) nadpisują sprite'y z Canvasu, gdy są wpisane.
public class MainMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    // true, gdy w trakcie gry otwarto pauzę (zębatka albo Escape).
    public static bool PauseOpen { get; private set; }

    // true od wciśnięcia Play do powrotu do menu: kolejne rundy (restart sceny) już nie pokazują menu.
    public static bool GameStarted { get; private set; }

    public static bool ShouldShow => !GameStarted;

    const string BotCountKey = "bot_count";
    const int MinBots = 1;
    const int MaxBots = MatchManager.MaxSlots - 1;

    static int botCount = -1;
    static MainMenu instance;

    // Ile botów gra w rundzie (1-3, czyli 2-4 graczy razem z Tobą).
    public static int BotCount
    {
        get
        {
            if (botCount < 0)
            {
                botCount = Mathf.Clamp(PlayerPrefs.GetInt(BotCountKey, MaxBots), MinBots, MaxBots);
            }

            return botCount;
        }
    }

    // Czy tryb wieloosobowy jest dostępny (na razie nie: multiplayer robimy na końcu).
    public static bool MultiplayerAvailable => false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsOpen = false;
        PauseOpen = false;
        GameStarted = false;
        botCount = -1;
        instance = null;
    }

    // Menu ze sceny (albo nowe, zbudowane w locie, gdy w scenie go nie ma).
    static MainMenu Ensure()
    {
        if (instance == null)
        {
            instance = FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include);
        }

        if (instance == null)
        {
            GameObject go = new GameObject(MenuBuilder.CanvasName, typeof(RectTransform));
            instance = go.AddComponent<MainMenu>();
        }

        instance.Initialize();
        return instance;
    }

    public static void Open()
    {
        Ensure().Show();
    }

    // Pauza w trakcie gry (zębatka w HUD): Resume, Settings, Quit.
    public static void OpenPause()
    {
        if (!IsOpen && !PauseOpen)
        {
            Ensure().ShowPause();
        }
    }

    // Wraca do menu głównego (np. z menu debug): wczytuje scenę od nowa i pokazuje menu.
    public static void ReturnToMenu()
    {
        GameStarted = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    enum Page
    {
        Main,
        Mode,
        Bots,
        Settings,
        Skins,
        Pause,
        PauseSettings
    }

    bool initialized;
    GameObject root;
    GameObject pauseRoot;
    RectTransform skinGrid;
    GameObject skinTemplate;
    Page current = Page.Main;

    readonly Dictionary<Page, RectTransform> pages = new Dictionary<Page, RectTransform>();
    readonly List<Action> settingsRefreshers = new List<Action>();
    readonly List<Action> skinRefreshers = new List<Action>();
    readonly List<SwitchVisual> switches = new List<SwitchVisual>();

    void Awake()
    {
        instance = this;

        // menu zbudowane w Hierarchii jest widoczne w edytorze, więc w grze chowamy je od razu (otwiera je MatchManager)
        Initialize();
        root.SetActive(false);

        if (pauseRoot != null)
        {
            pauseRoot.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // ---------- Przygotowanie: znalezienie obiektów i podpięcie akcji ----------

    void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        // brak zbudowanego menu: budujemy je w locie
        if (FindDeep(transform, "Background") == null)
        {
            MenuBuilder.SetupCanvas(gameObject);
            MenuBuilder.Build(transform);
        }

        Transform background = FindDeep(transform, "Background");
        root = background.gameObject;

        Transform pauseTransform = FindDeep(transform, "PauseOverlay");
        pauseRoot = pauseTransform != null ? pauseTransform.gameObject : null;

        foreach (Page page in Enum.GetValues(typeof(Page)))
        {
            Transform found = FindDeep(transform, "Page_" + page);

            if (found != null)
            {
                pages[page] = (RectTransform)found;
            }
        }

        Transform grid = FindDeep(transform, "Grid");
        skinGrid = grid != null ? (RectTransform)grid : null;

        Transform template = FindDeep(transform, "SkinCardTemplate");
        skinTemplate = template != null ? template.gameObject : null;

        EnsureButtonFx();
        ApplyArt();
        Bind();
    }

    // Menu zbudowane w Hierarchii mogło mieć uszkodzony efekt przycisków (po przeniesieniu klasy ButtonFx do osobnego pliku).
    // Przyciskom, które go nie mają, dokładamy nowy.
    void EnsureButtonFx()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            Image image = button.GetComponent<Image>();

            if (image != null && button.GetComponent<ButtonFx>() == null)
            {
                button.transition = Selectable.Transition.None;
                AddButtonFx(button, image);
            }
        }
    }

    static Transform FindDeep(Transform start, string objectName)
    {
        foreach (Transform child in start.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    // Grafiki z GameArt (tło, logo, przyciski) nadpisują sprite'y z Canvasu, gdy są wpisane.
    void ApplyArt()
    {
        GameArt art = GameArt.Instance;

        if (art == null)
        {
            return;
        }

        Image background = root.GetComponent<Image>();

        if (art.menuBackground != null && background != null)
        {
            background.sprite = art.menuBackground;
            background.type = Image.Type.Simple;
            background.color = Color.white;
        }

        Transform logo = FindDeep(transform, "Logo");
        Transform title = pages.ContainsKey(Page.Main) ? pages[Page.Main].Find("Title") : null;

        if (art.menuLogo != null && logo != null)
        {
            logo.GetComponent<Image>().sprite = art.menuLogo;
            logo.gameObject.SetActive(true);

            if (title != null)
            {
                title.gameObject.SetActive(false);
            }
        }

        // wiersze ustawień, przełączniki ON/OFF i panel pauzy
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            Image image = t.GetComponent<Image>();

            if (image == null)
            {
                continue;
            }

            if (t.name.Contains("Row_") && art.menuRow != null)
            {
                image.sprite = art.menuRow;
                image.type = Image.Type.Sliced;
            }
            else if (t.name == "Switch" && art.menuSwitchTrack != null)
            {
                image.type = Image.Type.Sliced;
                ButtonFx fx = t.GetComponent<ButtonFx>();

                if (fx != null)
                {
                    fx.SetSprites(art.menuSwitchTrack, null, null);
                }
                else
                {
                    image.sprite = art.menuSwitchTrack;
                }
            }
            else if (t.name == "Knob" && art.menuSwitchKnob != null)
            {
                image.sprite = art.menuSwitchKnob;
            }
            else if (t.name == "PausePanel" && art.menuPausePanel != null)
            {
                image.sprite = art.menuPausePanel;
                image.type = Image.Type.Sliced;
            }
        }

        if (art.menuButton != null)
        {
            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                ButtonFx fx = button.GetComponent<ButtonFx>();

                if (fx == null || !button.name.StartsWith("Button_"))
                {
                    continue;
                }

                Image image = button.GetComponent<Image>();
                image.sprite = art.menuButton;
                image.type = Image.Type.Sliced;
                fx.SetSprites(art.menuButton, art.menuButtonHover, art.menuButtonPressed);
            }
        }
    }

    void Bind()
    {
        Wire("Button_Play", () => ShowPage(Page.Mode));
        Wire("Button_Skins", () => ShowPage(Page.Skins));
        Wire("Button_Settings", () => ShowPage(Page.Settings));
        Wire("Button_Solo", () => ShowPage(Page.Bots));
        Wire("Button_Bots1", () => StartGame(1));
        Wire("Button_Bots2", () => StartGame(2));
        Wire("Button_Bots3", () => StartGame(3));
        Wire("Button_Back", Back);

        // Multi: przycisk jest, ale nieaktywny, dopóki nie powstanie tryb sieciowy
        Transform multi = FindDeep(transform, "Button_Multi");

        if (multi != null)
        {
            multi.GetComponent<Button>().interactable = MultiplayerAvailable;
            multi.GetComponent<Image>().color = MultiplayerAvailable ? MenuBuilder.ChocolateColor : MenuBuilder.DisabledColor;
        }

        // pauza
        Wire("Button_Resume", ClosePause);
        Wire("Button_PauseSettings", () => ShowPage(Page.PauseSettings));
        Wire("Button_Quit", QuitToMenu);
        Wire("Button_PauseBack", () => ShowPage(Page.Pause));

        BindSwitch("Row_Sound", () => !GameAudio.MuteSound, on => GameAudio.MuteSound = !on);
        BindSwitch("Row_Music", () => !GameAudio.MuteMusic, on => GameAudio.MuteMusic = !on);
        BindSwitch("PauseRow_Sound", () => !GameAudio.MuteSound, on => GameAudio.MuteSound = !on);
        BindSwitch("PauseRow_Music", () => !GameAudio.MuteMusic, on => GameAudio.MuteMusic = !on);
    }

    // Podpina akcję pod wszystkie przyciski o danej nazwie (np. kilka przycisków Wstecz).
    void Wire(string objectName, Action action)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == objectName)
            {
                WireClick(button, action, true);
            }
        }
    }

    void BindSwitch(string rowName, Func<bool> isOn, Action<bool> setOn)
    {
        Transform row = FindDeep(transform, rowName);
        Transform track = row != null ? row.Find("Switch") : null;

        if (track == null)
        {
            return;
        }

        Button button = track.GetComponent<Button>();
        Transform knob = track.Find("Knob");
        TextMeshProUGUI label = track.GetComponentInChildren<TextMeshProUGUI>(true);

        if (button == null || knob == null || label == null)
        {
            return;
        }

        SwitchVisual visual = new SwitchVisual
        {
            track = track.GetComponent<Image>(),
            knob = (RectTransform)knob,
            label = label
        };

        switches.Add(visual);

        WireClick(button, () =>
        {
            setOn(!isOn());
            PlayerPrefs.Save();
            visual.target = isOn();
        }, true);

        // przy wejściu na ekran przełącznik ustawia się od razu, bez animacji
        settingsRefreshers.Add(() =>
        {
            visual.target = isOn();
            visual.t = visual.target ? 1f : 0f;
            visual.Apply();
        });

        visual.target = isOn();
        visual.t = visual.target ? 1f : 0f;
        visual.Apply();
    }

    // Wygląd jednego przełącznika: t = 0 (OFF, gałka z lewej) ... 1 (ON, gałka z prawej).
    class SwitchVisual
    {
        public Image track;
        public RectTransform knob;
        public TextMeshProUGUI label;
        public bool target;
        public float t;

        public void Step(float dt)
        {
            t = Mathf.MoveTowards(t, target ? 1f : 0f, dt * 9f);
            Apply();
        }

        public void Apply()
        {
            float margin = 12f + (MenuBuilder.SwitchHeight - 24f) * 0.5f;
            knob.anchoredPosition = new Vector2(Mathf.Lerp(margin, MenuBuilder.SwitchWidth - margin, t), 0f);
            track.color = Color.Lerp(MenuBuilder.SwitchOffColor, MenuBuilder.SwitchOnColor, t);

            // napis stoi po przeciwnej stronie niż gałka
            label.text = target ? "ON" : "OFF";
            label.alignment = target ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
            label.margin = new Vector4(34f, 0f, 34f, 0f);
        }
    }

    // ---------- Działanie ----------

    void Update()
    {
        // przycisk Wstecz na telefonie (Escape) cofa o jeden ekran, a w trakcie gry otwiera pauzę
        if (BackPressed())
        {
            if (IsOpen || PauseOpen)
            {
                Back();
            }
            else
            {
                OpenPause();
            }
        }

        // animacja przełączników: gałka płynnie jedzie na drugą stronę
        if ((IsOpen && current == Page.Settings) || (PauseOpen && current == Page.PauseSettings))
        {
            foreach (SwitchVisual visual in switches)
            {
                visual.Step(Time.unscaledDeltaTime);
            }
        }
    }

    static bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    // Kolory przycisków wyboru liczby graczy (1, 2 i 3 boty): zielony, niebieski i pomarańczowy.
    // Ustawiane z kodu przy wejściu na ekran, więc nadpisują kolory z Hierarchii. Zmień je tutaj.
    static readonly Color[] BotButtonColors =
    {
        new Color(0.30f, 0.72f, 0.35f, 1f),
        new Color(0.25f, 0.55f, 0.95f, 1f),
        new Color(0.93f, 0.55f, 0.18f, 1f)
    };

    void HighlightBotChoice()
    {
        for (int bots = MinBots; bots <= MaxBots; bots++)
        {
            Transform found = FindDeep(transform, "Button_Bots" + bots);

            if (found != null)
            {
                found.GetComponent<Image>().color = BotButtonColors[Mathf.Clamp(bots - 1, 0, BotButtonColors.Length - 1)];
            }
        }
    }

    void Show()
    {
        EnsureEventSystem();

        root.SetActive(true);
        IsOpen = true;
        GameAudio.PlayMusic(MusicTrack.Menu);
        ShowPage(Page.Main);
    }

    void Close()
    {
        IsOpen = false;
        root.SetActive(false);
    }

    void StartGame(int bots)
    {
        botCount = Mathf.Clamp(bots, MinBots, MaxBots);
        PlayerPrefs.SetInt(BotCountKey, botCount);
        PlayerPrefs.Save();

        GameStarted = true;
        GameAudio.PlayMusic(MusicTrack.Game);
        Close();
    }

    void Back()
    {
        switch (current)
        {
            case Page.Bots:
                ShowPage(Page.Mode);
                break;
            case Page.Mode:
            case Page.Settings:
            case Page.Skins:
                ShowPage(Page.Main);
                break;
            case Page.Pause:
                ClosePause();
                break;
            case Page.PauseSettings:
                ShowPage(Page.Pause);
                break;
        }
    }

    void ShowPage(Page page)
    {
        current = page;

        foreach (KeyValuePair<Page, RectTransform> entry in pages)
        {
            entry.Value.gameObject.SetActive(entry.Key == page);
        }

        if (page == Page.Bots)
        {
            HighlightBotChoice();
        }

        if (page == Page.Settings || page == Page.PauseSettings)
        {
            foreach (Action refresh in settingsRefreshers)
            {
                refresh();
            }
        }
        else if (page == Page.Skins)
        {
            RebuildSkins();
        }
    }

    // ---------- Pauza ----------

    void ShowPause()
    {
        if (pauseRoot == null)
        {
            return;
        }

        EnsureEventSystem();
        pauseRoot.SetActive(true);
        PauseOpen = true;
        Time.timeScale = 0f;
        ShowPage(Page.Pause);
    }

    void ClosePause()
    {
        PauseOpen = false;
        Time.timeScale = 1f;

        if (pauseRoot != null)
        {
            pauseRoot.SetActive(false);
        }
    }

    // Quit z pauzy: kończy rundę i wraca do menu głównego.
    void QuitToMenu()
    {
        ClosePause();
        ReturnToMenu();
    }

    // ---------- Skiny ----------

    // Karty skinów powstają przy każdym wejściu na ekran, bo lista w GameArt może się zmienić.
    // Każda karta jest kopią obiektu SkinCardTemplate (jego wygląd edytuje grafik w Hierarchii).
    void RebuildSkins()
    {
        if (skinGrid == null || skinTemplate == null)
        {
            return;
        }

        for (int i = skinGrid.childCount - 1; i >= 0; i--)
        {
            GameObject child = skinGrid.GetChild(i).gameObject;

            if (child != skinTemplate)
            {
                Destroy(child);
            }
        }

        skinRefreshers.Clear();

        GameArt art = GameArt.Instance;

        if (art == null || art.SkinCount == 0)
        {
            TextMeshProUGUI empty = NewText(skinGrid, "Brak skinów. Dodaj je w obiekcie GameArt (lista Skiny postaci).",
                48f, MenuBuilder.CardColor, TextAlignmentOptions.Center);
            empty.name = "EmptyInfo";
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 200f;
            return;
        }

        for (int i = 0; i < art.SkinCount; i++)
        {
            AddSkinCard(art, i);
        }

        RefreshSkins();
    }

    void AddSkinCard(GameArt art, int index)
    {
        PlayerSprites skin = art.players[index];

        GameObject card = Instantiate(skinTemplate, skinGrid);
        card.name = "Skin_" + index;
        card.SetActive(true);

        Image cardImage = card.GetComponent<Image>();
        Button button = card.GetComponent<Button>();
        ButtonFx fx = card.GetComponent<ButtonFx>();

        if (art.menuCard != null)
        {
            cardImage.sprite = art.menuCard;
            cardImage.type = Image.Type.Sliced;

            if (fx != null)
            {
                fx.SetSprites(art.menuCard, art.menuCardHover, art.menuCardPressed);
            }
        }
        else if (fx != null && (art.menuCardHover != null || art.menuCardPressed != null))
        {
            fx.SetSprites(null, art.menuCardHover, art.menuCardPressed);
        }

        Image preview = card.transform.Find("Preview").GetComponent<Image>();
        Sprite sprite = skin.Portrait != null ? skin.Portrait : skin.Front;
        preview.sprite = sprite != null ? sprite : PickupArt.Face(false);

        TextMeshProUGUI nameText = card.transform.Find("Name").GetComponent<TextMeshProUGUI>();
        nameText.text = string.IsNullOrWhiteSpace(skin.label) ? "Skin " + (index + 1) : skin.label;

        TextMeshProUGUI tag = card.transform.Find("Tag").GetComponent<TextMeshProUGUI>();

        WireClick(button, () =>
        {
            SkinSelection.Index = index;
            PlayerPrefs.Save();
            RefreshSkins();
        }, true);

        Color normalColor = cardImage.color;

        skinRefreshers.Add(() =>
        {
            bool selected = Mathf.Clamp(SkinSelection.Index, 0, art.SkinCount - 1) == index;
            cardImage.color = selected ? new Color(0.62f, 0.92f, 0.62f, 1f) : normalColor;
            tag.text = selected ? "WYBRANY" : "";
        });
    }

    void RefreshSkins()
    {
        foreach (Action refresh in skinRefreshers)
        {
            refresh();
        }
    }
}
