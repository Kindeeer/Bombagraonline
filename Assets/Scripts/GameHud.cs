using System;
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

    [Header("Przycisk akcji (Spacja)")]
    public Image actionIcon;
    public TMP_Text actionBadge;
    public Sprite bombIcon;

    PlayerPickups localPickups;

    // ostatnio pokazany stan slotów (0 pusty, 1 żyje, 2 martwy) i wynik, żeby nie budować napisów co klatkę
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
        ApplyArt();

        shownState = new int[playerSlots.Length];
        shownScore = new int[playerSlots.Length];
        Array.Fill(shownState, -1);
        emoteUntil = new float[playerSlots.Length];
        EnsureCrowns();

        UpdateSafeArea();

        ArenaCamera arenaCamera = FindAnyObjectByType<ArenaCamera>();

        if (arenaCamera != null && leftPanel != null && rightPanel != null)
        {
            arenaCamera.SetSidePanels(leftPanel.anchorMax.x, 1f - rightPanel.anchorMin.x);
        }
    }

    void Update()
    {
        if (Screen.safeArea != lastSafeArea || Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y)
        {
            UpdateSafeArea();
        }

        MatchManager match = MatchManager.Instance;

        // lokalny gracz to slot 0 (boty zajmują kolejne sloty)
        if (localPickups == null && match != null && match.Slots[0] != null)
        {
            localPickups = match.Slots[0].pickups;
        }

        UpdatePlayers(match);
        UpdateCrown(match);
        UpdateBuff();
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
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        if (safeArea == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        Vector2 screen = new Vector2(Screen.width, Screen.height);
        safeArea.anchorMin = lastSafeArea.position / screen;
        safeArea.anchorMax = (lastSafeArea.position + lastSafeArea.size) / screen;
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
        int count = 0;

        if (localPickups != null && localPickups.HeldItem != null && localPickups.HeldUses > 0)
        {
            shown = localPickups.HeldItem;
            count = localPickups.HeldUses;
        }
        else if (localPickups != null && localPickups.TryGetTimedBuff(out PickupDefinition timed, out float secondsLeft))
        {
            shown = timed;
            count = -Mathf.CeilToInt(secondsLeft); // ujemne = sekundy
        }

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