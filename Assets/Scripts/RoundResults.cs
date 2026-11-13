using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Okienko z wynikami na koniec rundy (jak w Ultimate Chicken Horse): każdy gracz w jednym rzędzie z buźką, nickiem,
// zabiciami w tej rundzie, wygranymi rundami i monetami zdobytymi w tej rundzie. Na dole: ile monet mam razem.
// Ma własny Canvas (znika razem ze sceną), więc nie wymaga zmian w HUD.
public class RoundResults : MonoBehaviour
{
    const float PanelWidth = 1040f;
    const float RowHeight = 78f;
    const float HeaderHeight = 84f;
    const float FooterHeight = 70f;

    static readonly Color Gold = new Color(1f, 0.85f, 0.25f);
    static readonly Color PanelColor = new Color(0.20f, 0.07f, 0.15f, 0.93f);

    public static void Show(MatchManager match, Combatant winner)
    {
        GameObject root = new GameObject("RoundResults");
        root.AddComponent<RoundResults>().Build(match, winner);
    }

    float age;
    RectTransform panel;

    void Build(MatchManager match, Combatant winner)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        int rows = 0;

        foreach (Combatant combatant in match.Slots)
        {
            if (combatant != null && !combatant.left)
            {
                rows++;
            }
        }

        float height = HeaderHeight + rows * RowHeight + FooterHeight;

        // pod napisem z wynikiem rundy (ten jest w środku ekranu)
        panel = NewRect("Panel", transform);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -60f);
        panel.sizeDelta = new Vector2(PanelWidth, height);
        NewImage("Background", panel, PanelColor).rectTransform.anchorMax = Vector2.one;

        AddHeader(panel);

        float y = -HeaderHeight;

        foreach (Combatant combatant in match.Slots)
        {
            if (combatant == null || combatant.left)
            {
                continue;
            }

            AddRow(match, panel, combatant, combatant == winner, y);
            y -= RowHeight;
        }

        TextMeshProUGUI total = NewText("Total", panel, Loc.F("board.total", PlayerWallet.Format(PlayerWallet.Coins)), 34f, Gold, TextAlignmentOptions.Center);
        total.rectTransform.anchorMin = new Vector2(0f, 0f);
        total.rectTransform.anchorMax = new Vector2(1f, 0f);
        total.rectTransform.pivot = new Vector2(0.5f, 0f);
        total.rectTransform.sizeDelta = new Vector2(0f, FooterHeight);
        total.rectTransform.anchoredPosition = Vector2.zero;

        panel.localScale = Vector3.zero;
    }

    void AddHeader(RectTransform parent)
    {
        TextMeshProUGUI title = NewText("Title", parent, Loc.T("board.title"), 44f, Color.white, TextAlignmentOptions.Center);
        Place(title.rectTransform, 0f, -8f, PanelWidth, 50f);

        Color grey = new Color(1f, 1f, 1f, 0.6f);
        Place(NewText("HKills", parent, Loc.T("board.kills"), 24f, grey, TextAlignmentOptions.Center).rectTransform, 500f, -58f, 170f, 26f);
        Place(NewText("HWins", parent, Loc.T("board.wins"), 24f, grey, TextAlignmentOptions.Center).rectTransform, 680f, -58f, 170f, 26f);
        Place(NewText("HCoins", parent, Loc.T("board.coins"), 24f, grey, TextAlignmentOptions.Center).rectTransform, 860f, -58f, 170f, 26f);
    }

    void AddRow(MatchManager match, RectTransform parent, Combatant combatant, bool isWinner, float y)
    {
        Color slotColor = match.slotColors[Mathf.Clamp(combatant.slot, 0, match.slotColors.Length - 1)];

        RectTransform row = NewRect("Row_" + combatant.slot, parent);
        Place(row, 12f, y, PanelWidth - 24f, RowHeight - 8f);
        Image background = NewImage("Background", row, isWinner ? new Color(Gold.r, Gold.g, Gold.b, 0.40f) : new Color(slotColor.r, slotColor.g, slotColor.b, 0.22f));
        background.rectTransform.anchorMax = Vector2.one;

        // buźka
        GameArt art = GameArt.Instance;
        PlayerSprites look = art != null ? art.PlayerFor(combatant.slot) : null;
        Sprite portrait = look != null ? (combatant.IsAlive ? look.Portrait : look.PortraitDead ?? look.Portrait) : null;

        Image face = NewImage("Face", row, portrait != null ? Color.white : slotColor);
        face.sprite = portrait;
        face.preserveAspect = true;
        Place(face.rectTransform, 10f, -3f, 62f, 62f);

        string name = MatchManager.NameOf(combatant);
        TextMeshProUGUI label = NewText("Name", row, name, 38f, isWinner ? Gold : Color.white, TextAlignmentOptions.MidlineLeft);
        label.overflowMode = TextOverflowModes.Ellipsis;
        Place(label.rectTransform, 90f, 0f, 380f, RowHeight - 8f);

        int coins = PlayerWallet.RoundReward + combatant.humanKills * PlayerWallet.PlayerKillReward + (isWinner && !combatant.isBot ? PlayerWallet.WinReward : 0);

        Place(NewText("Kills", row, combatant.humanKills.ToString(), 40f, Color.white, TextAlignmentOptions.Center).rectTransform, 488f, 0f, 170f, RowHeight - 8f);
        Place(NewText("Wins", row, MatchManager.GetScore(combatant.slot).ToString(), 40f, Color.white, TextAlignmentOptions.Center).rectTransform, 668f, 0f, 170f, RowHeight - 8f);
        Place(NewText("Coins", row, combatant.isBot ? "-" : "+" + coins, 40f, combatant.isBot ? new Color(1f, 1f, 1f, 0.5f) : Gold, TextAlignmentOptions.Center).rectTransform, 848f, 0f, 170f, RowHeight - 8f);
    }

    void Update()
    {
        // wskakuje z lekkim przeskokiem
        age += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(age / 0.35f);
        float scale = k < 1f ? Mathf.Lerp(0f, 1.08f, k) : Mathf.Lerp(1.08f, 1f, Mathf.Clamp01((age - 0.35f) / 0.15f));
        panel.localScale = Vector3.one * scale;
    }

    // ---------- Małe pomocniki UI ----------

    static RectTransform NewRect(string objectName, Transform parent)
    {
        RectTransform rect = (RectTransform)new GameObject(objectName, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }

    // Pozycja od lewej-górnej krawędzi rodzica (x w prawo, y w dół jako wartość ujemna).
    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    static Image NewImage(string objectName, Transform parent, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return image;
    }

    static TextMeshProUGUI NewText(string objectName, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        label.outlineWidth = 0.2f;
        label.outlineColor = new Color32(45, 20, 35, 255);
        return label;
    }
}
