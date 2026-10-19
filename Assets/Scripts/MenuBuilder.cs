using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiKit;

// Buduje wygląd menu głównego i pauzy jako zwykłe obiekty UI (Canvas) z nazwami, po których szuka ich skrypt MainMenu.
// Używa go MainMenu: gdy w scenie nie ma obiektu Background (menu zbudowanego w Hierarchii), buduje menu w locie.
// UWAGA przy edycji menu w Hierarchii: nie zmieniaj nazw obiektów (Page_*, Button_*, Row_*, Switch, Knob, Grid,
// SkinCardTemplate, Preview, Name, Tag), bo po nich skrypt podpina akcje. Pozycje, rozmiary, kolory i grafiki możesz
// zmieniać dowolnie. SkinCardTemplate to wzór karty skina (wyłączony obiekt): jego wygląd kopiuje każda karta w Skins.
public static class MenuBuilder
{
    public const string CanvasName = "MainMenuCanvas";

    public static readonly Color BackgroundColor = new Color(0.36f, 0.13f, 0.27f, 1f);
    public static readonly Color CardColor = new Color(0.97f, 0.84f, 0.68f, 1f);
    public static readonly Color DarkText = new Color(0.15f, 0.12f, 0.15f, 1f);
    public static readonly Color OrangeColor = new Color(0.93f, 0.55f, 0.18f, 1f);
    public static readonly Color ChocolateColor = new Color(0.50f, 0.27f, 0.17f, 1f);
    public static readonly Color PlumColor = new Color(0.55f, 0.25f, 0.42f, 1f);
    public static readonly Color DisabledColor = new Color(0.45f, 0.35f, 0.42f, 1f);
    public static readonly Color SwitchOffColor = new Color(0.62f, 0.55f, 0.58f, 1f);
    public static readonly Color SwitchOnColor = new Color(0.30f, 0.72f, 0.35f, 1f);

    public const float SwitchWidth = 260f;
    public const float SwitchHeight = 124f;

    // Dodaje na obiekcie Canvas z ustawieniami skalowania jak w reszcie gry (1920 x 1080, dopasowanie do wysokości).
    public static void SetupCanvas(GameObject canvasObject)
    {
        Canvas canvas = canvasObject.GetComponent<Canvas>();

        if (canvas == null)
        {
            canvas = canvasObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();

        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }
    }

    // Tworzy całe menu (strony i pauzę) pod podanym Canvasem.
    public static void Build(Transform canvas)
    {
        Image background = NewImage("Background", canvas, BackgroundColor, false);
        Stretch(background.rectTransform);

        BuildMain(background.transform);
        BuildMode(background.transform);
        BuildBots(background.transform);
        BuildSettings(background.transform);
        BuildSkins(background.transform);
        BuildPause(canvas);
    }

    // ---------- Strony ----------

    static void BuildMain(Transform root)
    {
        RectTransform page = NewPage("Page_Main", root);
        AddTitle(page, "BOMB GAME", 170f);

        // logo: wyłączony obiekt, MainMenu włącza go, gdy w GameArt jest grafika logo (zamiast napisu)
        Image logo = NewImage("Logo", page, Color.white, false);
        logo.preserveAspect = true;
        logo.raycastTarget = false;
        Anchor(logo.rectTransform, 0.25f, 0.62f, 0.75f, 0.97f);
        logo.gameObject.SetActive(false);

        RectTransform column = AddColumn(page, 760f, 620f, -150f);
        AddMenuButton(column, "Button_Play", "PLAY", OrangeColor, 190f, 100f);
        AddMenuButton(column, "Button_Skins", "SKINS", ChocolateColor, 150f, 70f);
        AddMenuButton(column, "Button_Settings", "SETTINGS", ChocolateColor, 150f, 70f);
    }

    static void BuildMode(Transform root)
    {
        RectTransform page = NewPage("Page_Mode", root);
        AddTitle(page, "TRYB GRY", 130f);

        RectTransform column = AddColumn(page, 900f, 500f, -90f);
        AddMenuButton(column, "Button_Solo", "SOLO", OrangeColor, 190f, 90f);
        AddMenuButton(column, "Button_Multi", "MULTI  (wkrótce)", DisabledColor, 190f, 80f);

        AddBackButton(page);
    }

    static void BuildBots(Transform root)
    {
        RectTransform page = NewPage("Page_Bots", root);
        AddTitle(page, "ILU GRACZY?", 130f);

        RectTransform column = AddColumn(page, 1000f, 640f, -110f);
        AddMenuButton(column, "Button_Bots1", "2 GRACZE   (1 bot)", new Color(0.30f, 0.72f, 0.35f), 170f, 70f);
        AddMenuButton(column, "Button_Bots2", "3 GRACZE   (2 boty)", new Color(0.25f, 0.55f, 0.95f), 170f, 70f);
        AddMenuButton(column, "Button_Bots3", "4 GRACZE   (3 boty)", OrangeColor, 170f, 70f);

        AddBackButton(page);
    }

    static void BuildSettings(Transform root)
    {
        RectTransform page = NewPage("Page_Settings", root);
        AddTitle(page, "USTAWIENIA", 130f);

        RectTransform column = AddColumn(page, 1100f, 400f, -90f);
        AddSwitchRow(column, "Row_Sound", "Sound", "Efekty dźwiękowe (OFF = mute sound)");
        AddSwitchRow(column, "Row_Music", "Music", "Muzyka w tle (OFF = mute music)");

        AddBackButton(page);
    }

    static void BuildSkins(Transform root)
    {
        RectTransform page = NewPage("Page_Skins", root);
        AddTitle(page, "SKINY", 130f);

        // przewijana siatka kart (lista skinów w GameArt może być dowolnie długa)
        RectTransform scrollRoot = NewRect("Scroll", page);
        Anchor(scrollRoot, 0.06f, 0.16f, 0.94f, 0.76f);

        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 60f;

        RectTransform viewport = NewRect("Viewport", scrollRoot);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        Image catcher = viewport.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);

        RectTransform grid = NewRect("Grid", viewport);
        grid.anchorMin = new Vector2(0f, 1f);
        grid.anchorMax = new Vector2(1f, 1f);
        grid.pivot = new Vector2(0.5f, 1f);
        grid.offsetMin = Vector2.zero;
        grid.offsetMax = Vector2.zero;

        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(330f, 430f);
        layout.spacing = new Vector2(28f, 28f);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.constraint = GridLayoutGroup.Constraint.Flexible;
        layout.padding = new RectOffset(10, 10, 10, 30);

        ContentSizeFitter fitter = grid.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = grid;

        BuildSkinCardTemplate(grid);
        AddBackButton(page);
    }

    // Wzór karty skina. Wyłączony obiekt: układ go pomija, a MainMenu kopiuje go dla każdego skina z GameArt.
    static void BuildSkinCardTemplate(RectTransform grid)
    {
        Image card = NewImage("SkinCardTemplate", grid, CardColor);
        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.transition = Selectable.Transition.None;
        AddButtonFx(button, card);

        Image preview = NewImage("Preview", card.transform, Color.white, false);
        Anchor(preview.rectTransform, 0.08f, 0.30f, 0.92f, 0.95f);
        preview.preserveAspect = true;
        preview.raycastTarget = false;

        TextMeshProUGUI name = NewText(card.transform, "Skin", 46f, DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        name.name = "Name";
        Anchor(name.rectTransform, 0.04f, 0.14f, 0.96f, 0.32f);

        TextMeshProUGUI tag = NewText(card.transform, "", 34f, new Color(0.10f, 0.45f, 0.15f), TextAlignmentOptions.Center, FontStyles.Bold);
        tag.name = "Tag";
        Anchor(tag.rectTransform, 0.04f, 0.02f, 0.96f, 0.14f);

        card.gameObject.SetActive(false);
    }

    // Pauza w grze (zębatka w HUD): przyciemnienie, panel na środku, dwie strony.
    static void BuildPause(Transform canvas)
    {
        Image dim = NewImage("PauseOverlay", canvas, new Color(0f, 0f, 0f, 0.65f), false);
        Stretch(dim.rectTransform);

        Image panel = NewImage("PausePanel", dim.transform, BackgroundColor);
        panel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        panel.rectTransform.sizeDelta = new Vector2(1500f, 900f);

        RectTransform pausePage = NewPage("Page_Pause", panel.transform);
        AddTitle(pausePage, "PAUZA", 120f);

        RectTransform column = AddColumn(pausePage, 900f, 560f, -70f);
        AddMenuButton(column, "Button_Resume", "RESUME", OrangeColor, 170f, 80f);
        AddMenuButton(column, "Button_PauseSettings", "SETTINGS", ChocolateColor, 150f, 70f);
        AddMenuButton(column, "Button_Quit", "QUIT", ChocolateColor, 150f, 70f);

        RectTransform settingsPage = NewPage("Page_PauseSettings", panel.transform);
        AddTitle(settingsPage, "USTAWIENIA", 110f);

        RectTransform switches = AddColumn(settingsPage, 1100f, 400f, -60f);
        AddSwitchRow(switches, "PauseRow_Sound", "Sound", "Efekty dźwiękowe (OFF = mute sound)");
        AddSwitchRow(switches, "PauseRow_Music", "Music", "Muzyka w tle (OFF = mute music)");

        AddBackButton(settingsPage, "Button_PauseBack");

        dim.gameObject.SetActive(false);
    }

    // ---------- Klocki ----------

    static RectTransform NewPage(string name, Transform parent)
    {
        RectTransform rect = NewRect(name, parent);
        Stretch(rect);
        return rect;
    }

    static void AddTitle(RectTransform page, string text, float size)
    {
        TextMeshProUGUI title = NewText(page, text, size, CardColor, TextAlignmentOptions.Center, FontStyles.Bold);
        title.name = "Title";
        Anchor(title.rectTransform, 0.05f, 0.74f, 0.95f, 0.97f);
        title.outlineWidth = 0.25f;
        title.outlineColor = new Color32(45, 20, 35, 255);
    }

    // Pionowa kolumna przycisków wyśrodkowana na ekranie.
    static RectTransform AddColumn(RectTransform page, float width, float height, float offsetY)
    {
        RectTransform column = NewRect("Buttons", page);
        column.anchorMin = new Vector2(0.5f, 0.5f);
        column.anchorMax = new Vector2(0.5f, 0.5f);
        column.pivot = new Vector2(0.5f, 0.5f);
        column.sizeDelta = new Vector2(width, height);
        column.anchoredPosition = new Vector2(0f, offsetY);

        VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return column;
    }

    static Button AddMenuButton(RectTransform column, string objectName, string text, Color color, float height, float fontSize)
    {
        Button button = NewButton(column, text, color, null, fontSize);
        button.name = objectName;
        button.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return button;
    }

    static void AddBackButton(RectTransform page, string objectName = "Button_Back")
    {
        Button back = NewButton(page, "WSTECZ", PlumColor, null, 52f);
        back.name = objectName;
        RectTransform rect = (RectTransform)back.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(60f, 50f);
        rect.sizeDelta = new Vector2(360f, 120f);
    }

    // Wiersz z przełącznikiem jak w grach mobilnych: karta z opisem po lewej i suwak ON/OFF po prawej.
    // Obiekty: Row (karta), Title, Subtitle, Switch (przycisk z napisem ON/OFF), Switch/Knob (gałka).
    static void AddSwitchRow(RectTransform column, string rowName, string title, string subtitle)
    {
        Image row = NewImage(rowName, column, CardColor);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 170f;

        TextMeshProUGUI titleText = NewText(row.transform, title, 62f, DarkText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        titleText.name = "Title";
        Anchor(titleText.rectTransform, 0.04f, 0.38f, 0.64f, 0.95f);

        TextMeshProUGUI subtitleText = NewText(row.transform, subtitle, 36f, new Color(0.35f, 0.28f, 0.32f), TextAlignmentOptions.MidlineLeft);
        subtitleText.name = "Subtitle";
        Anchor(subtitleText.rectTransform, 0.04f, 0.05f, 0.64f, 0.42f);

        Button toggle = NewButton(row.transform, "OFF", SwitchOffColor, null, 44f);
        toggle.name = "Switch";
        RectTransform trackRect = (RectTransform)toggle.transform;
        trackRect.anchorMin = new Vector2(1f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.anchoredPosition = new Vector2(-40f, 0f);
        trackRect.sizeDelta = new Vector2(SwitchWidth, SwitchHeight);

        Image knob = NewImage("Knob", toggle.transform, Color.white, false);
        knob.sprite = CircleSprite();
        knob.raycastTarget = false;
        knob.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        knob.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        knob.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        knob.rectTransform.sizeDelta = new Vector2(SwitchHeight - 24f, SwitchHeight - 24f);
        knob.rectTransform.anchoredPosition = new Vector2(12f + (SwitchHeight - 24f) * 0.5f, 0f);
    }
}
