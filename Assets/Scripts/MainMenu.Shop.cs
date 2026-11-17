using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiKit;

// Sklep i edycja skinów w menu głównym (reszta menu jest w MainMenu.cs).
//
// - Strona Shop: skrzynka za monety (PlayerWallet.ChestPrice), po jej otwarciu losuje się chroma skina, której jeszcze nie masz.
// - Otwieranie skrzynki to animacja na całym ekranie: trzęsienie, błysk, uniesienie wieczka, konfetti i karta z wylosowaną chromą.
//   Każdy etap ma dźwięk (AudioEvent.Chest*), rzadsza chroma ma inny dźwięk, więcej konfetti i bardziej błyszczące promienie.
// - Strona Edit (przycisk EDIT na stronie Skins): chromy wybranego skina. Zdobyte można zakładać, reszta jest zablokowana.
// Strony powstają w kodzie, więc działają zarówno z menu zbudowanym w Hierarchii, jak i z menu budowanym w locie.
public partial class MainMenu
{
    static readonly Color ChestBodyColor = new Color(0.52f, 0.28f, 0.13f);
    static readonly Color ChestLidColor = new Color(0.66f, 0.37f, 0.17f);
    static readonly Color ChestGoldColor = new Color(0.98f, 0.78f, 0.22f);
    static readonly Color EquippedColor = new Color(0.62f, 0.92f, 0.62f, 1f);

    class ChestView
    {
        public RectTransform root;
        public RectTransform lid;
        public Image glow;
    }

    class Particle
    {
        public RectTransform rect;
        public Image image;
        public Vector2 velocity;
        public float spin;
        public float life;
        public float maxLife;
        public float gravity;
        public Color color;
    }

    class ChestOverlay
    {
        public RectTransform root;
        public Image dim;
        public Image rays;
        public ChestView chest;
        public RectTransform sparks;
        public Image flash;
        public RectTransform card;
        public Image cardBorder;
        public Image cardPreview;
        public TextMeshProUGUI cardName;
        public TextMeshProUGUI cardRarity;
        public RectTransform buttons;
    }

    bool chestBusy;
    bool shopIdleRunning;
    bool resultChosen;
    bool equipAfterOpen;

    TextMeshProUGUI shopCoinText;
    TextMeshProUGUI collectionText;
    TextMeshProUGUI chancesText;
    TextMeshProUGUI chestTitleText;
    TextMeshProUGUI hintText;
    Image openChestImage;
    TextMeshProUGUI openChestLabel;
    ChestView shopChest;
    ChestOverlay overlay;

    TextMeshProUGUI editTitle;
    TextMeshProUGUI editRarity;
    Image editPreview;
    RectTransform editRow;

    readonly List<Particle> particles = new List<Particle>();
    static Sprite raysSprite;
    static Sprite raysOverride;

    // Edytor zapisuje promienie jako plik PNG, bo obiekty w scenie nie mogą wskazywać sprite'ów z pamięci.
    public static void SetShopSpriteOverrides(Sprite rays)
    {
        raysOverride = rays;
    }

    public static Texture2D CreateRaysTexture()
    {
        return RaysSprite().texture;
    }

    // ---------- Budowa stron ----------

#if UNITY_EDITOR
    // Dla edytora: buduje strony skrzynek i edycji w scenie (poza grą), żeby były widoczne i edytowalne w Hierarchii.
    public bool BuildShopInEditor()
    {
        pages.Clear();

        foreach (Page page in Enum.GetValues(typeof(Page)))
        {
            Transform found = FindDeep(transform, "Page_" + page);

            if (found != null)
            {
                pages[page] = (RectTransform)found;
            }
        }

        if (!pages.ContainsKey(Page.Skins))
        {
            return false;
        }

        BuildShop();
        return true;
    }
#endif

    void BuildShop()
    {
        if (!pages.TryGetValue(Page.Skins, out RectTransform skinsPage))
        {
            return;
        }

        Transform parent = skinsPage.parent;

        EnsureShopButton();
        DecorateShopButton();
        EnsureEditButton(skinsPage);

        if (!pages.ContainsKey(Page.Shop))
        {
            BuildShopPage(parent);
        }

        if (!pages.ContainsKey(Page.Edit))
        {
            BuildEditPage(parent);
        }

        BuildChestOverlay(parent);

        // nowe strony są wyłączone, dopóki ich nie otworzymy
        pages[Page.Shop].gameObject.SetActive(false);
        pages[Page.Edit].gameObject.SetActive(false);

        BindShopReferences();
    }

    // Strony skrzynek mogą już być w scenie (zbudowane w edytorze) albo powstać w locie. W obu wypadkach szukamy ich elementów
    // po nazwach, więc obiekty w Hierarchii możesz przesuwać i kolorować, ale nie zmieniaj ich nazw.
    void BindShopReferences()
    {
        RectTransform shop = pages[Page.Shop];
        shopCoinText = shop.Find("Coins/CoinText").GetComponent<TextMeshProUGUI>();
        collectionText = shop.Find("Info/CollectionText").GetComponent<TextMeshProUGUI>();
        Transform open = shop.Find("Info/Button_OpenChest");
        openChestImage = open.GetComponent<Image>();
        openChestLabel = open.GetComponentInChildren<TextMeshProUGUI>();
        shopChest = ViewOf(shop.Find("Chest"));

        // tytuł karty (nazwa wybranej skrzynki), przyciski zmiany skrzynki i przycisk testowych monet
        Transform info = shop.Find("Info");
        chestTitleText = info != null ? info.GetComponentInChildren<TextMeshProUGUI>(true) : null;

        if (chestTitleText != null)
        {
            chestTitleText.enableAutoSizing = true;
            chestTitleText.fontSizeMin = 28f;
            chestTitleText.fontSizeMax = 52f;
        }

        if (shop.Find("ChestPrev") == null)
        {
            CreateChestArrow(shop, "ChestPrev", "<", 0.085f, -1);
            CreateChestArrow(shop, "ChestNext", ">", 0.435f, 1);
        }

        Transform freeButton = shop.Find("Button_FreeCoins");

        if (freeButton != null)
        {
            SetButtonText(freeButton.GetComponent<Button>(), "+500 (TEST)");
        }

        // teksty ustawiane z kodu przy zmianie języka: szanse (zawierają %) i podpowiedź o monetach
        chancesText = null;
        hintText = null;

        foreach (TextMeshProUGUI candidate in shop.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (candidate.name == "ChanceText" || (chancesText == null && candidate.text.Contains("%")))
            {
                chancesText = candidate;
            }
            else if (candidate.name == "HintText" || candidate.text.StartsWith("Za każdą") || candidate.text.StartsWith("You earn"))
            {
                hintText = candidate;
            }
        }

        if (shopChest.glow.sprite == null)
        {
            shopChest.glow.sprite = RaysSprite();
        }

        // przycisk darmowych monet jest tylko do testów
        Transform free = shop.Find("Button_FreeCoins");

        if (free != null)
        {
            free.gameObject.SetActive(Debug.isDebugBuild);
        }

        RectTransform edit = pages[Page.Edit];
        editTitle = edit.Find("Title").GetComponent<TextMeshProUGUI>();
        editPreview = edit.Find("Preview/Image").GetComponent<Image>();
        editRarity = edit.Find("Preview/RarityText").GetComponent<TextMeshProUGUI>();
        editRow = EnsureEditGrid((RectTransform)edit.Find("Chromas"));

        Transform found = FindDeep(transform, "ChestOverlay");
        overlay = ReadOverlay((RectTransform)found);

        if (overlay.rays.sprite == null)
        {
            overlay.rays.sprite = RaysSprite();
        }

        if (overlay.chest.glow.sprite == null)
        {
            overlay.chest.glow.sprite = RaysSprite();
        }
    }

    static ChestView ViewOf(Transform root)
    {
        return new ChestView
        {
            root = (RectTransform)root,
            lid = (RectTransform)root.Find("Lid"),
            glow = root.Find("Glow").GetComponent<Image>()
        };
    }

    ChestOverlay ReadOverlay(RectTransform root)
    {
        return new ChestOverlay
        {
            root = root,
            dim = root.GetComponent<Image>(),
            rays = root.Find("Rays").GetComponent<Image>(),
            chest = ViewOf(root.Find("Chest")),
            sparks = (RectTransform)root.Find("Sparks"),
            flash = root.Find("Flash").GetComponent<Image>(),
            card = (RectTransform)root.Find("ResultCard"),
            cardBorder = root.Find("ResultCard/Border").GetComponent<Image>(),
            cardPreview = root.Find("ResultCard/Preview").GetComponent<Image>(),
            cardName = root.Find("ResultCard/CardName").GetComponent<TextMeshProUGUI>(),
            cardRarity = root.Find("ResultCard/CardRarity").GetComponent<TextMeshProUGUI>(),
            buttons = (RectTransform)root.Find("Buttons")
        };
    }

    // Przycisk SHOP pod SKINS (menu zbudowane wcześniej w Hierarchii go nie ma, więc dokładamy go w locie).
    void EnsureShopButton()
    {
        if (FindDeep(transform, "Button_Shop") != null)
        {
            return;
        }

        Transform skinsButton = FindDeep(transform, "Button_Skins");

        if (skinsButton == null)
        {
            return;
        }

        Transform column = skinsButton.parent;
        Button shop = NewButton(column, "CHESTS", MenuBuilder.PlumColor, null, 64f);
        shop.name = "Button_Shop";
        shop.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
        shop.transform.SetSiblingIndex(skinsButton.GetSiblingIndex() + 1);

        // cztery przyciski muszą się zmieścić pod tytułem i nad dolną krawędzią ekranu, więc robią się niższe
        foreach (LayoutElement element in column.GetComponentsInChildren<LayoutElement>())
        {
            element.preferredHeight = element.name == "Button_Play" ? 170f : 120f;
        }

        VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();

        if (layout != null)
        {
            layout.spacing = 20f;
        }

        RectTransform columnRect = column as RectTransform;

        if (columnRect != null)
        {
            columnRect.sizeDelta = new Vector2(columnRect.sizeDelta.x, 640f);
            columnRect.anchoredPosition = new Vector2(columnRect.anchoredPosition.x, -150f);
        }
    }

    // Przycisk do skrzynek ma napis CHESTS i małą skrzynkę po lewej (bez słowa SHOP, żeby nie kojarzył się z płatnościami).
    void DecorateShopButton()
    {
        Transform found = FindDeep(transform, "Button_Shop");

        if (found == null)
        {
            return;
        }

        SetButtonText(found.GetComponent<Button>(), "CHESTS");

        if (found.Find("ChestIcon") != null)
        {
            return;
        }

        ChestView icon = BuildChest(found, "ChestIcon");
        icon.root.anchorMin = icon.root.anchorMax = new Vector2(0.13f, 0.5f);
        icon.root.anchoredPosition = Vector2.zero;
        icon.root.localScale = Vector3.one * 0.2f;
    }

    // Przycisk EDIT w prawym dolnym rogu strony Skins: chromy aktualnie wybranego skina.
    void EnsureEditButton(RectTransform skinsPage)
    {
        if (FindDeep(transform, "Button_Edit") != null)
        {
            return;
        }

        Button edit = NewButton(skinsPage, "EDIT", MenuBuilder.OrangeColor, null, 52f);
        edit.name = "Button_Edit";
        RectTransform rect = (RectTransform)edit.transform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-60f, 50f);
        rect.sizeDelta = new Vector2(360f, 120f);
    }

    void BuildShopPage(Transform parent)
    {
        RectTransform page = MenuBuilder.NewPage("Page_Shop", parent);
        MenuBuilder.AddTitle(page, "SKRZYNKI", 130f);
        pages[Page.Shop] = page;

        // licznik monet
        Image pill = NewImage("Coins", page, new Color(0.20f, 0.07f, 0.15f, 0.9f));
        Anchor(pill.rectTransform, 0.69f, 0.865f, 0.96f, 0.975f);
        pill.raycastTarget = false;
        AddCoinIcon(pill.transform, 0.08f, 0.12f, 0.26f, 0.88f);
        shopCoinText = NewText(pill.transform, "0", 64f, new Color(1f, 0.9f, 0.45f), TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        shopCoinText.name = "CoinText";
        Anchor(shopCoinText.rectTransform, 0.28f, 0f, 0.94f, 1f);

        // skrzynka po lewej
        shopChest = BuildChest(page, "Chest");
        shopChest.root.anchorMin = shopChest.root.anchorMax = new Vector2(0.26f, 0.47f);

        // karta z opisem po prawej
        Image info = NewImage("Info", page, MenuBuilder.CardColor);
        Anchor(info.rectTransform, 0.52f, 0.20f, 0.95f, 0.76f);
        info.raycastTarget = false;

        TextMeshProUGUI title = NewText(info.transform, "SKRZYNKA Z CHROMAMI", 52f, MenuBuilder.DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        Anchor(title.rectTransform, 0.04f, 0.82f, 0.96f, 0.97f);

        TextMeshProUGUI chances = NewText(info.transform, ChanceLines(Chests.Selected, GameArt.Instance), 38f, MenuBuilder.DarkText, TextAlignmentOptions.Center);
        Anchor(chances.rectTransform, 0.04f, 0.40f, 0.96f, 0.82f);
        chances.richText = true;
        chances.name = "ChanceText";

        collectionText = NewText(info.transform, "", 36f, new Color(0.35f, 0.28f, 0.32f), TextAlignmentOptions.Center);
        collectionText.name = "CollectionText";
        Anchor(collectionText.rectTransform, 0.04f, 0.29f, 0.96f, 0.40f);

        Button open = NewButton(info.transform, "OTWÓRZ", MenuBuilder.OrangeColor, null, 62f);
        open.name = "Button_OpenChest";
        Anchor((RectTransform)open.transform, 0.08f, 0.04f, 0.92f, 0.27f);
        openChestImage = open.GetComponent<Image>();
        openChestLabel = open.GetComponentInChildren<TextMeshProUGUI>();

        TextMeshProUGUI hint = NewText(page, "Za każdą wygraną rundę dostajesz " + PlayerWallet.WinReward + " monet.", 38f,
            MenuBuilder.CardColor, TextAlignmentOptions.Center);
        hint.name = "HintText";
        Anchor(hint.rectTransform, 0.30f, 0.06f, 0.95f, 0.17f);

        // do testów: darmowe monety (w grze widoczny tylko w edytorze i buildach developerskich)
        Button free = NewButton(page, "+100 (TEST)", MenuBuilder.ChocolateColor, null, 40f);
        free.name = "Button_FreeCoins";
        RectTransform rect = (RectTransform)free.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(450f, 60f);
        rect.sizeDelta = new Vector2(300f, 90f);

        MenuBuilder.AddBackButton(page);
    }

    // Opis skrzynki i szanse na kolejne rzadkości (z wag i liczby chrom w jej puli), w jednym tekście z kolorami.
    static string ChanceLines(ChestType chest, GameArt art)
    {
        ChromaRarity[] order = { ChromaRarity.Common, ChromaRarity.Rare, ChromaRarity.Epic, ChromaRarity.Legendary };
        float[] weights = new float[4];
        float total = 0f;

        if (art != null)
        {
            for (int skin = 0; skin < art.SkinCount; skin++)
            {
                foreach (SkinChroma chroma in art.players[skin].ChromaList)
                {
                    if (chest.Accepts(chroma))
                    {
                        weights[(int)chroma.rarity] += chest.Weight(chroma.rarity);
                        total += chest.Weight(chroma.rarity);
                    }
                }
            }
        }

        System.Text.StringBuilder text = new System.Text.StringBuilder(chest.Info + "\n");

        foreach (ChromaRarity rarity in order)
        {
            if (weights[(int)rarity] <= 0f || total <= 0f)
            {
                continue;
            }

            float percent = 100f * weights[(int)rarity] / total;
            string hex = ColorUtility.ToHtmlStringRGB(SkinChroma.RarityColor(rarity) * 0.78f);
            text.Append("\n<b><color=#").Append(hex).Append('>').Append(SkinChroma.RarityName(rarity)).Append("</color></b>  ")
                .Append(percent >= 10f ? Mathf.RoundToInt(percent).ToString() : percent.ToString("0.#")).Append('%');
        }

        return text.ToString();
    }

    void CreateChestArrow(RectTransform page, string objectName, string text, float x, int direction)
    {
        Button arrow = NewButton(page, text, MenuBuilder.PlumColor, null, 90f);
        arrow.name = objectName;
        RectTransform rect = (RectTransform)arrow.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(x, 0.47f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(110f, 160f);
        WireClick(arrow, () => ChangeChest(direction), true);
    }

    void ChangeChest(int direction)
    {
        if (chestBusy)
        {
            return;
        }

        Chests.SelectedIndex += direction;
        RefreshShop();
    }

    // Koloruje skrzynkę według rodzaju (korpus, wieczko i okucia), po nazwach obiektów.
    static void TintChest(ChestView view, ChestType chest)
    {
        foreach (Image image in view.root.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "Body")
            {
                image.color = chest.body;
            }
            else if (image.name == "Lid")
            {
                image.color = chest.lid;
            }
            else if (image.name == "Plate" || image.name.StartsWith("Band"))
            {
                image.color = chest.accent;
            }
        }
    }

    static void AddCoinIcon(Transform parent, float xMin, float yMin, float xMax, float yMax)
    {
        Image outer = NewImage("Coin", parent, new Color(0.85f, 0.58f, 0.05f), false);
        outer.sprite = CircleSprite();
        outer.raycastTarget = false;
        outer.preserveAspect = true;
        Anchor(outer.rectTransform, xMin, yMin, xMax, yMax);

        Image inner = NewImage("Shine", outer.transform, new Color(1f, 0.84f, 0.25f), false);
        inner.sprite = CircleSprite();
        inner.raycastTarget = false;
        Anchor(inner.rectTransform, 0.14f, 0.14f, 0.86f, 0.86f);
    }

    void BuildEditPage(Transform parent)
    {
        RectTransform page = MenuBuilder.NewPage("Page_Edit", parent);
        MenuBuilder.AddTitle(page, "EDYCJA", 110f);
        pages[Page.Edit] = page;
        editTitle = page.Find("Title").GetComponent<TextMeshProUGUI>();

        // duży podgląd wybranego wyglądu po lewej
        Image card = NewImage("Preview", page, MenuBuilder.CardColor);
        Anchor(card.rectTransform, 0.05f, 0.17f, 0.32f, 0.74f);
        card.raycastTarget = false;

        editPreview = NewImage("Image", card.transform, Color.white, false);
        Anchor(editPreview.rectTransform, 0.06f, 0.20f, 0.94f, 0.97f);
        editPreview.preserveAspect = true;
        editPreview.raycastTarget = false;

        editRarity = NewText(card.transform, "", 40f, MenuBuilder.DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        editRarity.name = "RarityText";
        Anchor(editRarity.rectTransform, 0.04f, 0.02f, 0.96f, 0.20f);

        // rząd kart: zwykły wygląd i chromy
        editRow = NewRect("Chromas", page);
        Anchor(editRow, 0.34f, 0.17f, 0.96f, 0.74f);

        HorizontalLayoutGroup layout = editRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 22f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        MenuBuilder.AddBackButton(page);
    }

    // ---------- Sklep: odświeżanie i kupno ----------

    void RefreshShop()
    {
        GameArt art = GameArt.Instance;
        ChestType chest = Chests.Selected;
        int total = art != null ? PlayerWallet.TotalCount(art, chest) : 0;
        int missing = art != null ? PlayerWallet.MissingCount(art, chest) : 0;
        bool all = missing == 0;

        shopCoinText.text = PlayerWallet.Format(PlayerWallet.Coins);
        collectionText.text = Loc.F("shop.collected", total - missing, total);
        openChestLabel.text = all ? Loc.T("shop.all") : Loc.F("shop.open", chest.price);

        if (chestTitleText != null)
        {
            chestTitleText.text = Loc.P("SKRZYNKA ", "CHEST ") + chest.Name;
        }

        TintChest(shopChest, chest);

        if (chancesText != null)
        {
            chancesText.enableAutoSizing = true;
            chancesText.fontSizeMin = 22f;
            chancesText.fontSizeMax = 38f;
            chancesText.text = ChanceLines(chest, art);
        }

        if (hintText != null)
        {
            hintText.text = Loc.F("shop.hint", PlayerWallet.WinReward);
        }

        openChestImage.color = !all && PlayerWallet.Coins >= chest.price ? MenuBuilder.OrangeColor : MenuBuilder.DisabledColor;

        if (!shopIdleRunning)
        {
            StartCoroutine(ShopIdle());
        }
    }

    // Skrzynka w sklepie delikatnie się buja, a wieczko co jakiś czas uchyla.
    IEnumerator ShopIdle()
    {
        shopIdleRunning = true;
        Vector2 basePosition = shopChest.root.anchoredPosition;

        while (shopChest.root != null && pages.ContainsKey(Page.Shop) && pages[Page.Shop].gameObject.activeInHierarchy)
        {
            float t = Time.unscaledTime;
            shopChest.root.anchoredPosition = basePosition + new Vector2(0f, Mathf.Sin(t * 2f) * 10f);
            shopChest.root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f) * 2.5f);
            shopChest.lid.localRotation = Quaternion.Euler(0f, 0f, Mathf.Max(0f, Mathf.Sin(t * 1.1f) - 0.55f) * 22f);
            Color glow = Chests.Selected.glow;
            shopChest.glow.color = new Color(glow.r, glow.g, glow.b, 0.18f + Mathf.Sin(t * 2.4f) * 0.06f);
            shopChest.glow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 12f);
            yield return null;
        }

        shopIdleRunning = false;
    }

    void OpenChestClicked()
    {
        GameArt art = GameArt.Instance;

        if (chestBusy || art == null)
        {
            return;
        }

        ChestType chest = Chests.Selected;

        if (PlayerWallet.MissingCount(art, chest) == 0 || PlayerWallet.Coins < chest.price)
        {
            GameAudio.Play(AudioEvent.ShopDenied);
            StartCoroutine(Wobble(openChestImage.rectTransform));
            return;
        }

        PlayerWallet.TrySpend(chest.price);

        if (!PlayerWallet.Roll(art, chest, out int skinIndex, out int chromaIndex))
        {
            return;
        }

        // wygrana jest zapisana od razu, więc nic nie przepada, nawet jeśli ktoś zamknie grę w trakcie animacji
        PlayerWallet.Unlock(art.players[skinIndex].label, chromaIndex);
        GameAudio.Play(AudioEvent.ShopBuy);
        RefreshShop();
        StartCoroutine(OpenChestRoutine(skinIndex, chromaIndex, chest));
    }

    IEnumerator Wobble(RectTransform rect)
    {
        yield return Tween(0.35f, u => rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(u * Mathf.PI * 6f) * 4f * (1f - u)));
        rect.localRotation = Quaternion.identity;
    }

    // ---------- Edycja skina ----------

    void RefreshEdit()
    {
        GameArt art = GameArt.Instance;

        for (int i = editRow.childCount - 1; i >= 0; i--)
        {
            Destroy(editRow.GetChild(i).gameObject);
        }

        if (art == null || art.SkinCount == 0)
        {
            editTitle.text = Loc.T("edit.title");
            return;
        }

        int skinIndex = Mathf.Clamp(SkinSelection.Index, 0, art.SkinCount - 1);
        PlayerSprites skin = art.players[skinIndex];
        SkinChroma[] chromas = skin.ChromaList;
        int equipped = PlayerWallet.Equipped(skin.label);

        editTitle.text = Loc.F("edit.titleOf", Loc.SkinName(skin.label).ToUpperInvariant());
        editPreview.sprite = ChromaFront(skin, equipped);
        ShowFx(editPreview, equipped >= 0 ? chromas[equipped].Fx : default);
        editRarity.text = equipped >= 0 ? Loc.ChromaName(chromas[equipped]) + "\n" + SkinChroma.RarityName(chromas[equipped].rarity) : Loc.T("edit.default");
        editRarity.color = equipped >= 0 ? SkinChroma.RarityColor(chromas[equipped].rarity) * 0.78f : MenuBuilder.DarkText;
        editRarity.fontSize = 34f;

        AddEditCard(skin, -1, equipped == -1, true);

        // chromy pogrupowane według motywu i rzadkości
        List<int> order = new List<int>();

        for (int c = 0; c < chromas.Length; c++)
        {
            order.Add(c);
        }

        order.Sort((a, b) =>
        {
            int byTheme = chromas[a].theme.CompareTo(chromas[b].theme);
            return byTheme != 0 ? byTheme : chromas[a].rarity != chromas[b].rarity ? chromas[a].rarity.CompareTo(chromas[b].rarity) : a.CompareTo(b);
        });

        foreach (int c in order)
        {
            AddEditCard(skin, c, equipped == c, PlayerWallet.Owns(skin.label, c));
        }
    }

    // Chromy jest dużo, więc zamiast jednego rzędu jest przewijana siatka 5 kolumn. Zwraca obiekt, do którego trafiają karty.
    RectTransform EnsureEditGrid(RectTransform chromas)
    {
        Transform existing = chromas.Find("Viewport/Content");

        if (existing != null)
        {
            return (RectTransform)existing;
        }

        HorizontalLayoutGroup row = chromas.GetComponent<HorizontalLayoutGroup>();

        if (row != null)
        {
            row.enabled = false;
        }

        ScrollRect scroll = chromas.GetComponent<ScrollRect>();

        if (scroll == null)
        {
            scroll = chromas.gameObject.AddComponent<ScrollRect>();
        }

        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 50f;

        RectTransform viewport = NewRect("Viewport", chromas);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        RectTransform content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(190f, 300f);
        grid.spacing = new Vector2(14f, 14f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(4, 4, 4, 20);

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        return content;
    }

    // Sam wzór chromy na obrazku postaci (bez cząstek), np. na małych kartach.
    static void ShowPattern(Image image, ChromaFxLook look)
    {
        UiEnchantGlint glint = image.GetComponent<UiEnchantGlint>();

        if (glint == null && look.glint)
        {
            glint = image.gameObject.AddComponent<UiEnchantGlint>();
        }

        if (glint != null)
        {
            glint.Configure(look);
        }
    }

    // Włącza (albo wyłącza) efekt chromy na obrazku postaci w interfejsie.
    static void ShowFx(Image image, ChromaFxLook look)
    {
        UiChromaFx emitter = image.GetComponent<UiChromaFx>();
        UiEnchantGlint glint = image.GetComponent<UiEnchantGlint>();

        if (emitter == null && look.HasParticles)
        {
            emitter = image.gameObject.AddComponent<UiChromaFx>();
        }

        if (glint == null && look.glint)
        {
            glint = image.gameObject.AddComponent<UiEnchantGlint>();
        }

        if (emitter != null)
        {
            emitter.Configure(image.rectTransform, look);
        }

        if (glint != null)
        {
            glint.Configure(look);
        }
    }

    // Grafika postaci od przodu w danej chromie (-1 = zwykła).
    static Sprite ChromaFront(PlayerSprites skin, int chroma)
    {
        SkinChroma[] list = skin.ChromaList;

        if (chroma < 0 || chroma >= list.Length)
        {
            return skin.Front != null ? skin.Front : skin.Portrait;
        }

        return list[chroma].front != null ? list[chroma].front : ChromaBaker.Recolor(skin.Front, list[chroma]);
    }

    void AddEditCard(PlayerSprites skin, int chroma, bool equipped, bool owned)
    {
        SkinChroma[] list = skin.ChromaList;
        bool isDefault = chroma < 0;

        Image card = NewImage(isDefault ? "Card_Default" : "Card_" + chroma, editRow, equipped ? EquippedColor : MenuBuilder.CardColor);
        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.transition = Selectable.Transition.None;
        AddButtonFx(button, card);

        Image preview = NewImage("Preview", card.transform, owned ? Color.white : new Color(0.12f, 0.08f, 0.12f, 1f), false);
        Anchor(preview.rectTransform, 0.06f, 0.34f, 0.94f, 0.97f);
        preview.sprite = owned ? ChromaFront(skin, chroma) : ChromaFront(skin, -1);
        preview.preserveAspect = true;
        preview.raycastTarget = false;

        // odblokowana chroma pokazuje na karcie swój wzór (ikonki zsuwające się po postaci)
        if (owned && !isDefault)
        {
            ShowPattern(preview, list[chroma].Fx);
        }

        // kłódka na zablokowanej chromie
        if (!owned)
        {
            Image lockShadow = NewImage("LockShadow", card.transform, new Color(0f, 0f, 0f, 0.55f), false);
            lockShadow.sprite = LockSprite();
            lockShadow.preserveAspect = true;
            lockShadow.raycastTarget = false;
            Anchor(lockShadow.rectTransform, 0.27f, 0.45f, 0.73f, 0.83f);

            Image padlock = NewImage("Lock", card.transform, ChestGoldColor, false);
            padlock.sprite = LockSprite();
            padlock.preserveAspect = true;
            padlock.raycastTarget = false;
            Anchor(padlock.rectTransform, 0.28f, 0.47f, 0.72f, 0.85f);
        }

        string title = isDefault ? Loc.T("edit.default") : Loc.ChromaName(list[chroma]);
        TextMeshProUGUI name = NewText(card.transform, title, 34f, MenuBuilder.DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        name.enableAutoSizing = true;
        name.fontSizeMin = 18f;
        name.fontSizeMax = 34f;
        Anchor(name.rectTransform, 0.02f, 0.19f, 0.98f, 0.34f);

        string state = equipped ? Loc.T("skin.selected") : owned ? "" : Loc.T("edit.locked");
        Color stateColor = equipped ? new Color(0.10f, 0.45f, 0.15f) : new Color(0.55f, 0.20f, 0.20f);

        if (!isDefault)
        {
            string rarityName = SkinChroma.RarityName(list[chroma].rarity);
            state = equipped || !owned ? state : rarityName;
            stateColor = equipped ? stateColor : owned ? SkinChroma.RarityColor(list[chroma].rarity) * 0.78f : stateColor;
        }

        TextMeshProUGUI stateText = NewText(card.transform, state, 28f, stateColor, TextAlignmentOptions.Center, FontStyles.Bold);
        Anchor(stateText.rectTransform, 0.02f, 0.03f, 0.98f, 0.19f);

        int chosen = chroma;

        WireClick(button, () =>
        {
            if (!owned)
            {
                GameAudio.Play(AudioEvent.ShopDenied);
                StartCoroutine(Wobble((RectTransform)card.transform));
                return;
            }

            PlayerWallet.Equip(skin.label, chosen);
            GameAudio.Play(AudioEvent.ChromaEquip);
            RefreshEdit();
        }, false);
    }

    // ---------- Skrzynka: wygląd ----------

    ChestView BuildChest(Transform parent, string objectName)
    {
        ChestView view = new ChestView();
        view.root = NewRect(objectName, parent);
        view.root.anchorMin = view.root.anchorMax = new Vector2(0.5f, 0.5f);
        view.root.pivot = new Vector2(0.5f, 0.5f);
        view.root.sizeDelta = new Vector2(420f, 380f);

        view.glow = NewImage("Glow", view.root, new Color(1f, 0.85f, 0.4f, 0f), false);
        view.glow.sprite = RaysSprite();
        view.glow.raycastTarget = false;
        Anchor(view.glow.rectTransform, -0.7f, -0.7f, 1.7f, 1.7f);

        Image shadow = NewImage("Shadow", view.root, new Color(0f, 0f, 0f, 0.35f), false);
        shadow.sprite = CircleSprite();
        shadow.raycastTarget = false;
        Anchor(shadow.rectTransform, 0.0f, -0.07f, 1.0f, 0.13f);

        Image body = NewImage("Body", view.root, ChestBodyColor);
        body.raycastTarget = false;
        Anchor(body.rectTransform, 0.04f, 0.02f, 0.96f, 0.58f);
        AddChestBands(body.rectTransform);

        Image lid = NewImage("Lid", view.root, ChestLidColor);
        lid.raycastTarget = false;
        Anchor(lid.rectTransform, 0.0f, 0.50f, 1.0f, 0.98f);
        lid.rectTransform.pivot = new Vector2(0f, 0f);
        view.lid = lid.rectTransform;
        AddChestBands(lid.rectTransform);

        Image shine = NewImage("Shine", lid.transform, new Color(1f, 1f, 1f, 0.16f));
        shine.raycastTarget = false;
        Anchor(shine.rectTransform, 0.30f, 0.66f, 0.70f, 0.86f);

        Image plate = NewImage("Plate", lid.transform, ChestGoldColor);
        plate.raycastTarget = false;
        Anchor(plate.rectTransform, 0.42f, -0.12f, 0.58f, 0.34f);

        Image keyhole = NewImage("Keyhole", plate.transform, new Color(0.25f, 0.12f, 0.05f), false);
        keyhole.sprite = CircleSprite();
        keyhole.raycastTarget = false;
        Anchor(keyhole.rectTransform, 0.30f, 0.35f, 0.70f, 0.75f);

        return view;
    }

    static void AddChestBands(RectTransform parent)
    {
        for (int i = 0; i < 2; i++)
        {
            Image band = NewImage("Band" + i, parent, ChestGoldColor, false);
            band.raycastTarget = false;
            float x = i == 0 ? 0.14f : 0.76f;
            Anchor(band.rectTransform, x, 0f, x + 0.10f, 1f);
        }
    }

    static Sprite lockSprite;

    // Kłódka rysowana w kodzie (biała, barwiona przy użyciu): kabłąk, korpus i dziurka na klucz.
    static Sprite LockSprite()
    {
        if (lockSprite != null)
        {
            return lockSprite;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int covered = 0;

                for (int sy = 0; sy < 3; sy++)
                {
                    for (int sx = 0; sx < 3; sx++)
                    {
                        if (InsideLock((x + (sx + 0.5f) / 3f) / size, (y + (sy + 0.5f) / 3f) / size))
                        {
                            covered++;
                        }
                    }
                }

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(covered * 255 / 9));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        lockSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        lockSprite.hideFlags = HideFlags.HideAndDontSave;
        return lockSprite;
    }

    static bool InsideLock(float x, float y)
    {
        // korpus: zaokrąglony prostokąt
        Vector2 center = new Vector2(0.5f, 0.31f);
        Vector2 half = new Vector2(0.30f, 0.23f);
        const float radius = 0.07f;
        Vector2 d = new Vector2(Mathf.Abs(x - center.x), Mathf.Abs(y - center.y)) - (half - new Vector2(radius, radius));
        float outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude - radius;
        bool body = outside <= 0f;

        // dziurka od klucza wycięta z korpusu
        float keyhole = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.34f) * (y - 0.34f));
        bool hole = keyhole < 0.055f || (Mathf.Abs(x - 0.5f) < 0.025f && y > 0.20f && y < 0.34f);

        if (body)
        {
            return !hole;
        }

        // kabłąk: półokrąg nad korpusem i dwie nóżki
        float ring = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.56f) * (y - 0.56f));
        bool arc = y >= 0.56f && ring <= 0.25f && ring >= 0.15f;
        bool legs = y < 0.56f && y > 0.40f && Mathf.Abs(Mathf.Abs(x - 0.5f) - 0.20f) <= 0.05f;
        return arc || legs;
    }

    // Promienie (sprite rysowany w kodzie): jasne wachlarze, które się obracają za skrzynką i kartą.
    static Sprite RaysSprite()
    {
        if (raysOverride != null)
        {
            return raysOverride;
        }

        if (raysSprite != null)
        {
            return raysSprite;
        }

        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x + 0.5f, y + 0.5f) - center;
                float radius = offset.magnitude / (size * 0.5f);
                float wave = 0.5f + 0.5f * Mathf.Cos(Mathf.Atan2(offset.y, offset.x) * 10f);
                float ray = Mathf.SmoothStep(0.45f, 0.95f, wave);
                float fade = Mathf.Clamp01(1f - radius);
                byte alpha = (byte)Mathf.RoundToInt(255f * ray * fade * fade);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        raysSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        raysSprite.hideFlags = HideFlags.HideAndDontSave;
        return raysSprite;
    }

    // ---------- Nakładka z otwieraniem skrzynki ----------

    void BuildChestOverlay(Transform parent)
    {
        if (FindDeep(transform, "ChestOverlay") != null)
        {
            return;
        }

        overlay = new ChestOverlay();

        overlay.root = NewRect("ChestOverlay", parent);
        Stretch(overlay.root);

        // przyciemnienie blokuje klikanie w to, co pod spodem
        overlay.dim = overlay.root.gameObject.AddComponent<Image>();
        overlay.dim.color = new Color(0.05f, 0.02f, 0.06f, 0f);

        overlay.rays = NewImage("Rays", overlay.root, new Color(1f, 0.85f, 0.4f, 0f), false);
        overlay.rays.sprite = RaysSprite();
        overlay.rays.raycastTarget = false;
        CenterRect(overlay.rays.rectTransform, 1900f, 1900f);

        overlay.chest = BuildChest(overlay.root, "Chest");
        overlay.chest.root.anchoredPosition = new Vector2(0f, -60f);

        overlay.sparks = NewRect("Sparks", overlay.root);
        Stretch(overlay.sparks);

        overlay.flash = NewImage("Flash", overlay.root, new Color(1f, 1f, 1f, 0f), false);
        overlay.flash.raycastTarget = false;
        Stretch(overlay.flash.rectTransform);

        // karta z wynikiem
        overlay.card = NewRect("ResultCard", overlay.root);
        CenterRect(overlay.card, 520f, 640f);
        overlay.card.anchoredPosition = new Vector2(0f, 70f);

        overlay.cardBorder = NewImage("Border", overlay.card, Color.white);
        overlay.cardBorder.raycastTarget = false;
        Stretch(overlay.cardBorder.rectTransform);

        Image inner = NewImage("Inner", overlay.card, MenuBuilder.CardColor);
        inner.raycastTarget = false;
        Stretch(inner.rectTransform, 12f);

        overlay.cardPreview = NewImage("Preview", overlay.card, Color.white, false);
        overlay.cardPreview.preserveAspect = true;
        overlay.cardPreview.raycastTarget = false;
        Anchor(overlay.cardPreview.rectTransform, 0.08f, 0.26f, 0.92f, 0.94f);

        overlay.cardName = NewText(overlay.card, "", 54f, MenuBuilder.DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        overlay.cardName.name = "CardName";
        Anchor(overlay.cardName.rectTransform, 0.04f, 0.13f, 0.96f, 0.27f);

        overlay.cardRarity = NewText(overlay.card, "", 40f, MenuBuilder.DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        overlay.cardRarity.name = "CardRarity";
        Anchor(overlay.cardRarity.rectTransform, 0.04f, 0.03f, 0.96f, 0.14f);

        // przyciski pod kartą
        overlay.buttons = NewRect("Buttons", overlay.root);
        overlay.buttons.anchorMin = overlay.buttons.anchorMax = new Vector2(0.5f, 0.5f);
        overlay.buttons.pivot = new Vector2(0.5f, 1f);
        overlay.buttons.anchoredPosition = new Vector2(0f, -290f);
        overlay.buttons.sizeDelta = new Vector2(900f, 130f);

        Button equip = NewButton(overlay.buttons, "ZAŁÓŻ", MenuBuilder.OrangeColor, null, 56f);
        equip.name = "Button_ChestEquip";
        Anchor((RectTransform)equip.transform, 0.02f, 0f, 0.48f, 1f);

        Button ok = NewButton(overlay.buttons, "OK", MenuBuilder.PlumColor, null, 56f);
        ok.name = "Button_ChestOk";
        Anchor((RectTransform)ok.transform, 0.52f, 0f, 0.98f, 1f);

        overlay.root.gameObject.SetActive(false);
    }

    static void CenterRect(RectTransform rect, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
    }

    // ---------- Animacja otwierania ----------

    IEnumerator OpenChestRoutine(int skinIndex, int chromaIndex, ChestType chest)
    {
        chestBusy = true;
        Color glowColor = chest.glow;

        GameArt art = GameArt.Instance;
        PlayerSprites skin = art.players[skinIndex];
        SkinChroma chroma = skin.ChromaList[chromaIndex];
        Color rarityColor = SkinChroma.RarityColor(chroma.rarity);
        int rarityLevel = (int)chroma.rarity;

        ChestOverlay ov = overlay;
        ov.root.gameObject.SetActive(true);
        ov.dim.color = new Color(0.05f, 0.02f, 0.06f, 0f);
        ov.rays.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0f);
        ov.flash.color = new Color(1f, 1f, 1f, 0f);
        ov.card.gameObject.SetActive(false);
        ov.buttons.gameObject.SetActive(false);
        ov.chest.root.localScale = Vector3.zero;
        ov.chest.root.localRotation = Quaternion.identity;
        ov.chest.root.anchoredPosition = new Vector2(0f, -60f);
        ov.chest.lid.localRotation = Quaternion.identity;
        ov.chest.glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        TintChest(ov.chest, chest);
        ClearParticles();
        resultChosen = false;
        equipAfterOpen = false;

        StartCoroutine(OverlayDriver());

        // 1. skrzynka wjeżdża na ekran
        yield return Tween(0.45f, u =>
        {
            ov.dim.color = new Color(0.05f, 0.02f, 0.06f, 0.9f * u);
            ov.chest.root.localScale = Vector3.one * EaseOutBack(u);
        });

        // 2. trzęsie się coraz mocniej, w środku robi się jasno
        GameAudio.Play(AudioEvent.ChestShake);
        float time = 0f;

        yield return Tween(1.7f, u =>
        {
            time += Time.unscaledDeltaTime;
            float amplitude = Mathf.Lerp(2f, 14f, u * u);
            float frequency = Mathf.Lerp(26f, 60f, u);
            ov.chest.root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * frequency) * amplitude);
            ov.chest.root.localScale = Vector3.one * (1f + 0.09f * u * u + Mathf.Sin(time * 18f) * 0.015f * u);
            ov.chest.glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.8f * u);
            ov.chest.lid.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * frequency * 0.7f) * 3f * u);

            if (UnityEngine.Random.value < 0.2f + u * 0.5f)
            {
                Vector2 spot = new Vector2(UnityEngine.Random.Range(-170f, 170f), UnityEngine.Random.Range(-60f, 80f));
                SpawnParticle(spot, new Vector2(UnityEngine.Random.Range(-60f, 60f), UnityEngine.Random.Range(160f, 380f)),
                    new Color(1f, 0.9f, 0.5f, 1f), UnityEngine.Random.Range(10f, 20f), 0.7f, 0f, true);
            }
        });

        // 3. błysk i otwarcie wieczka
        GameAudio.Play(AudioEvent.ChestOpen);
        GameAudio.Play(RevealEvent(chroma.rarity));

        ov.flash.color = Color.white;
        ov.chest.root.localRotation = Quaternion.identity;
        ov.rays.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.9f);
        SpawnBurst(rarityColor, 34 + rarityLevel * 22);

        yield return Tween(0.45f, u =>
        {
            ov.flash.color = new Color(1f, 1f, 1f, 1f - u);
            ov.chest.lid.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 64f, EaseOutCubic(u)));
            ov.chest.root.localScale = Vector3.one * Mathf.Lerp(1.09f, 1f, u);
        });

        // 4. karta wylatuje ze skrzynki
        ov.cardBorder.color = rarityColor;
        ov.cardPreview.sprite = ChromaFront(skin, chromaIndex);
        ShowFx(ov.cardPreview, chroma.Fx);
        ov.cardName.text = Loc.SkinName(skin.label) + "  " + Loc.ChromaName(chroma);
        ov.cardName.fontSize = 46f;
        ov.cardRarity.text = SkinChroma.RarityName(chroma.rarity) + "  -  " + Loc.T("chest.new");
        ov.cardRarity.color = rarityColor * 0.75f;
        ov.card.gameObject.SetActive(true);

        yield return Tween(0.75f, u =>
        {
            float pop = EaseOutBack(u);
            ov.card.localScale = Vector3.one * Mathf.LerpUnclamped(0.1f, 1f, pop);
            ov.card.anchoredPosition = Vector2.LerpUnclamped(new Vector2(0f, -40f), new Vector2(0f, 70f), EaseOutCubic(u));
            ov.card.localRotation = Quaternion.Euler(0f, 0f, (1f - u) * -14f);
            ov.chest.root.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, u);
            ov.chest.root.anchoredPosition = Vector2.Lerp(new Vector2(0f, -60f), new Vector2(0f, -300f), EaseOutCubic(u));
        });

        SpawnBurst(rarityColor, 18 + rarityLevel * 10);
        yield return new WaitForSecondsRealtime(0.4f);

        // 5. przyciski i czekanie na wybór
        ov.buttons.gameObject.SetActive(true);

        yield return Tween(0.25f, u => ov.buttons.localScale = Vector3.one * EaseOutBack(u));

        while (!resultChosen)
        {
            // karta lekko oddycha, żeby ekran nie był martwy
            ov.card.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 2.5f) * 0.012f);
            yield return null;
        }

        if (equipAfterOpen)
        {
            PlayerWallet.Equip(skin.label, chromaIndex);
            SkinSelection.Index = skinIndex;
            GameAudio.Play(AudioEvent.ChromaEquip);
        }

        // 6. zamknięcie
        yield return Tween(0.25f, u =>
        {
            ov.dim.color = new Color(0.05f, 0.02f, 0.06f, 0.9f * (1f - u));
            ov.card.localScale = Vector3.one * (1f - u);
            ov.buttons.localScale = Vector3.one * (1f - u);
        });

        ShowFx(ov.cardPreview, default);
        ov.root.gameObject.SetActive(false);
        ClearParticles();
        chestBusy = false;
        RefreshShop();
    }

    static AudioEvent RevealEvent(ChromaRarity rarity)
    {
        switch (rarity)
        {
            case ChromaRarity.Rare: return AudioEvent.ChestRevealRare;
            case ChromaRarity.Epic: return AudioEvent.ChestRevealEpic;
            case ChromaRarity.Legendary: return AudioEvent.ChestRevealLegendary;
            default: return AudioEvent.ChestRevealCommon;
        }
    }

    // Obraca promienie i porusza konfetti, dopóki nakładka jest widoczna.
    IEnumerator OverlayDriver()
    {
        while (overlay.root.gameObject.activeSelf)
        {
            float dt = Time.unscaledDeltaTime;
            overlay.rays.rectTransform.Rotate(0f, 0f, -22f * dt);

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.life -= dt;

                if (p.life <= 0f || p.rect == null)
                {
                    if (p.rect != null)
                    {
                        Destroy(p.rect.gameObject);
                    }

                    particles.RemoveAt(i);
                    continue;
                }

                p.velocity.y -= p.gravity * dt;
                p.rect.anchoredPosition += p.velocity * dt;
                p.rect.Rotate(0f, 0f, p.spin * dt);

                float fade = Mathf.Clamp01(p.life / (p.maxLife * 0.35f));
                p.image.color = new Color(p.color.r, p.color.g, p.color.b, p.color.a * fade);
            }

            yield return null;
        }
    }

    void SpawnParticle(Vector2 position, Vector2 velocity, Color color, float size, float life, float gravity, bool round)
    {
        Image image = NewImage("P", overlay.sparks, color, false);
        image.sprite = round ? CircleSprite() : RoundedSprite();
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        CenterRect(image.rectTransform, size, size * (round ? 1f : 0.6f));
        image.rectTransform.anchoredPosition = position + new Vector2(0f, -60f);

        particles.Add(new Particle
        {
            rect = image.rectTransform,
            image = image,
            velocity = velocity,
            spin = UnityEngine.Random.Range(-540f, 540f),
            life = life,
            maxLife = life,
            gravity = gravity,
            color = color
        });
    }

    // Fajerwerk konfetti ze skrzynki (kolory rzadkości, biały i złoty).
    void SpawnBurst(Color rarityColor, int count)
    {
        Color[] palette = { rarityColor, Color.white, ChestGoldColor, rarityColor * 0.8f + new Color(0.2f, 0.2f, 0.2f, 0f) };

        for (int i = 0; i < count; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float speed = UnityEngine.Random.Range(380f, 1250f);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) + 0.35f) * speed;
            Color color = palette[UnityEngine.Random.Range(0, palette.Length)];
            color.a = 1f;
            SpawnParticle(new Vector2(0f, 20f), velocity, color, UnityEngine.Random.Range(16f, 34f), UnityEngine.Random.Range(1.1f, 2.0f), 1500f, false);
        }
    }

    void ClearParticles()
    {
        foreach (Particle p in particles)
        {
            if (p.rect != null)
            {
                Destroy(p.rect.gameObject);
            }
        }

        particles.Clear();
    }

    // ---------- Pomocnicze animacje ----------

    // Wykonuje krok animacji co klatkę od 0 do 1 przez podany czas (niezależnie od Time.timeScale).
    static IEnumerator Tween(float duration, Action<float> step)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            step(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        step(1f);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    static float EaseOutCubic(float x)
    {
        return 1f - Mathf.Pow(1f - x, 3f);
    }
}
