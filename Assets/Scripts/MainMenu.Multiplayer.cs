using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiKit;

// Multiplayer w menu: strona z nickiem i wyborem HOST / JOIN oraz strona wpisywania kodu pokoju.
// HOST tworzy poczekalnię (patrz LobbyController) i kod pokoju. JOIN przyjmie kod od hosta, ale łączenie przez internet
// dojdzie w następnym kroku (potrzebny jest serwer pośredniczący), więc na razie pokazuje o tym informację.
public partial class MainMenu
{
    TMP_InputField nickField;
    TMP_InputField joinField;
    TextMeshProUGUI multiTitle;
    TextMeshProUGUI nickLabel;
    TextMeshProUGUI multiInfo;
    TextMeshProUGUI hostLabel;
    TextMeshProUGUI joinLabel;
    TextMeshProUGUI joinTitle;
    TextMeshProUGUI joinHint;
    TextMeshProUGUI joinStatus;
    TextMeshProUGUI joinGoLabel;

    // Liczba botów w następnej rundzie ustawiona z poczekalni (0-3, bez zapisywania na telefonie).
    public static void SetBotCount(int count)
    {
        botCount = Mathf.Clamp(count, 0, MaxBots);
    }

    void BuildMultiplayerPages()
    {
        if (!pages.TryGetValue(Page.Mode, out RectTransform mode))
        {
            return;
        }

        Transform parent = mode.parent;

        if (!pages.ContainsKey(Page.Multi))
        {
            BuildMultiPage(parent);
        }

        if (!pages.ContainsKey(Page.Join))
        {
            BuildJoinPage(parent);
        }

        pages[Page.Multi].gameObject.SetActive(false);
        pages[Page.Join].gameObject.SetActive(false);
    }

    void BuildMultiPage(Transform parent)
    {
        RectTransform page = MenuBuilder.NewPage("Page_Multi", parent);
        MenuBuilder.AddTitle(page, "MULTIPLAYER", 130f);
        pages[Page.Multi] = page;
        multiTitle = page.Find("Title").GetComponent<TextMeshProUGUI>();

        nickLabel = NewText(page, "", 46f, MenuBuilder.CardColor, TextAlignmentOptions.Center, FontStyles.Bold);
        Anchor(nickLabel.rectTransform, 0.25f, 0.60f, 0.75f, 0.68f);

        nickField = NewInputField(page, "Nick", 54f, PlayerProfile.MaxLength);
        Anchor((RectTransform)nickField.transform, 0.32f, 0.47f, 0.68f, 0.59f);
        nickField.onEndEdit.AddListener(value =>
        {
            PlayerProfile.Nick = value;
            nickField.text = PlayerProfile.Nick;
        });

        RectTransform column = MenuBuilder.AddColumn(page, 760f, 330f, -215f);
        Button host = MenuBuilder.AddMenuButton(column, "Button_Host", "HOST", MenuBuilder.OrangeColor, 150f, 76f);
        Button join = MenuBuilder.AddMenuButton(column, "Button_JoinPage", "JOIN", MenuBuilder.ChocolateColor, 150f, 76f);
        hostLabel = host.GetComponentInChildren<TextMeshProUGUI>();
        joinLabel = join.GetComponentInChildren<TextMeshProUGUI>();
        WireClick(host, HostClicked, true);
        WireClick(join, () =>
        {
            PlayerProfile.Nick = nickField.text;
            ShowPage(Page.Join);
        }, true);

        multiInfo = NewText(page, "", 34f, MenuBuilder.CardColor, TextAlignmentOptions.Center);
        Anchor(multiInfo.rectTransform, 0.20f, 0.04f, 0.80f, 0.19f);
        multiInfo.alpha = 0.85f;

        MenuBuilder.AddBackButton(page);
    }

    void BuildJoinPage(Transform parent)
    {
        RectTransform page = MenuBuilder.NewPage("Page_Join", parent);
        MenuBuilder.AddTitle(page, "JOIN", 130f);
        pages[Page.Join] = page;
        joinTitle = page.Find("Title").GetComponent<TextMeshProUGUI>();

        joinHint = NewText(page, "", 44f, MenuBuilder.CardColor, TextAlignmentOptions.Center, FontStyles.Bold);
        Anchor(joinHint.rectTransform, 0.20f, 0.60f, 0.80f, 0.69f);

        joinField = NewInputField(page, "ABC123", 80f, RoomCode.Length);
        Anchor((RectTransform)joinField.transform, 0.32f, 0.45f, 0.68f, 0.59f);
        joinField.onValueChanged.AddListener(value =>
        {
            string cleaned = RoomCode.Clean(value);

            if (cleaned != value)
            {
                joinField.text = cleaned;
            }
        });

        Button go = NewButton(page, "", MenuBuilder.OrangeColor, JoinClicked, 66f, true);
        go.name = "Button_JoinGo";
        Anchor((RectTransform)go.transform, 0.34f, 0.28f, 0.66f, 0.40f);
        joinGoLabel = go.GetComponentInChildren<TextMeshProUGUI>();

        joinStatus = NewText(page, "", 38f, new Color(1f, 0.82f, 0.45f), TextAlignmentOptions.Center);
        Anchor(joinStatus.rectTransform, 0.15f, 0.08f, 0.85f, 0.26f);

        MenuBuilder.AddBackButton(page);
    }

    void RefreshMultiplayer()
    {
        if (nickField == null)
        {
            return;
        }

        multiTitle.text = "MULTIPLAYER";
        nickLabel.text = Loc.P("TWÓJ NICK", "YOUR NICKNAME");
        nickField.text = PlayerProfile.Nick;
        hostLabel.text = "HOST";
        joinLabel.text = Loc.P("DOŁĄCZ", "JOIN");
        multiInfo.text = Loc.P(
            "HOST tworzy poczekalnię i dostaje kod. Kolega wpisuje ten kod po wybraniu DOŁĄCZ.",
            "HOST creates a lobby and gets a code. A friend types that code after choosing JOIN.");

        joinTitle.text = Loc.P("DOŁĄCZ", "JOIN");
        joinHint.text = Loc.P("WPISZ KOD OD HOSTA", "ENTER THE HOST'S CODE");
        joinGoLabel.text = Loc.P("DOŁĄCZ", "JOIN");
        joinStatus.text = "";
    }

    bool connecting;

    async void HostClicked()
    {
        if (connecting)
        {
            return;
        }

        connecting = true;
        PlayerProfile.Nick = nickField.text;
        multiInfo.text = Loc.P("Tworzenie poczekalni...", "Creating the lobby...");

        GameRules.Reset();
        string error = await OnlineSession.HostAsync(LobbyState.Settings);
        connecting = false;

        if (this == null)
        {
            return;
        }

        // bez internetu poczekalnia działa offline (kod lokalny nie połączy innych graczy)
        LobbyState.Notice = error != null
            ? Loc.P("Brak połączenia z internetem, poczekalnia offline: ", "No internet connection, offline lobby: ") + error
            : null;
        LobbyState.BeginHost(error == null ? OnlineSession.Code : null);

        EnterLobby();
    }

    void EnterLobby()
    {
        GameStarted = true;
        GameAudio.PlayMusic(MusicTrack.Game);
        Close();
    }

    async void JoinClicked()
    {
        if (connecting)
        {
            return;
        }

        string code = RoomCode.Clean(joinField.text);

        if (code.Length < RoomCode.Length)
        {
            joinStatus.text = Loc.P("Kod ma " + RoomCode.Length + " znaków.", "The code has " + RoomCode.Length + " characters.");
            return;
        }

        connecting = true;
        joinStatus.text = Loc.P("Łączenie...", "Connecting...");
        GameRules.Reset();

        string error = await OnlineSession.JoinAsync(code);
        connecting = false;

        if (this == null)
        {
            return;
        }

        if (error != null)
        {
            joinStatus.text = Loc.P("Nie udało się dołączyć: ", "Could not join: ") + error;
            return;
        }

        LobbyState.Notice = null;
        LobbyState.BeginClient(OnlineSession.Code);
        EnterLobby();
    }
}
