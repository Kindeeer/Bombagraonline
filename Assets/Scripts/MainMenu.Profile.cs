using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiKit;

// Ekrany gracza w menu:
// - PROFIL: ikonka, poziom konta z paskiem doświadczenia i statystyki (dane liczy PlayerProgress),
// - IKONKI: wybór ikonki profilu. Zwykłe buźki ma każdy, a buźka w kolorach chromy odblokowuje się razem z chromą,
// - MISJE: trzy misje dzienne z postępem i przyciskiem odbioru nagrody (DailyMissions).
// Do profilu i misji wchodzi się dwiema plakietkami w lewym górnym rogu menu głównego.
public partial class MainMenu
{
    static readonly Color AccentText = new Color(0.75f, 0.35f, 0.08f);

    TextMeshProUGUI profileTitle;
    TextMeshProUGUI profileLevel;
    TextMeshProUGUI profileNick;
    TextMeshProUGUI profileXp;
    TextMeshProUGUI profileReward;
    TextMeshProUGUI profileStatNames;
    TextMeshProUGUI profileStatValues;
    TextMeshProUGUI profileBadge;
    TextMeshProUGUI profileChangeLabel;
    Image profileBadgeFace;
    Image profileBar;
    Image profileFace;

    TextMeshProUGUI iconsTitle;
    RectTransform iconsContent;

    class MissionRow
    {
        public TextMeshProUGUI title;
        public TextMeshProUGUI progress;
        public TextMeshProUGUI reward;
        public TextMeshProUGUI buttonLabel;
        public Image fill;
        public Image buttonImage;
        public Button button;
    }

    readonly List<MissionRow> missionRows = new List<MissionRow>();
    TextMeshProUGUI missionsTitle;
    TextMeshProUGUI missionsTimer;
    // sekunda pokazana ostatnio na zegarze misji (-1 = trzeba odświeżyć napis)
    int missionsTimerSecond = -1;
    TextMeshProUGUI missionsBadge;
    Image missionsBadgeImage;

    void BuildProfilePage()
    {
        if (!pages.TryGetValue(Page.Main, out RectTransform main))
        {
            return;
        }

        BuildProfileCards(main);
        BuildIconsPage(main);
        BuildMissionsPage(main);
        BuildMainBadges(main);

        pages[Page.Profile].gameObject.SetActive(false);
        pages[Page.Icons].gameObject.SetActive(false);
        pages[Page.Missions].gameObject.SetActive(false);
    }

    RectTransform EnsurePage(Page key, string title, RectTransform main)
    {
        if (!pages.ContainsKey(key))
        {
            RectTransform page = MenuBuilder.NewPage("Page_" + key, main.parent);
            MenuBuilder.AddTitle(page, title, 130f);
            MenuBuilder.AddBackButton(page);
            pages[key] = page;
        }

        return pages[key];
    }

    // ---------- PROFIL ----------

    void BuildProfileCards(RectTransform main)
    {
        RectTransform profile = EnsurePage(Page.Profile, "PROFIL", main);
        profileTitle = profile.Find("Title").GetComponent<TextMeshProUGUI>();

        // lewa karta: ikonka profilu, nick, poziom i pasek doświadczenia
        Image left = NewImage("LevelCard", profile, MenuBuilder.CardColor);
        Anchor(left.rectTransform, 0.07f, 0.20f, 0.40f, 0.74f);

        profileFace = NewImage("Face", left.transform, Color.white, false);
        profileFace.preserveAspect = true;
        profileFace.raycastTarget = false;
        Anchor(profileFace.rectTransform, 0.30f, 0.62f, 0.70f, 0.96f);

        Button change = NewButton(left.transform, "", MenuBuilder.PlumColor, () => ShowPage(Page.Icons), 28f, true);
        change.name = "Button_ChangeIcon";
        Anchor((RectTransform)change.transform, 0.72f, 0.72f, 0.97f, 0.86f);
        profileChangeLabel = change.GetComponentInChildren<TextMeshProUGUI>();

        profileNick = NewText(left.transform, "", 44f, MenuBuilder.DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        Anchor(profileNick.rectTransform, 0.04f, 0.52f, 0.96f, 0.62f);

        profileLevel = NewText(left.transform, "", 72f, AccentText, TextAlignmentOptions.Center, FontStyles.Bold);
        Anchor(profileLevel.rectTransform, 0.04f, 0.36f, 0.96f, 0.52f);

        Image track = NewImage("XpTrack", left.transform, new Color(0.36f, 0.13f, 0.27f, 0.35f));
        Anchor(track.rectTransform, 0.08f, 0.25f, 0.92f, 0.33f);

        profileBar = NewImage("XpFill", track.transform, MenuBuilder.OrangeColor);
        profileBar.raycastTarget = false;

        profileXp = NewText(left.transform, "", 34f, MenuBuilder.DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        Anchor(profileXp.rectTransform, 0.04f, 0.15f, 0.96f, 0.24f);

        profileReward = NewText(left.transform, "", 28f, MenuBuilder.DarkText, TextAlignmentOptions.Center);
        Anchor(profileReward.rectTransform, 0.04f, 0.03f, 0.96f, 0.15f);
        profileReward.alpha = 0.8f;

        // prawa karta: statystyki (nazwy po lewej, liczby po prawej)
        Image right = NewImage("StatsCard", profile, MenuBuilder.CardColor);
        Anchor(right.rectTransform, 0.44f, 0.20f, 0.93f, 0.74f);

        profileStatNames = NewText(right.transform, "", 36f, MenuBuilder.DarkText, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Anchor(profileStatNames.rectTransform, 0.06f, 0.05f, 0.74f, 0.95f);
        AutoSize(profileStatNames, 20f, 36f);

        profileStatValues = NewText(right.transform, "", 36f, AccentText, TextAlignmentOptions.TopRight, FontStyles.Bold);
        Anchor(profileStatValues.rectTransform, 0.60f, 0.05f, 0.94f, 0.95f);
        AutoSize(profileStatValues, 20f, 36f);
    }

    static void AutoSize(TextMeshProUGUI text, float min, float max)
    {
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
    }

    // Dwie plakietki w menu głównym: profil (ikonka, poziom, nick) i misje (ile zrobionych).
    void BuildMainBadges(RectTransform main)
    {
        Button badge = FindOrAddButton(main, "Button_Profile", MenuBuilder.PlumColor, 0.02f, 0.87f, 0.24f, 0.97f);
        WireClick(badge, () => ShowPage(Page.Profile), true);
        profileBadge = badge.GetComponentInChildren<TextMeshProUGUI>();

        if (profileBadge != null)
        {
            AutoSize(profileBadge, 20f, 38f);
            // miejsce na ikonkę po lewej stronie plakietki
            profileBadge.margin = new Vector4(86f, 0f, 8f, 0f);
        }

        Transform faceTransform = badge.transform.Find("Face");
        profileBadgeFace = faceTransform != null ? faceTransform.GetComponent<Image>() : null;

        if (profileBadgeFace == null)
        {
            profileBadgeFace = NewImage("Face", badge.transform, Color.white, false);
            profileBadgeFace.preserveAspect = true;
            profileBadgeFace.raycastTarget = false;
            Anchor(profileBadgeFace.rectTransform, 0.02f, 0.06f, 0.22f, 0.94f);
        }

        Button missions = FindOrAddButton(main, "Button_Missions", MenuBuilder.PlumColor, 0.02f, 0.76f, 0.24f, 0.86f);
        WireClick(missions, () => ShowPage(Page.Missions), true);
        missionsBadge = missions.GetComponentInChildren<TextMeshProUGUI>();
        missionsBadgeImage = missions.GetComponent<Image>();

        if (missionsBadge != null)
        {
            AutoSize(missionsBadge, 20f, 38f);
        }
    }

    Button FindOrAddButton(RectTransform parent, string objectName, Color color, float xMin, float yMin, float xMax, float yMax)
    {
        Transform existing = parent.Find(objectName);
        Button button = existing != null ? existing.GetComponent<Button>() : null;

        if (button == null)
        {
            button = NewButton(parent, "", color, null, 38f);
            button.name = objectName;
            Anchor((RectTransform)button.transform, xMin, yMin, xMax, yMax);
        }

        return button;
    }

    // Napisy na plakietkach w menu głównym.
    void RefreshProfileBadge()
    {
        if (profileBadge != null)
        {
            profileBadge.text = Loc.P("POZIOM ", "LEVEL ") + PlayerProgress.Level + "\n" + PlayerProfile.Nick;
        }

        if (profileBadgeFace != null)
        {
            ShowAvatar(profileBadgeFace, false);
        }

        if (missionsBadge != null)
        {
            int claimable = DailyMissions.ClaimableCount;
            missionsBadge.text = Loc.P("MISJE ", "MISSIONS ") + DailyMissions.DoneCount + "/" + DailyMissions.Count
                + (claimable > 0 ? Loc.P("  •  ODBIERZ!", "  •  CLAIM!") : "");

            // pomarańczowa plakietka, gdy czeka nagroda do odebrania
            if (missionsBadgeImage != null)
            {
                missionsBadgeImage.color = claimable > 0 ? MenuBuilder.OrangeColor : MenuBuilder.PlumColor;
            }
        }
    }

    // Wpisuje w obrazek ikonkę profilu gracza (z wzorem chromy, gdy ikonka jest w kolorach chromy).
    static void ShowAvatar(Image image, bool withPattern)
    {
        PlayerSprites look = PlayerAvatar.Look(GameArt.Instance);
        Sprite face = look != null ? (look.Portrait != null ? look.Portrait : look.Front) : null;

        image.sprite = face;
        image.enabled = face != null;

        if (withPattern)
        {
            ShowPattern(image, look != null && face != null ? look.fx : default);
        }
    }

    void RefreshProfile()
    {
        if (profileLevel == null)
        {
            return;
        }

        int level = PlayerProgress.Level;
        int xp = PlayerProgress.XpInLevel;
        int needed = PlayerProgress.XpToNext(level);

        profileTitle.text = Loc.P("PROFIL", "PROFILE");
        profileChangeLabel.text = Loc.P("ZMIEŃ", "CHANGE");
        profileNick.text = PlayerProfile.Nick;
        profileLevel.text = Loc.P("POZIOM ", "LEVEL ") + level;
        profileXp.text = xp + " / " + needed + " XP";
        profileReward.text = Loc.P("Nagroda za poziom ", "Reward for level ") + (level + 1) + ": "
            + PlayerProgress.LevelReward(level + 1) + Loc.P(" monet", " coins")
            + "\n" + Loc.P("XP: runda +", "XP: round +") + PlayerProgress.XpForRound
            + Loc.P(", wygrana +", ", win +") + PlayerProgress.XpForWin
            + Loc.P(", zabicie +", ", kill +") + PlayerProgress.XpForPlayerKill;

        // pasek doświadczenia: wypełnienie od lewej
        SetFill(profileBar, needed > 0 ? (float)xp / needed : 0f);
        ShowAvatar(profileFace, true);

        GameArt art = GameArt.Instance;
        string favorite = PlayerProgress.FavoriteSkin(art);

        profileStatNames.text = string.Join("\n",
            Loc.P("Rozegrane rundy", "Rounds played"),
            Loc.P("Wygrane rundy", "Rounds won"),
            Loc.P("Skuteczność", "Win rate"),
            Loc.P("Najdłuższa seria wygranych", "Best win streak"),
            Loc.P("Zabici gracze", "Players killed"),
            Loc.P("Pokonane boty", "Bots defeated"),
            Loc.P("Śmierci", "Deaths"),
            Loc.P("Postawione bomby", "Bombs placed"),
            Loc.P("Zebrane przedmioty", "Items collected"),
            Loc.P("Rundy online", "Online rounds"),
            Loc.P("Zdobyte monety", "Coins earned"),
            Loc.P("Ulubiona postać", "Favourite character"));

        profileStatValues.text = string.Join("\n",
            PlayerProgress.Get(PlayerStat.RoundsPlayed),
            PlayerProgress.Get(PlayerStat.RoundsWon),
            PlayerProgress.WinPercent + "%",
            PlayerProgress.Get(PlayerStat.BestWinStreak),
            PlayerProgress.Get(PlayerStat.PlayerKills),
            PlayerProgress.Get(PlayerStat.BotKills),
            PlayerProgress.Get(PlayerStat.Deaths),
            PlayerProgress.Get(PlayerStat.BombsPlaced),
            PlayerProgress.Get(PlayerStat.PickupsCollected),
            PlayerProgress.Get(PlayerStat.OnlineRounds),
            PlayerWallet.Format(PlayerProgress.Get(PlayerStat.CoinsEarned)),
            favorite.Length > 0 ? Loc.SkinName(favorite) : "-");
    }

    // Pasek postępu: obrazek wypełnia rodzica od lewej do podanego ułamka.
    static void SetFill(Image fill, float fraction)
    {
        RectTransform rect = fill.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        fill.enabled = fraction > 0.001f;
    }

    // ---------- IKONKI ----------

    void BuildIconsPage(RectTransform main)
    {
        RectTransform page = EnsurePage(Page.Icons, "IKONKI", main);
        iconsTitle = page.Find("Title").GetComponent<TextMeshProUGUI>();

        Transform existing = page.Find("Scroll/Viewport/Content");

        if (existing != null)
        {
            iconsContent = (RectTransform)existing;
            return;
        }

        // przewijana siatka kart (liczba kolumn dopasowuje się do szerokości ekranu)
        RectTransform scrollRect = NewRect("Scroll", page);
        Anchor(scrollRect, 0.04f, 0.19f, 0.96f, 0.75f);

        ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 50f;

        RectTransform viewport = NewRect("Viewport", scrollRect);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        iconsContent = NewRect("Content", viewport);
        iconsContent.anchorMin = new Vector2(0f, 1f);
        iconsContent.anchorMax = new Vector2(1f, 1f);
        iconsContent.pivot = new Vector2(0.5f, 1f);
        iconsContent.offsetMin = Vector2.zero;
        iconsContent.offsetMax = Vector2.zero;

        GridLayoutGroup grid = iconsContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(196f, 236f);
        grid.spacing = new Vector2(12f, 12f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(4, 4, 4, 20);

        ContentSizeFitter fitter = iconsContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = iconsContent;
    }

    // Buduje karty ikonek od nowa: najpierw odblokowane (zwykłe buźki i chromy, które gracz ma), potem zablokowane.
    void RebuildIcons()
    {
        GameArt art = GameArt.Instance;

        if (iconsContent == null || art == null || art.SkinCount == 0)
        {
            return;
        }

        for (int i = iconsContent.childCount - 1; i >= 0; i--)
        {
            Destroy(iconsContent.GetChild(i).gameObject);
        }

        PlayerAvatar.Current(art, out int chosenSkin, out int chosenChroma);

        int total = 0;
        int unlocked = 0;

        // dwa przejścia: odblokowane, potem zablokowane (karty lądują w siatce w tej kolejności)
        for (int pass = 0; pass < 2; pass++)
        {
            for (int skinIndex = 0; skinIndex < art.SkinCount; skinIndex++)
            {
                PlayerSprites skin = art.players[skinIndex];

                for (int chroma = -1; chroma < skin.ChromaList.Length; chroma++)
                {
                    bool owned = PlayerAvatar.Unlocked(skin.label, chroma);

                    if (pass == 0)
                    {
                        total++;
                        unlocked += owned ? 1 : 0;
                    }

                    if (owned == (pass == 0))
                    {
                        AddIconCard(art, skinIndex, chroma, owned, skinIndex == chosenSkin && chroma == chosenChroma);
                    }
                }
            }
        }

        iconsTitle.text = Loc.P("IKONKI  ", "ICONS  ") + unlocked + "/" + total;
    }

    void AddIconCard(GameArt art, int skinIndex, int chroma, bool owned, bool chosen)
    {
        PlayerSprites skin = art.players[skinIndex];
        PlayerSprites look = owned ? (art.SkinLook(skinIndex, chroma) ?? skin) : skin;
        Sprite face = look.Portrait != null ? look.Portrait : look.Front;

        Image card = NewImage("Icon_" + skin.label + "_" + chroma, iconsContent, chosen ? EquippedColor : MenuBuilder.CardColor);
        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.transition = Selectable.Transition.None;
        AddButtonFx(button, card);

        Image preview = NewImage("Preview", card.transform, owned ? Color.white : new Color(0.12f, 0.08f, 0.12f, 1f), false);
        Anchor(preview.rectTransform, 0.10f, 0.28f, 0.90f, 0.96f);
        preview.sprite = face;
        preview.preserveAspect = true;
        preview.raycastTarget = false;

        if (owned && chroma >= 0)
        {
            ShowPattern(preview, look.fx);
        }

        if (!owned)
        {
            Image padlock = NewImage("Lock", card.transform, ChestGoldColor, false);
            padlock.sprite = LockSprite();
            padlock.preserveAspect = true;
            padlock.raycastTarget = false;
            Anchor(padlock.rectTransform, 0.30f, 0.42f, 0.70f, 0.82f);
        }

        // nazwa: sama postać dla zwykłej buźki, postać i chroma dla pozostałych
        string title = Loc.SkinName(skin.label) + (chroma >= 0 ? "\n" + Loc.ChromaName(skin.ChromaList[chroma]) : "");
        TextMeshProUGUI name = NewText(card.transform, title, 26f, MenuBuilder.DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        AutoSize(name, 14f, 26f);
        Anchor(name.rectTransform, 0.03f, 0.02f, 0.97f, 0.27f);

        if (!owned)
        {
            name.alpha = 0.6f;
            return;
        }

        WireClick(button, () =>
        {
            PlayerAvatar.Set(skin.label, chroma);
            RebuildIcons();
        }, true);
    }

    // ---------- MISJE ----------

    void BuildMissionsPage(RectTransform main)
    {
        RectTransform page = EnsurePage(Page.Missions, "MISJE", main);
        missionsTitle = page.Find("Title").GetComponent<TextMeshProUGUI>();
        missionRows.Clear();

        for (int i = 0; i < DailyMissions.Count; i++)
        {
            float top = 0.73f - i * 0.185f;
            Transform existing = page.Find("Mission_" + i);

            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            Image card = NewImage("Mission_" + i, page, MenuBuilder.CardColor);
            Anchor(card.rectTransform, 0.14f, top - 0.165f, 0.86f, top);

            MissionRow row = new MissionRow();

            row.title = NewText(card.transform, "", 46f, MenuBuilder.DarkText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Anchor(row.title.rectTransform, 0.03f, 0.50f, 0.60f, 0.95f);
            AutoSize(row.title, 24f, 46f);

            Image track = NewImage("Track", card.transform, new Color(0.36f, 0.13f, 0.27f, 0.35f));
            Anchor(track.rectTransform, 0.03f, 0.14f, 0.60f, 0.44f);

            row.fill = NewImage("Fill", track.transform, MenuBuilder.OrangeColor);
            row.fill.raycastTarget = false;

            row.progress = NewText(track.transform, "", 32f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(row.progress.rectTransform);
            row.progress.outlineWidth = 0.25f;
            row.progress.outlineColor = new Color32(45, 20, 35, 255);

            row.reward = NewText(card.transform, "", 34f, AccentText, TextAlignmentOptions.Center, FontStyles.Bold);
            Anchor(row.reward.rectTransform, 0.61f, 0.08f, 0.79f, 0.92f);
            AutoSize(row.reward, 20f, 34f);

            int index = i;
            row.button = NewButton(card.transform, "", MenuBuilder.OrangeColor, () => ClaimMission(index), 38f, true);
            row.button.name = "Button_Claim";
            Anchor((RectTransform)row.button.transform, 0.80f, 0.16f, 0.98f, 0.84f);
            row.buttonImage = row.button.GetComponent<Image>();
            row.buttonLabel = row.button.GetComponentInChildren<TextMeshProUGUI>();
            AutoSize(row.buttonLabel, 18f, 38f);

            missionRows.Add(row);
        }

        Transform timer = page.Find("Timer");
        missionsTimer = timer != null ? timer.GetComponent<TextMeshProUGUI>() : null;

        if (missionsTimer == null)
        {
            missionsTimer = NewText(page, "", 34f, MenuBuilder.CardColor, TextAlignmentOptions.Center);
            missionsTimer.name = "Timer";
            Anchor(missionsTimer.rectTransform, 0.30f, 0.06f, 0.90f, 0.16f);
            missionsTimer.alpha = 0.85f;
        }
    }

    void ClaimMission(int index)
    {
        if (DailyMissions.Claim(index))
        {
            GameAudio.Play(AudioEvent.CoinGain);
        }

        RefreshMissions();
    }

    void RefreshMissions()
    {
        if (missionsTitle == null)
        {
            return;
        }

        missionsTitle.text = Loc.P("MISJE DZIENNE", "DAILY MISSIONS");
        DailyMission[] missions = DailyMissions.Today;

        for (int i = 0; i < missionRows.Count; i++)
        {
            MissionRow row = missionRows[i];
            bool exists = i < missions.Length;
            row.title.transform.parent.gameObject.SetActive(exists);

            if (!exists)
            {
                continue;
            }

            DailyMission mission = missions[i];
            MissionTemplate template = mission.template;

            row.title.text = template.Text;
            row.progress.text = mission.Progress + " / " + template.target;
            row.reward.text = "+" + template.coins + Loc.P(" monet", " coins") + "\n+" + template.xp + " XP";
            SetFill(row.fill, (float)mission.Progress / Mathf.Max(1, template.target));

            // przycisk: ODBIERZ dopiero po zrobieniu misji, potem ODEBRANO
            bool claimable = mission.Done && !mission.claimed;
            row.button.interactable = claimable;
            row.buttonImage.color = claimable ? MenuBuilder.OrangeColor : mission.claimed ? MenuBuilder.SwitchOnColor : MenuBuilder.DisabledColor;
            row.buttonLabel.text = mission.claimed ? Loc.P("ODEBRANO", "CLAIMED") : claimable ? Loc.P("ODBIERZ", "CLAIM") : Loc.P("W TOKU", "IN PROGRESS");
        }

        missionsTimerSecond = -1;
        TickMissionsTimer();
    }

    // Zegar "Nowe misje za 6:25:04": wołany co klatkę, gdy ekran MISJE jest otwarty, ale napis zmienia tylko raz na sekundę.
    // O północy licznik przeskakuje z 0:00:00 z powrotem na 23:59:59 - wtedy ekran sam ładuje nowe misje.
    void TickMissionsTimer()
    {
        if (missionsTimer == null)
        {
            return;
        }

        int seconds = Mathf.Max(0, (int)DailyMissions.TimeToReset.TotalSeconds);

        if (seconds == missionsTimerSecond)
        {
            return;
        }

        bool newDay = missionsTimerSecond >= 0 && seconds > missionsTimerSecond;
        missionsTimerSecond = seconds;

        if (newDay)
        {
            RefreshMissions();
            return;
        }

        // <mspace> daje cyfrom równą szerokość, więc napis nie skacze na boki przy zmianie sekundy
        string clock = (seconds / 3600) + ":" + ((seconds / 60) % 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        missionsTimer.text = Loc.P("Nowe misje za ", "New missions in ") + "<mspace=0.62em>" + clock + "</mspace>";
    }
}
