using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Odświeża HUD zbudowany w Hierarchii (Canvas "HUD"). Układ, kolory i obrazki zmieniasz w edytorze,
// a ten skrypt tylko wpisuje aktualne dane. Grafiki HUD podmienia obiekt GameArt.
public class GameHud : MonoBehaviour
{
    [Serializable]
    public class PlayerSlot
    {
        public Image background;
        public Image face;
        public TMP_Text label;
        [Tooltip("Korona lidera. Puste = GameHud dorobi ją sam przy buźce.")]
        public Image crown;

        [Header("Emotka (dymek obok buźki)")]
        public GameObject emoteBubble;
        public Image emoteIcon;
        public TMP_Text emoteText;
    }

    [Header("Układ na różnych ekranach")]
    [Tooltip("Panele boczne zajmują całe wolne miejsce obok planszy (w granicach poniżej), więc na szerokich ekranach HUD wypełnia boki, a na wąskich się nie zgniata.")]
    public bool adaptPanelWidth = true;
    [Tooltip("Najmniejsza szerokość panelu bocznego jako ułamek wysokości ekranu.")]
    [Range(0.15f, 0.6f)] public float minPanelWidth = 0.30f;
    [Tooltip("Największa szerokość panelu bocznego jako ułamek wysokości ekranu.")]
    [Range(0.2f, 0.9f)] public float maxPanelWidth = 0.55f;
    [Tooltip("Skalowanie interfejsu: 0 = według szerokości ekranu, 1 = według wysokości. Wartość pośrednia zmniejsza różnicę wielkości między telefonami o różnych proporcjach.")]
    [Range(0f, 1f)] public float scaleMatch = 0.5f;

    [Header("Układ")]
    [Tooltip("Obszar bez wcięć ekranu telefonu. Jego kotwice ustawiają się same.")]
    public RectTransform safeArea;
    [Tooltip("Panele boczne. Kamera zostawia na nie miejsce według ich kotwic.")]
    public RectTransform leftPanel;
    public RectTransform rightPanel;

    [Header("Gracze")]
    public PlayerSlot[] playerSlots = new PlayerSlot[4];
    public Color[] playerColors =
    {
        new Color(0.30f, 0.78f, 0.35f),
        new Color(1f, 0.86f, 0.25f),
        new Color(1f, 0.38f, 0.38f),
        new Color(0.38f, 0.60f, 1f)
    };
    public Sprite faceAlive;
    public Sprite faceDead;
    [Tooltip("Obrazek korony. Puste = korona rysowana z kodu.")]
    public Sprite crownSprite;
    public Color cardColor = new Color(0.97f, 0.84f, 0.68f);
    public Color textColor = new Color(0.15f, 0.12f, 0.15f);
    public Color deadColor = new Color(0.85f, 0.15f, 0.15f);
    [Range(0f, 1f)] public float emptySlotAlpha = 0.3f;

    [Header("Slot Buff")]
    public Image buffIcon;
    [Tooltip("Pokazywane, gdy slot jest pusty (np. znak zapytania).")]
    public GameObject buffEmpty;
    public TMP_Text buffName;
    public TMP_Text buffCount;

    [Header("Małe sloty pod Buffem (tarcza i buty)")]
    [Tooltip("Ile miejsca (ułamek wysokości panelu bocznego) zajmuje rząd małych slotów. Duży Buff i przycisk akcji robią się o tyle niższe.")]
    [Range(0.08f, 0.3f)] public float passiveSlotsHeight = 0.13f;

    [Header("Przycisk akcji (Spacja)")]
    public Image actionIcon;
    public TMP_Text actionBadge;
    public Sprite bombIcon;

    PlayerPickups localPickups;
    PlayerStats localStats;

    // małe sloty pod Buffem: kumulowane przedmioty (tarcza, buty)
    readonly List<PassiveSlot> passives = new List<PassiveSlot>();

    // ostatnio pokazany stan slotów (0 pusty, 1 żyje, 2 martwy) i wynik, żeby nie budować napisów co klatkę
    int shownSkinVersion;
    int[] shownState;
    int[] shownScore;
    int shownLeader = -2;

    // do kiedy (czas bez skali) każda emotka ma być widoczna
    float[] emoteUntil;

    Rect lastSafeArea;
    Vector2Int lastScreenSize;

    // ostatnio pokazane wartości, żeby nie budować napisów co klatkę
    PickupDefinition shownBuff;
    int shownCount = -1;
    PickupDefinition shownAction;

    void OnEnable()
    {
        Emotes.Sent += OnEmoteSent;
    }

    void OnDisable()
    {
        Emotes.Sent -= OnEmoteSent;
    }

    void OnEmoteSent(int slot, Emote emote)
    {
        ShowEmote(slot, Emotes.SpriteFor(emote));
    }

    // Wpisuje w HUD grafiki z GameArt: buźki, koronę, ikonę bomby, tło paneli, klawisze i joystick.
    // Grafiki zastępcze są rozpoznawane po nazwie sprite'a (Panel, KeyCap), a joystick po nazwie obiektu.
    void ApplyArt()
    {
        GameArt art = GameArt.Instance;

        if (art == null)
        {
            return;
        }

        if (art.uiFace != null) faceAlive = art.uiFace;
        if (art.uiFaceDead != null) faceDead = art.uiFaceDead;
        if (art.uiCrown != null) crownSprite = art.uiCrown;
        if (art.uiBombButton != null) bombIcon = art.uiBombButton;

        foreach (Image image in GetComponentsInChildren<Image>(true))
        {
            Sprite current = image.sprite;

            if (current != null && art.uiPanel != null && (current.name == "Panel" || current.name == "ui_panel"))
            {
                image.sprite = art.uiPanel;
            }
            else if (current != null && art.uiKeyCap != null && (current.name == "KeyCap" || current.name == "ui_key"))
            {
                image.sprite = art.uiKeyCap;
            }
            else if (image.gameObject.name == "JoystickRing" && art.joystickRing != null)
            {
                image.sprite = art.joystickRing;
            }
            else if (image.gameObject.name == "JoystickKnob" && art.joystickKnob != null)
            {
                image.sprite = art.joystickKnob;
            }
        }
    }

    void Start()
    {
        CanvasScaler scaler = GetComponentInParent<CanvasScaler>();

        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            scaler.matchWidthOrHeight = scaleMatch;
        }

        ApplyArt();
        CreatePassiveSlots();
        CreateGearButton();
        CreateSecondBuffSlot();

        shownState = new int[playerSlots.Length];
        shownScore = new int[playerSlots.Length];
        Array.Fill(shownState, -1);
        emoteUntil = new float[playerSlots.Length];
        EnsureCrowns();

        // tło paneli i ramka okna z planszą (obiekty ze sceny albo tworzone w locie)
        BuildFrameObjects();

        UpdateSafeArea();

        ArenaCamera arenaCamera = FindAnyObjectByType<ArenaCamera>();

        if (arenaCamera != null && leftPanel != null && rightPanel != null)
        {
            arenaCamera.SetSidePanels(leftPanel.anchorMax.x, 1f - rightPanel.anchorMin.x);
        }
    }

    void Update()
    {
        if (ScreenInsets.SafeArea != lastSafeArea || Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y)
        {
            UpdateSafeArea();
        }

        MatchManager match = MatchManager.Instance;

        // lokalny gracz to slot 0 (boty zajmują kolejne sloty)
        if (localPickups == null && match != null && match.Slots[0] != null)
        {
            localPickups = match.Slots[0].pickups;
        }

        // po zmianie skina w menu buźki na kartach trzeba narysować od nowa
        if (SkinSelection.Version != shownSkinVersion)
        {
            shownSkinVersion = SkinSelection.Version;
            Array.Fill(shownState, -1);
        }

        UpdatePlayers(match);
        UpdateCrown(match);
        UpdateBuff();
        UpdatePassiveSlots();
        UpdateAction();
        UpdateEmotes();
    }

    // Pokazuje dymek emotki obok buźki gracza o danym slocie (0 = lokalny gracz) na kilka sekund.
    // Ikona i tekst są opcjonalne. Wołaj to, gdy gracz wyśle emotkę (później także z sieci).
    public void ShowEmote(int slot, Sprite icon, string text = null, float seconds = 2.5f)
    {
        if (slot < 0 || slot >= playerSlots.Length || playerSlots[slot] == null || playerSlots[slot].emoteBubble == null)
        {
            return;
        }

        if (emoteUntil == null)
        {
            emoteUntil = new float[playerSlots.Length];
        }

        PlayerSlot target = playerSlots[slot];

        if (target.emoteIcon != null)
        {
            target.emoteIcon.sprite = icon;
            target.emoteIcon.enabled = icon != null;
        }

        if (target.emoteText != null)
        {
            target.emoteText.text = text ?? "";
        }

        target.emoteBubble.SetActive(true);
        emoteUntil[slot] = Time.unscaledTime + seconds;
    }

    [ContextMenu("Test emotki (gracz 1)")]
    void TestEmote()
    {
        ShowEmote(0, null, "GG!");
    }

    void UpdateEmotes()
    {
        if (emoteUntil == null)
        {
            return;
        }

        for (int i = 0; i < playerSlots.Length; i++)
        {
            PlayerSlot slot = playerSlots[i];

            if (slot != null && slot.emoteBubble != null && slot.emoteBubble.activeSelf && Time.unscaledTime >= emoteUntil[i])
            {
                slot.emoteBubble.SetActive(false);
            }
        }
    }

    // Każdy gracz dostaje koronę (wyłączoną) przy buźce, jeśli designer nie dodał własnej.
    void EnsureCrowns()
    {
        foreach (PlayerSlot slot in playerSlots)
        {
            if (slot == null || slot.face == null || slot.crown != null)
            {
                continue;
            }

            GameObject crownObject = new GameObject("Crown", typeof(RectTransform), typeof(Image));
            RectTransform rect = crownObject.GetComponent<RectTransform>();
            rect.SetParent(slot.face.rectTransform, false);
            // korona siedzi na czubku główki: wyśrodkowana nad buźką, dolna krawędź lekko zachodzi na głowę
            rect.anchorMin = new Vector2(0.22f, 0.68f);
            rect.anchorMax = new Vector2(0.78f, 1.14f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = crownObject.GetComponent<Image>();
            image.sprite = crownSprite != null ? crownSprite : PickupArt.Crown();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            slot.crown = image;
        }
    }

    // Lider to jedyny gracz z największą, dodatnią liczbą punktów. Remis na prowadzeniu: nikt nie ma korony.
    int FindLeader(MatchManager match)
    {
        int leader = -1;
        int best = 0;
        bool tie = false;

        for (int i = 0; i < playerSlots.Length && match != null && i < match.Slots.Length; i++)
        {
            if (match.Slots[i] == null)
            {
                continue;
            }

            int score = MatchManager.GetScore(i);

            if (score > best)
            {
                best = score;
                leader = i;
                tie = false;
            }
            else if (score == best && best > 0)
            {
                tie = true;
            }
        }

        return tie ? -1 : leader;
    }

    void UpdateCrown(MatchManager match)
    {
        int leader = FindLeader(match);

        if (leader == shownLeader)
        {
            return;
        }

        shownLeader = leader;

        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] != null && playerSlots[i].crown != null)
            {
                playerSlots[i].crown.enabled = i == leader;
            }
        }
    }

    void UpdateSafeArea()
    {
        lastSafeArea = ScreenInsets.SafeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        if (safeArea == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        Vector2 screen = new Vector2(Screen.width, Screen.height);
        safeArea.anchorMin = lastSafeArea.position / screen;
        safeArea.anchorMax = (lastSafeArea.position + lastSafeArea.size) / screen;

        AdaptPanels(lastSafeArea);
        UpdateMapFrame();
    }

    ArenaCamera arenaCameraCache;

    // Panele boczne dostają całe wolne miejsce obok planszy (plansza ma stałe proporcje), ale nie mniej i nie więcej niż
    // ustawione granice. Dzięki temu na szerokim ekranie HUD wypełnia boki, a na wąskim się nie zgniata.
    // Kamera dostaje te same szerokości, żeby plansza stała dokładnie między panelami.
    void AdaptPanels(Rect safe)
    {
        if (!adaptPanelWidth || leftPanel == null || rightPanel == null || safe.width <= 0f || safe.height <= 0f)
        {
            return;
        }

        if (arenaCameraCache == null)
        {
            arenaCameraCache = FindAnyObjectByType<ArenaCamera>();
        }

        float aspect = arenaCameraCache != null ? arenaCameraCache.WorldAspect : GridMap.Width / (float)GridMap.Height;
        float freeWidth = safe.width - safe.height * aspect;
        float panel = Mathf.Clamp(freeWidth * 0.5f, safe.height * minPanelWidth, safe.height * maxPanelWidth);
        float fraction = Mathf.Min(0.45f, panel / safe.width);

        SetAnchorsX(leftPanel, 0f, fraction);
        SetAnchorsX(rightPanel, 1f - fraction, 1f);

        if (arenaCameraCache != null)
        {
            arenaCameraCache.SetSidePanels(fraction, fraction);
        }
    }

    void UpdatePlayers(MatchManager match)
    {
        for (int i = 0; i < playerSlots.Length; i++)
        {
            PlayerSlot slot = playerSlots[i];

            if (slot == null || slot.face == null)
            {
                continue;
            }

            Combatant combatant = match != null && i < match.Slots.Length ? match.Slots[i] : null;
            int state = combatant == null ? 0 : combatant.IsAlive ? 1 : 2;
            int score = MatchManager.GetScore(i);

            if (state == shownState[i] && score == shownScore[i])
            {
                continue;
            }

            shownState[i] = state;
            shownScore[i] = score;

            Color color = match != null && i < match.slotColors.Length ? match.slotColors[i]
                : i < playerColors.Length ? playerColors[i] : Color.white;

            // własna buźka gracza z GameArt nie jest barwiona kolorem slotu, bez niej jest biała buźka w kolorze slotu
            GameArt art = GameArt.Instance;
            PlayerSprites sprites = art != null ? art.PlayerFor(i) : null;
            Sprite portrait = sprites != null ? sprites.Portrait : null;
            Sprite portraitDead = sprites != null ? sprites.PortraitDead : null;
            Sprite alive = portrait != null ? portrait : faceAlive;
            Sprite dead = portraitDead != null ? portraitDead : (portrait != null ? portrait : faceDead);
            Color aliveColor = portrait != null ? Color.white : color;
            Color deadFaceColor = portraitDead != null || portrait != null ? Color.white : color;

            if (state == 0)
            {
                // pusty slot: nikt nie gra tym kolorem
                aliveColor.a = emptySlotAlpha;
                SetSlot(slot, alive, aliveColor, new Color(cardColor.r, cardColor.g, cardColor.b, 0.5f), "", textColor);
            }
            else if (state == 2)
            {
                SetSlot(slot, dead, deadFaceColor, cardColor, "DEAD  " + score, deadColor);
            }
            else
            {
                SetSlot(slot, alive, aliveColor, cardColor, MatchManager.NameOf(combatant) + "  " + score, textColor);
            }
        }
    }

    static void SetSlot(PlayerSlot slot, Sprite face, Color faceColor, Color background, string label, Color labelColor)
    {
        slot.face.sprite = face;
        slot.face.color = faceColor;

        if (slot.background != null)
        {
            slot.background.color = background;
        }

        if (slot.label != null)
        {
            slot.label.text = label;
            slot.label.color = labelColor;
        }
    }

    void UpdateBuff()
    {
        PickupDefinition shown = null;
        PickupDefinition second = null;
        int count = 0;
        int secondCount = 0;

        // slot Buff: pierwszy przedmiot jest duży, drugi (tylko w trybie CHAOS) mały
        if (localPickups != null)
        {
            localPickups.TryGetOccupant(0, out shown, out count);
            localPickups.TryGetOccupant(1, out second, out secondCount);
        }

        UpdateSecondBuff(second, secondCount);

        if (shown == shownBuff && count == shownCount)
        {
            return;
        }

        shownBuff = shown;
        shownCount = count;

        Sprite icon = shown != null ? shown.Icon : null;

        if (buffIcon != null)
        {
            buffIcon.sprite = icon;
            buffIcon.enabled = icon != null;
        }

        if (buffEmpty != null) buffEmpty.SetActive(shown == null);
        if (buffName != null) buffName.text = shown != null ? shown.displayName : "";
        if (buffCount != null) buffCount.text = count < 0 ? -count + "s" : count > 1 ? "x" + count : "";
    }

    // ---------- Małe sloty pod Buffem ----------

    class PassiveSlot
    {
        public Image background;
        public Image icon;
        public TMP_Text count;
        // wartość slotu (0 = slot pusty, przygaszony) i sposób zamiany jej na napis
        public Func<int> read;
        public Func<int, string> format;
        public int shown = int.MinValue;
    }

    // Pod dużym slotem Buff (bomba, młotek...) dokładamy dwa małe sloty obok siebie: Tarcza i Buty (prędkość).
    // To jedyne przedmioty, które się kumulują i nie zajmują slotu Buff. Duży Buff i przycisk akcji robią się
    // odrobinę niższe, żeby zrobić im miejsce. Wygląd paneli kopiujemy z Buffa.
    void CreatePassiveSlots()
    {
        RectTransform buffPanel = buffIcon != null ? buffIcon.transform.parent as RectTransform : null;
        RectTransform side = buffPanel != null ? buffPanel.parent as RectTransform : null;
        RectTransform actionPanel = actionIcon != null ? actionIcon.transform.parent as RectTransform : null;

        if (buffPanel == null || side == null)
        {
            return;
        }

        const float gap = 0.015f;
        float height = passiveSlotsHeight;
        float actionTop = actionPanel != null ? actionPanel.anchorMax.y : buffPanel.anchorMin.y - 0.08f;

        // brakujące miejsce zabieramy po równo od przycisku akcji (od góry) i od Buffa (od dołu)
        float extra = Mathf.Max(0f, height + 2f * gap - (buffPanel.anchorMin.y - actionTop));
        float newActionTop = actionTop - (actionPanel != null ? extra * 0.5f : 0f);
        float newBuffBottom = buffPanel.anchorMin.y + (actionPanel != null ? extra * 0.5f : extra);

        if (actionPanel != null)
        {
            Vector2 max = actionPanel.anchorMax;
            max.y = newActionTop;
            actionPanel.anchorMax = max;
        }

        Vector2 min = buffPanel.anchorMin;
        min.y = newBuffBottom;
        buffPanel.anchorMin = min;

        float stripBottom = newActionTop + gap;
        float stripTop = newBuffBottom - gap;

        RectTransform strip = new GameObject("PassiveSlots", typeof(RectTransform)).GetComponent<RectTransform>();
        strip.SetParent(side, false);
        strip.anchorMin = new Vector2(buffPanel.anchorMin.x, stripBottom);
        strip.anchorMax = new Vector2(buffPanel.anchorMax.x, stripTop);
        strip.offsetMin = Vector2.zero;
        strip.offsetMax = Vector2.zero;

        Image look = buffPanel.GetComponent<Image>();

        AddPassive(strip, look, 0, PickupIcon.Shield,
            () => localPickups != null ? localPickups.ShieldCharges : 0, n => "x" + n);
        AddPassive(strip, look, 1, PickupIcon.Speed,
            () => localStats != null ? Mathf.Max(0, localStats.SpeedLevel) : 0, n => "x" + n);
    }

    void AddPassive(RectTransform strip, Image look, int index, PickupIcon iconType, Func<int> read, Func<int, string> format)
    {
        const int columns = 2;
        const int rows = 1;
        const float gapX = 0.03f;
        const float gapY = 0.0f;

        int column = index % columns;
        int row = index / columns;

        float width = (1f - gapX * (columns - 1)) / columns;
        float height = (1f - gapY * (rows - 1)) / rows;
        float xMin = column * (width + gapX);
        float yMax = 1f - row * (height + gapY);

        PassiveSlot slot = new PassiveSlot { read = read, format = format };

        RectTransform cell = new GameObject("Slot_" + iconType, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        cell.SetParent(strip, false);
        cell.anchorMin = new Vector2(xMin, yMax - height);
        cell.anchorMax = new Vector2(xMin + width, yMax);
        cell.offsetMin = Vector2.zero;
        cell.offsetMax = Vector2.zero;

        slot.background = cell.GetComponent<Image>();
        slot.background.raycastTarget = false;

        if (look != null)
        {
            slot.background.sprite = look.sprite;
            slot.background.type = look.type;
            slot.background.color = look.color;
        }

        RectTransform iconRect = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        iconRect.SetParent(cell, false);
        iconRect.anchorMin = new Vector2(0.06f, 0.08f);
        iconRect.anchorMax = new Vector2(0.46f, 0.92f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        slot.icon = iconRect.GetComponent<Image>();
        slot.icon.preserveAspect = true;
        slot.icon.raycastTarget = false;

        GameArt art = GameArt.Instance;
        Sprite sprite = art != null ? art.IconFor(iconType) : null;
        slot.icon.sprite = sprite != null ? sprite : PickupArt.Icon(iconType);

        RectTransform countRect = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
        countRect.SetParent(cell, false);
        countRect.anchorMin = new Vector2(0.46f, 0.04f);
        countRect.anchorMax = new Vector2(0.98f, 0.96f);
        countRect.offsetMin = Vector2.zero;
        countRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = countRect.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 60f;
        text.color = new Color(0.15f, 0.12f, 0.15f);
        text.raycastTarget = false;
        slot.count = text;

        passives.Add(slot);
    }

    void UpdatePassiveSlots()
    {
        if (passives.Count == 0 || localPickups == null)
        {
            return;
        }

        if (localStats == null)
        {
            localStats = localPickups.GetComponent<PlayerStats>();
        }

        foreach (PassiveSlot slot in passives)
        {
            int value = slot.read();

            if (value == slot.shown)
            {
                continue;
            }

            slot.shown = value;

            // slot z zerem jest przygaszony, z wartością pokazuje ją obok ikony
            bool active = value != 0;
            slot.count.text = slot.format(value);
            slot.icon.color = active ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            slot.count.alpha = active ? 1f : 0.4f;
        }
    }


    // ---------- Drugi przedmiot w slocie Buff (tryb CHAOS: DOUBLE BUFFS) ----------

    Image secondIcon;
    TMP_Text secondCount;
    PickupDefinition shownSecond;
    int shownSecondCount = int.MinValue;

    // W trybie CHAOS slot Buff mieści dwa przedmioty. Drugi pokazujemy jako małą ikonę w lewym dolnym rogu panelu.
    void CreateSecondBuffSlot()
    {
        RectTransform buffPanel = buffIcon != null ? buffIcon.transform.parent as RectTransform : null;

        if (buffPanel == null)
        {
            return;
        }

        RectTransform iconRect = new GameObject("SecondIcon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        iconRect.SetParent(buffPanel, false);
        iconRect.anchorMin = new Vector2(0.04f, 0.20f);
        iconRect.anchorMax = new Vector2(0.26f, 0.42f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        secondIcon = iconRect.GetComponent<Image>();
        secondIcon.preserveAspect = true;
        secondIcon.raycastTarget = false;
        secondIcon.enabled = false;

        RectTransform countRect = new GameObject("SecondCount", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
        countRect.SetParent(buffPanel, false);
        countRect.anchorMin = new Vector2(0.26f, 0.20f);
        countRect.anchorMax = new Vector2(0.55f, 0.42f);
        countRect.offsetMin = Vector2.zero;
        countRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = countRect.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 32f;
        text.color = new Color(0.15f, 0.12f, 0.15f);
        text.raycastTarget = false;
        secondCount = text;
    }

    void UpdateSecondBuff(PickupDefinition second, int count)
    {
        if (secondIcon == null || (second == shownSecond && count == shownSecondCount))
        {
            return;
        }

        shownSecond = second;
        shownSecondCount = count;

        Sprite icon = second != null ? second.Icon : null;
        secondIcon.sprite = icon;
        secondIcon.enabled = icon != null;
        secondCount.text = second == null ? "" : count < 0 ? -count + "s" : count > 1 ? "x" + count : "";
    }

    // ---------- Ramka i panele (Canvas) ----------

    const string BackdropName = "Backdrop";
    const string MapFrameName = "MapFrame";

    Image leftBackdrop;
    Image rightBackdrop;
    Image mapFrameImage;

    // Tworzy brakujące obiekty nakładki HUD: tło paneli bocznych (Backdrop) i ramkę wokół okna z planszą (MapFrame).
    // Obiekty ze sceny są wykorzystywane, a brakujące powstają w locie. Narzędzie w GameArt (Narzędzia) tworzy je w Hierarchii,
    // żeby grafik mógł je oglądać i edytować. Wygląd (sprite'y, kolory, grubość) bierze się z GameArt.
    public void BuildFrameObjects()
    {
        leftBackdrop = EnsureFrameImage(leftPanel, BackdropName);
        rightBackdrop = EnsureFrameImage(rightPanel, BackdropName);
        mapFrameImage = EnsureFrameImage(safeArea, MapFrameName);

        if (mapFrameImage != null)
        {
            mapFrameImage.fillCenter = false;
            mapFrameImage.raycastTarget = false;
        }

        ApplyFrameLook();
        UpdateMapFrame();
    }

    static Image EnsureFrameImage(RectTransform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform found = parent.Find(objectName);
        Image image = found != null ? found.GetComponent<Image>() : null;

        if (image == null)
        {
            image = UiKit.NewImage(objectName, parent, Color.white);
            UiKit.Stretch(image.rectTransform);

            // za wszystkim innym w panelu
            image.transform.SetAsFirstSibling();
        }

        return image;
    }

    // Sprite'y, kolory i grubość ramki z GameArt (sekcja "Ramka i panele HUD").
    void ApplyFrameLook()
    {
        GameArt art = GameArt.Instance;

        Color panelColor = art != null ? art.hudPanelColor : new Color(0.48f, 0.18f, 0.33f, 1f);
        Color frameColor = art != null ? art.hudFrameColor : new Color(0.40f, 0.20f, 0.13f, 1f);
        float thickness = art != null ? art.hudFrameThickness : 28f;

        foreach (Image backdrop in new[] { leftBackdrop, rightBackdrop })
        {
            if (backdrop == null)
            {
                continue;
            }

            if (art != null && art.hudPanelSprite != null)
            {
                backdrop.sprite = art.hudPanelSprite;
            }
            else if (backdrop.sprite == null)
            {
                backdrop.sprite = UiKit.RoundedSprite();
            }

            backdrop.type = Image.Type.Sliced;
            backdrop.color = panelColor;
            backdrop.raycastTarget = true;
        }

        if (mapFrameImage != null)
        {
            bool custom = art != null && art.hudMapFrameSprite != null;

            if (custom)
            {
                mapFrameImage.sprite = art.hudMapFrameSprite;
            }
            else if (mapFrameImage.sprite == null)
            {
                mapFrameImage.sprite = UiKit.RoundedSprite();
            }

            mapFrameImage.type = Image.Type.Sliced;
            mapFrameImage.color = frameColor;

            // prosta ramka z kodu: grubość regulowana mnożnikiem (obramowanie sprite'a to RoundedRadius jednostek)
            mapFrameImage.pixelsPerUnitMultiplier = custom ? 1f : UiKit.RoundedRadius / Mathf.Max(4f, thickness);
        }
    }

    // Ramka stoi dokładnie na oknie, które rysuje kamera (między panelami bocznymi, na całą wysokość bezpiecznego obszaru).
    void UpdateMapFrame()
    {
        if (mapFrameImage == null || leftPanel == null || rightPanel == null)
        {
            return;
        }

        RectTransform rect = mapFrameImage.rectTransform;
        rect.anchorMin = new Vector2(leftPanel.anchorMax.x, 0f);
        rect.anchorMax = new Vector2(rightPanel.anchorMin.x, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ---------- Zębatka (pauza) ----------

    // Obok CAM i EMOTKI dokładamy małą zębatkę, która otwiera menu pauzy: Resume, Settings, Quit.
    // Trzy przyciski w rzędzie dzielą szerokość panelu, a wygląd zębatki kopiujemy z przycisku CAM.
    void CreateGearButton()
    {
        CameraToggleButton cam = FindAnyObjectByType<CameraToggleButton>();
        EmotePicker emote = FindAnyObjectByType<EmotePicker>();
        RectTransform camRect = cam != null ? cam.transform as RectTransform : null;
        RectTransform emoteRect = emote != null && emote.openButton != null ? emote.openButton.transform as RectTransform : null;

        if (camRect == null || emoteRect == null || camRect.parent != emoteRect.parent)
        {
            return;
        }

        SetAnchorsX(camRect, 0.06f, 0.34f);
        SetAnchorsX(emoteRect, 0.36f, 0.64f);

        Image camImage = cam.background != null ? cam.background : camRect.GetComponent<Image>();

        Button gear = UiKit.NewButton(camRect.parent, "", cam.offColor, MainMenu.OpenPause, 40f);
        gear.name = "GearButton";
        RectTransform gearRect = (RectTransform)gear.transform;
        gearRect.anchorMin = new Vector2(0.66f, camRect.anchorMin.y);
        gearRect.anchorMax = new Vector2(0.94f, camRect.anchorMax.y);
        gearRect.offsetMin = camRect.offsetMin;
        gearRect.offsetMax = camRect.offsetMax;

        Image gearImage = gear.GetComponent<Image>();

        if (camImage != null && camImage.sprite != null)
        {
            gearImage.type = camImage.type;
            gear.GetComponent<ButtonFx>().SetSprites(camImage.sprite, null, null);
        }

        Image icon = UiKit.NewImage("GearIcon", gear.transform, new Color(0.15f, 0.12f, 0.15f), false);
        UiKit.Anchor(icon.rectTransform, 0.18f, 0.18f, 0.82f, 0.82f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        GameArt art = GameArt.Instance;
        icon.sprite = art != null && art.uiGear != null ? art.uiGear : UiKit.GearSprite();

        // własna grafika zębatki z GameArt jest rysowana w swoich kolorach
        if (art != null && art.uiGear != null)
        {
            icon.color = Color.white;
        }
    }

    static void SetAnchorsX(RectTransform rect, float xMin, float xMax)
    {
        rect.anchorMin = new Vector2(xMin, rect.anchorMin.y);
        rect.anchorMax = new Vector2(xMax, rect.anchorMax.y);
    }

    // Podpis pod ikoną na przycisku akcji (domyślnie SPACE). Z atomówką w slocie zmienia się na ATOM.
    TMP_Text actionLabelTmp;
    Text actionLabelLegacy;
    string actionLabelDefault;
    bool actionLabelSearched;

    void FindActionLabel()
    {
        actionLabelSearched = true;

        Transform panel = actionIcon != null ? actionIcon.transform.parent : null;

        if (panel == null)
        {
            return;
        }

        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.text.Trim().ToUpperInvariant() == "SPACE")
            {
                actionLabelTmp = text;
                actionLabelDefault = text.text;
                return;
            }
        }

        foreach (Text text in panel.GetComponentsInChildren<Text>(true))
        {
            if (text.text.Trim().ToUpperInvariant() == "SPACE")
            {
                actionLabelLegacy = text;
                actionLabelDefault = text.text;
                return;
            }
        }
    }

    // atom = true: napis ATOM (i widoczny nawet na telefonie, gdzie podpowiedź klawisza normalnie jest ukryta).
    void SetActionNuke(bool atom)
    {
        if (!actionLabelSearched)
        {
            FindActionLabel();
        }

        string label = atom ? "ATOM" : actionLabelDefault;

        if (actionLabelTmp != null)
        {
            actionLabelTmp.text = label;
        }
        else if (actionLabelLegacy != null)
        {
            actionLabelLegacy.text = label;
        }

        ActionButton button = actionIcon != null ? actionIcon.GetComponentInParent<ActionButton>() : null;

        if (button != null && button.keyHint != null)
        {
            button.keyHint.SetActive(atom || !Application.isMobilePlatform);
        }
    }

    void UpdateAction()
    {
        PickupDefinition held = localPickups != null && localPickups.HeldUses > 0 ? localPickups.HeldItem : null;

        if (held == shownAction)
        {
            return;
        }

        shownAction = held;

        bool special = held != null && held.special != SpecialItem.None;

        if (actionIcon != null)
        {
            actionIcon.sprite = special ? held.Icon : bombIcon;
        }

        SetActionNuke(held != null && held.special == SpecialItem.Nuke);

        if (actionBadge == null)
        {
            return;
        }

        actionBadge.text = "";

        if (held != null && !special)
        {
            if (held.extraRange != 0) actionBadge.text = (held.extraRange > 0 ? "+" : "") + held.extraRange;
            else if (held.extraBombs > 0) actionBadge.text = "+1";
        }
    }
}