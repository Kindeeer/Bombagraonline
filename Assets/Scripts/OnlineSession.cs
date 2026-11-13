using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

// Jeden gracz poczekalni widziany przez sieć (nick, skin, czy to host).
public class OnlinePlayer
{
    public string id;
    public string nick;
    public string skin;
    public int chroma = -1;
    // drużyna wybrana w poczekalni: 0 = obojętnie, 1 = niebiescy, 2 = czerwoni
    public int team;
    public bool isHost;
    public bool isMe;

    // Wygląd tego gracza (skin z założoną chromą) albo null, gdy nie ma skinów.
    public PlayerSprites Look()
    {
        GameArt art = GameArt.Instance;

        if (art == null || art.SkinCount == 0)
        {
            return null;
        }

        int index = Mathf.Max(0, System.Array.FindIndex(art.players, p => p.label == skin));
        return art.SkinLook(index, chroma);
    }

    public Sprite Portrait()
    {
        PlayerSprites look = Look();
        return look != null ? look.Portrait : null;
    }

    // Ja sam, bez internetu (poczekalnia offline).
    public static OnlinePlayer Local()
    {
        GameArt art = GameArt.Instance;
        int index = art != null && art.SkinCount > 0 ? Mathf.Clamp(SkinSelection.Index, 0, art.SkinCount - 1) : 0;
        string label = art != null && art.SkinCount > 0 ? art.players[index].label : "";

        return new OnlinePlayer
        {
            id = "local",
            nick = PlayerProfile.Nick,
            skin = label,
            chroma = label.Length > 0 ? PlayerWallet.Equipped(label) : -1,
            isHost = true,
            isMe = true
        };
    }
}

// Połączenie przez internet: logowanie anonimowe, założenie pokoju (HOST) albo dołączenie po kodzie (JOIN).
// Używa Unity Multiplayer Services (sesja = pokój z kodem). Sesja przechowuje listę graczy z ich nickami i skinami oraz
// zasady gry hosta, więc wszyscy w poczekalni widzą to samo. Ruch postaci i bomby po sieci dojdą w następnym kroku.
//
// Metody zwracają null, gdy się udało, a w razie błędu krótki opis (np. brak internetu), który pokazujemy graczowi.
public static class OnlineSession
{
    const string RulesKey = "rules";
    const string NickKey = "nick";
    const string SkinKey = "skin";
    const string ChromaKey = "chroma";
    const string TeamKey = "team";

    static ISession session;
    static bool refreshNeeded;

    public static bool Connected => session != null;

    public static string Code => session != null ? session.Code : "";

    public static bool IsHost => session != null && session.IsHost;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        session = null;
        refreshNeeded = false;
        ended = false;
        signInTask = null;
        regionTask = null;
        bestRegion = null;
    }

    // ---------- Rozgrzewka (żeby HOST i JOIN działały szybciej) ----------

    // Logowanie i pomiar najbliższego serwera trwają kilka sekund. Robimy je w tle zaraz po otwarciu menu,
    // więc zanim gracz kliknie HOST albo JOIN, zwykle są już gotowe i zostaje samo założenie pokoju.
    const string RegionKey = "relay_region";
    const string RegionDayKey = "relay_region_day";
    const int RegionValidDays = 3;

    static Task<string> signInTask;
    static Task regionTask;
    static string bestRegion;

    public static void Warmup()
    {
        _ = WarmupAsync();
    }

    static async Task WarmupAsync()
    {
        if (await SignInOnceAsync() == null)
        {
            await FindRegionOnceAsync();
        }
    }

    // Jedno logowanie naraz: kliknięcie HOST w trakcie rozgrzewki czeka na to samo logowanie, zamiast zaczynać drugie.
    static Task<string> SignInOnceAsync()
    {
        if (signInTask == null || (signInTask.IsCompleted && (signInTask.IsFaulted || signInTask.Result != null)))
        {
            signInTask = SignInAsync();
        }

        return signInTask;
    }

    static Task FindRegionOnceAsync()
    {
        if (regionTask == null)
        {
            regionTask = FindRegionAsync();
        }

        return regionTask;
    }

    // Najbliższy serwer Relay (ten z najmniejszym opóźnieniem). Bez tego pomiar robiłby się dopiero po kliknięciu HOST.
    static async Task FindRegionAsync()
    {
        try
        {
            List<string> ids = new List<string>();

            foreach (Unity.Services.Relay.Models.Region region in await Unity.Services.Relay.RelayService.Instance.ListRegionsAsync())
            {
                ids.Add(region.Id);
            }

            IList<Unity.Services.Qos.IQosResult> results = await Unity.Services.Qos.QosService.Instance.GetSortedQosResultsAsync("relay", ids);

            if (results != null && results.Count > 0 && !string.IsNullOrEmpty(results[0].Region))
            {
                bestRegion = results[0].Region;
                PlayerPrefs.SetString(RegionKey, bestRegion);
                PlayerPrefs.SetInt(RegionDayKey, Today());
                PlayerPrefs.Save();
            }
        }
        catch (Exception exception)
        {
            Debug.Log("OnlineSession: pomiar serwerów nieudany, wybierze je sama usługa: " + exception.Message);
        }
    }

    static int Today()
    {
        return (int)(DateTime.UtcNow - new DateTime(2020, 1, 1)).TotalDays;
    }

    // Zmierzony teraz albo zapamiętany z ostatnich dni. null = usługa zmierzy sama (wolniej).
    static string KnownRegion()
    {
        if (!string.IsNullOrEmpty(bestRegion))
        {
            return bestRegion;
        }

        string saved = PlayerPrefs.GetString(RegionKey, "");
        return saved.Length > 0 && Today() - PlayerPrefs.GetInt(RegionDayKey, 0) <= RegionValidDays ? saved : null;
    }

    // true raz po każdej zmianie w sesji (ktoś dołączył, wyszedł, zmienił skin albo host zmienił zasady).
    public static bool TakeRefresh()
    {
        bool value = refreshNeeded;
        refreshNeeded = false;
        return value;
    }

    // ---------- Logowanie ----------

    static async Task<string> SignInAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                InitializationOptions options = new InitializationOptions();

#if UNITY_EDITOR
                // kilka edytorów naraz (Multiplayer Play Mode) nie może dzielić jednego logowania, bo wyglądałyby jak ten sam gracz,
                // więc każdy dostaje własny profil zależny od ścieżki projektu
                options.SetProfile("ed" + Mathf.Abs(Application.dataPath.GetHashCode()));
#endif

                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            return null;
        }
        catch (Exception exception)
        {
            return Short(exception);
        }
    }

    static string Short(Exception exception)
    {
        string message = exception.Message ?? "";
        return message.Length > 140 ? message.Substring(0, 140) + "..." : message;
    }

    // ---------- Pokój ----------

    // Zakłada pokój i zapisuje w nim zasady hosta. Kod pokoju jest potem w OnlineSession.Code.
    public static async Task<string> HostAsync(LobbySettings rules)
    {
        string error = await SignInOnceAsync();

        if (error != null)
        {
            return error;
        }

        LobbyNet.EnsureManager();
        string region = KnownRegion();

        try
        {
            Attach(await CreateAsync(rules, region));
            return null;
        }
        catch (Exception exception)
        {
            if (region == null)
            {
                return Short(exception);
            }
        }

        // zapamiętany serwer mógł zniknąć: zapominamy go i próbujemy jeszcze raz z wyborem po stronie usługi
        bestRegion = null;
        PlayerPrefs.DeleteKey(RegionKey);

        try
        {
            Attach(await CreateAsync(rules, null));
            return null;
        }
        catch (Exception exception)
        {
            return Short(exception);
        }
    }

    static Task<IHostSession> CreateAsync(LobbySettings rules, string region)
    {
        SessionOptions options = new SessionOptions
        {
            MaxPlayers = 4,
            IsPrivate = true
        }.WithRelayNetwork(region);

        options.PlayerProperties = MyProperties();
        options.SessionProperties[RulesKey] = new SessionProperty(JsonUtility.ToJson(rules), VisibilityPropertyOptions.Member);
        return MultiplayerService.Instance.CreateSessionAsync(options);
    }

    public static async Task<string> JoinAsync(string code)
    {
        string error = await SignInOnceAsync();

        if (error != null)
        {
            return error;
        }

        try
        {
            LobbyNet.EnsureManager();

            // nick, skin i chroma lecą razem z dołączeniem (bez osobnego zapisu po wejściu)
            JoinSessionOptions options = new JoinSessionOptions { PlayerProperties = MyProperties() };
            ISession joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options);
            Attach(joined);
            return null;
        }
        catch (Exception exception)
        {
            return Short(exception);
        }
    }

    static Dictionary<string, PlayerProperty> MyProperties()
    {
        GameArt art = GameArt.Instance;
        int skinIndex = art != null ? Mathf.Clamp(SkinSelection.Index, 0, Mathf.Max(0, art.SkinCount - 1)) : 0;
        string skin = art != null && art.SkinCount > 0 ? art.players[skinIndex].label : "";
        int chroma = skin.Length > 0 ? PlayerWallet.Equipped(skin) : -1;

        return new Dictionary<string, PlayerProperty>
        {
            { NickKey, new PlayerProperty(PlayerProfile.Nick, VisibilityPropertyOptions.Member) },
            { SkinKey, new PlayerProperty(skin, VisibilityPropertyOptions.Member) },
            { ChromaKey, new PlayerProperty(chroma.ToString(), VisibilityPropertyOptions.Member) },
            { TeamKey, new PlayerProperty(LobbyState.MyTeam.ToString(), VisibilityPropertyOptions.Member) }
        };
    }

    // Wysyła pozostałym moje dane po zmianie (np. po wybraniu drużyny w poczekalni).
    public static async void PublishMyData()
    {
        await SaveMyDataAsync();
    }

    static async Task SaveMyDataAsync()
    {
        if (session == null)
        {
            return;
        }

        try
        {
            foreach (KeyValuePair<string, PlayerProperty> property in MyProperties())
            {
                session.CurrentPlayer.SetProperty(property.Key, property.Value);
            }

            await session.SaveCurrentPlayerDataAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("OnlineSession: nie udało się zapisać danych gracza: " + exception.Message);
        }
    }

    // Host wysyła zasady do pozostałych graczy.
    public static async void PublishRules(LobbySettings rules)
    {
        if (session == null || !session.IsHost)
        {
            return;
        }

        try
        {
            IHostSession host = session.AsHost();
            host.SetProperty(RulesKey, new SessionProperty(JsonUtility.ToJson(rules), VisibilityPropertyOptions.Member));
            await host.SavePropertiesAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("OnlineSession: nie udało się wysłać zasad: " + exception.Message);
        }
    }

    // Zasady hosta (dla pozostałych graczy). null, gdy ich jeszcze nie ma.
    public static LobbySettings ReadRules()
    {
        if (session != null && session.Properties.TryGetValue(RulesKey, out SessionProperty property) && !string.IsNullOrEmpty(property.Value))
        {
            return JsonUtility.FromJson<LobbySettings>(property.Value);
        }

        return null;
    }

    // Lista graczy w pokoju (host jest pierwszy).
    public static List<OnlinePlayer> Players()
    {
        List<OnlinePlayer> list = new List<OnlinePlayer>();

        if (session == null)
        {
            return list;
        }

        string myId = AuthenticationService.Instance.PlayerId;

        foreach (IReadOnlyPlayer player in session.Players)
        {
            OnlinePlayer entry = new OnlinePlayer
            {
                id = player.Id,
                isHost = player.Id == session.Host,
                isMe = player.Id == myId,
                nick = ValueOf(player, NickKey, "?"),
                skin = ValueOf(player, SkinKey, "")
            };

            int.TryParse(ValueOf(player, ChromaKey, "-1"), out entry.chroma);
            int.TryParse(ValueOf(player, TeamKey, "0"), out entry.team);
            list.Add(entry);
        }

        list.Sort((a, b) => b.isHost.CompareTo(a.isHost));
        return list;
    }

    static string ValueOf(IReadOnlyPlayer player, string key, string fallback)
    {
        return player.Properties != null && player.Properties.TryGetValue(key, out PlayerProperty property) && property.Value != null
            ? property.Value
            : fallback;
    }

    // ---------- Wyjście ----------

    public static async void LeaveAsync()
    {
        ISession current = session;
        session = null;
        LobbyNet.States.Clear();

        if (current == null)
        {
            return;
        }

        Detach(current);

        try
        {
            if (current.IsHost)
            {
                await current.AsHost().DeleteAsync();
            }
            else
            {
                await current.LeaveAsync();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("OnlineSession: wyjście z pokoju: " + exception.Message);
        }
    }

    // ---------- Zdarzenia sesji ----------

    static void Attach(ISession newSession)
    {
        session = newSession;
        refreshNeeded = true;
        session.Changed += OnSessionChanged;
        session.PlayerJoined += OnPlayer;
        session.PlayerHasLeft += OnPlayer;
        session.PlayerPropertiesChanged += OnSessionChanged;
        session.SessionPropertiesChanged += OnSessionChanged;
        session.RemovedFromSession += OnSessionEnded;
        session.Deleted += OnSessionEnded;
    }

    static void Detach(ISession oldSession)
    {
        oldSession.Changed -= OnSessionChanged;
        oldSession.PlayerJoined -= OnPlayer;
        oldSession.PlayerHasLeft -= OnPlayer;
        oldSession.PlayerPropertiesChanged -= OnSessionChanged;
        oldSession.SessionPropertiesChanged -= OnSessionChanged;
        oldSession.RemovedFromSession -= OnSessionEnded;
        oldSession.Deleted -= OnSessionEnded;
    }

    static void OnSessionChanged()
    {
        refreshNeeded = true;
    }

    static void OnPlayer(string playerId)
    {
        refreshNeeded = true;
    }

    // Host zamknął pokój albo wyrzucono nas z niego.
    static void OnSessionEnded()
    {
        ended = true;
    }

    static bool ended;

    public static bool TakeEnded()
    {
        bool value = ended;
        ended = false;

        if (value)
        {
            session = null;
        }

        return value;
    }
}
