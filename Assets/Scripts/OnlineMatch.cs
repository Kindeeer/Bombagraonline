using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;

// Gracz w rundzie online.
public class OnlineSlot
{
    public string id;
    public string nick;
    public string skin;
    public int chroma = -1;
    public bool isMe;
    // numer slotu w MatchManager (0 = ja, 1-3 = pozostali gracze)
    public int slot = -1;
    // numer rogu planszy, w którym ta osoba zaczyna (taki sam u wszystkich: kolejność z listy hosta)
    public int spawn;
    // drużyna w trybie 2 na 2 (0 = niebiescy, 1 = czerwoni, -1 = gra bez drużyn); rozdaje ją host
    public int team = Teams.None;
    // pierwszy na liście hosta to host
    public bool isHost;
}

// Runda online. Zasada jest prosta: każdy telefon liczy grę sam, ale wszyscy mają to samo.
// - Plansza jest taka sama, bo host losuje jedno ziarno (seed) i wszyscy generują arenę z tego samego ziarna.
// - Każdy steruje tylko własną postacią i wysyła jej pozycję. Cudze postacie są kopiami ruszanymi z sieci.
// - Postawienie bomby wysyła się wszystkim, a eksplozje, płomienie i niszczenie bloków każdy liczy u siebie tak samo.
// - Przedmioty losuje tylko host i mówi pozostałym, gdzie leżą.
// - Śmierć własnej postaci każdy ogłasza wszystkim, a koniec rundy ogłasza host.
//
// Wiadomości: bomba.start (host -> wszyscy), bomba.bomb i bomba.dead (każdy -> wszyscy, przez hosta),
// bomba.pickup i bomba.end (host -> wszyscy).
public static class OnlineMatch
{
    const string StartMessage = "bomba.start";
    const string BombMessage = "bomba.bomb";
    const string DeadMessage = "bomba.dead";
    const string PickupMessage = "bomba.pickup";
    const string EndMessage = "bomba.end";
    const string LeaveMessage = "bomba.leave";
    const string EmoteMessage = "bomba.emote";
    const string LobbyMessage = "bomba.lobby";
    const string KickMessage = "bomba.kick";
    const string CurseMessage = "bomba.curse";
    const string MineMessage = "bomba.mine";
    const string MineHitMessage = "bomba.minehit";

    public static bool Active { get; private set; }
    public static int Seed { get; private set; }
    public static LobbySettings Rules { get; private set; }
    public static readonly List<OnlineSlot> Players = new List<OnlineSlot>();

    // prefab bomby do stawiania bomb przychodzących z sieci (ustawia go MatchManager)
    public static Bomb BombPrefab { get; set; }

    // Emotka innego gracza odebrana w poczekalni (identyfikator gracza, emotka). Słucha tego LobbyController.
    public static event System.Action<string, Emote> LobbyEmote;

    static CustomMessagingManager handlersOn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active = false;
        Seed = 0;
        Rules = null;
        Players.Clear();
        BombPrefab = null;
        handlersOn = null;
        LobbyEmote = null;
    }

    public static string MyId => AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : "";

    public static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    // Chwila, w której zaczęła się ta runda (stany ruchu starsze od niej to jeszcze poczekalnia).
    public static float RoundStartedAt { get; private set; }

    // Czy przedmiot może wypaść w rundzie online. Przedmioty specjalne (atomówka, rakieta...) zależą od rzeczy,
    // których jeszcze nie przesyłamy przez sieć, więc online nie wypadają. Kopanie bomb, klątwa i mina mają własne wiadomości.
    public static bool AllowsPickup(PickupDefinition item)
    {
        return item != null && (item.special == SpecialItem.None || item.special == SpecialItem.Mine);
    }

    public static void End()
    {
        Active = false;
        Players.Clear();
        BombPrefab = null;
    }

    public static OnlineSlot SlotInfo(int slot)
    {
        foreach (OnlineSlot player in Players)
        {
            if (player.slot == slot)
            {
                return player;
            }
        }

        return null;
    }

    // Wygląd gracza z sieci (skin i chroma) dla slotu 1-3.
    public static PlayerSprites LookForSlot(GameArt art, int slot)
    {
        OnlineSlot info = SlotInfo(slot);

        if (info == null || art.SkinCount == 0)
        {
            return null;
        }

        int index = Mathf.Max(0, System.Array.FindIndex(art.players, p => p.label == info.skin));
        return art.SkinLook(index, info.chroma);
    }

    // ---------- Start rundy ----------

    // Host zaczyna rundę: losuje ziarno planszy, wysyła wszystkim graczy i zasady, i sam wczytuje arenę.
    public static void HostStart(LobbySettings rules)
    {
        List<OnlinePlayer> players = OnlineSession.Players();
        List<OnlineSlot> slots = new List<OnlineSlot>();

        foreach (OnlinePlayer player in players)
        {
            slots.Add(new OnlineSlot { id = player.id, nick = player.nick, skin = player.skin, chroma = player.chroma });
        }

        // tryb drużynowy: host rozdziela graczy według ich życzeń z poczekalni (drużyny zostają na kolejne rundy)
        if (rules.teams)
        {
            int[] wishes = new int[players.Count];

            for (int i = 0; i < players.Count; i++)
            {
                wishes[i] = players[i].team;
            }

            int[] teams = Teams.Assign(wishes);

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].team = teams[i];
            }
        }

        BeginAsHost(Random.Range(1, int.MaxValue), rules, slots);
    }

    // Kolejna runda w tym samym składzie (nowa plansza).
    public static void HostNextRound()
    {
        List<OnlineSlot> slots = new List<OnlineSlot>();

        foreach (OnlineSlot player in Players)
        {
            slots.Add(new OnlineSlot { id = player.id, nick = player.nick, skin = player.skin, chroma = player.chroma, team = player.team });
        }

        BeginAsHost(Random.Range(1, int.MaxValue), Rules, slots);
    }

    static void BeginAsHost(int seed, LobbySettings rules, List<OnlineSlot> slots)
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager != null && manager.IsListening)
        {
            using (FastBufferWriter writer = new FastBufferWriter(2048, Allocator.Temp, 8192))
            {
                writer.WriteValueSafe(seed);
                writer.WriteValueSafe(JsonUtility.ToJson(rules));
                writer.WriteValueSafe(slots.Count);

                foreach (OnlineSlot slot in slots)
                {
                    writer.WriteValueSafe(slot.id);
                    writer.WriteValueSafe(slot.nick);
                    writer.WriteValueSafe(slot.skin ?? "");
                    writer.WriteValueSafe(slot.chroma);
                    writer.WriteValueSafe(slot.team);
                }

                manager.CustomMessagingManager.SendNamedMessageToAll(StartMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        Begin(seed, rules, slots);
    }

    static void OnStart(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int seed);
        reader.ReadValueSafe(out string rulesJson);
        reader.ReadValueSafe(out int count);

        List<OnlineSlot> slots = new List<OnlineSlot>();

        for (int i = 0; i < count; i++)
        {
            OnlineSlot slot = new OnlineSlot();
            reader.ReadValueSafe(out slot.id);
            reader.ReadValueSafe(out slot.nick);
            reader.ReadValueSafe(out slot.skin);
            reader.ReadValueSafe(out slot.chroma);
            reader.ReadValueSafe(out slot.team);
            slots.Add(slot);
        }

        // host też dostaje własną wiadomość, ale już ją obsłużył
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            return;
        }

        Begin(seed, JsonUtility.FromJson<LobbySettings>(rulesJson), slots);
    }

    // Wspólny start dla wszystkich: zapamiętuje skład, ustawia zasady i wczytuje arenę.
    static void Begin(int seed, LobbySettings rules, List<OnlineSlot> slots)
    {
        string me = MyId;
        Active = true;
        Seed = seed;
        Rules = rules;
        Players.Clear();

        int nextSlot = 1;
        int[] teamSeats = new int[2];

        for (int i = 0; i < slots.Count; i++)
        {
            OnlineSlot slot = slots[i];
            slot.isMe = slot.id == me;
            slot.isHost = i == 0;
            slot.slot = slot.isMe ? 0 : nextSlot++;

            // w trybie drużynowym niebiescy startują w lewych rogach areny, a czerwoni w prawych
            bool inTeam = rules != null && rules.teams && slot.team >= 0 && slot.team < 2;
            slot.spawn = inTeam ? slot.team * 2 + (teamSeats[slot.team]++ % 2) : i;
            Players.Add(slot);
        }

        RoundStartedAt = Time.unscaledTime;
        LobbyNet.States.Clear();
        GameRules.Use(rules);
        MainMenu.SetBotCount(0);
        LobbyState.End();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ---------- Wiadomości w trakcie rundy ----------

    // Podpina odbiór wiadomości (wołać co klatkę, bo odbiór działa dopiero po starcie połączenia).
    public static void EnsureHandlers()
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null || manager.CustomMessagingManager == null || handlersOn == manager.CustomMessagingManager)
        {
            return;
        }

        handlersOn = manager.CustomMessagingManager;
        handlersOn.RegisterNamedMessageHandler(StartMessage, OnStart);
        handlersOn.RegisterNamedMessageHandler(BombMessage, OnBomb);
        handlersOn.RegisterNamedMessageHandler(DeadMessage, OnDead);
        handlersOn.RegisterNamedMessageHandler(PickupMessage, OnPickup);
        handlersOn.RegisterNamedMessageHandler(EndMessage, OnEnd);
        handlersOn.RegisterNamedMessageHandler(LeaveMessage, OnLeave);
        handlersOn.RegisterNamedMessageHandler(EmoteMessage, OnEmote);
        handlersOn.RegisterNamedMessageHandler(LobbyMessage, OnLobby);
        handlersOn.RegisterNamedMessageHandler(KickMessage, OnKick);
        handlersOn.RegisterNamedMessageHandler(CurseMessage, OnCurse);
        handlersOn.RegisterNamedMessageHandler(MineMessage, OnMine);
        handlersOn.RegisterNamedMessageHandler(MineHitMessage, OnMineHit);
    }

    // Wysyła wiadomość wszystkim: host od razu, klient przez hosta (host przekazuje ją dalej w handlerze).
    static void Relay(string name, FastBufferWriter writer)
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null || !manager.IsListening)
        {
            return;
        }

        if (manager.IsServer)
        {
            manager.CustomMessagingManager.SendNamedMessageToAll(name, writer, NetworkDelivery.ReliableSequenced);
        }
        else
        {
            manager.CustomMessagingManager.SendNamedMessage(name, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    // Host przekazuje wiadomość od klienta pozostałym klientom.
    static void Forward(string name, FastBufferWriter writer, ulong except)
    {
        NetworkManager manager = NetworkManager.Singleton;

        // wiadomość wysłana przez samego hosta doszła już do wszystkich (host dostaje też własne wiadomości),
        // więc nie przekazujemy jej drugi raz
        if (manager == null || !manager.IsServer || except == NetworkManager.ServerClientId)
        {
            return;
        }

        foreach (ulong client in manager.ConnectedClientsIds)
        {
            if (client != except && client != NetworkManager.ServerClientId)
            {
                manager.CustomMessagingManager.SendNamedMessage(name, client, writer, NetworkDelivery.ReliableSequenced);
            }
        }
    }

    // --- bomby ---

    public static void SendBomb(Vector2Int cell, int range, float fuse, bool piercing = false)
    {
        using (FastBufferWriter writer = PackBomb(MyId, cell, range, fuse, piercing))
        {
            Relay(BombMessage, writer);
        }
    }

    static FastBufferWriter PackBomb(string owner, Vector2Int cell, int range, float fuse, bool piercing)
    {
        FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
        writer.WriteValueSafe(owner);
        writer.WriteValueSafe(cell.x);
        writer.WriteValueSafe(cell.y);
        writer.WriteValueSafe(range);
        writer.WriteValueSafe(fuse);
        writer.WriteValueSafe(piercing);
        return writer;
    }

    static void OnBomb(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string owner);
        reader.ReadValueSafe(out int x);
        reader.ReadValueSafe(out int y);
        reader.ReadValueSafe(out int range);
        reader.ReadValueSafe(out float fuse);
        reader.ReadValueSafe(out bool piercing);

        Vector2Int cell = new Vector2Int(x, y);

        using (FastBufferWriter writer = PackBomb(owner, cell, range, fuse, piercing))
        {
            Forward(BombMessage, writer, sender);
        }

        if (!Active || owner == MyId || BombPrefab == null || GridMap.Instance == null || BombRegistry.HasBomb(cell))
        {
            return;
        }

        Bomb bomb = Object.Instantiate(BombPrefab);
        bomb.Init(cell, fuse, Mathf.Max(1, range));
        bomb.piercing = piercing;

        OnlineSlot ownerInfo = Players.Find(p => p.id == owner);
        bomb.ownerSlot = ownerInfo != null ? ownerInfo.slot : -1;
    }

    // --- śmierć ---

    public static void SendDead(string playerId)
    {
        using (FastBufferWriter writer = PackId(playerId))
        {
            Relay(DeadMessage, writer);
        }
    }

    static FastBufferWriter PackId(string id)
    {
        FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe(id);
        return writer;
    }

    static void OnDead(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string id);

        using (FastBufferWriter writer = PackId(id))
        {
            Forward(DeadMessage, writer, sender);
        }

        if (Active && MatchManager.Instance != null && id != MyId)
        {
            MatchManager.Instance.OnlineDead(id);
        }
    }

    // --- przedmioty (losuje tylko host) ---

    // bubble = true: przedmiot wylatuje z fontanny (bąbelek), false: leży od razu (wypadł z bloku).
    public static void SendPickup(string definitionName, Vector2Int cell, bool bubble)
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null || !manager.IsServer || !manager.IsListening)
        {
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp))
        {
            writer.WriteValueSafe(definitionName);
            writer.WriteValueSafe(cell.x);
            writer.WriteValueSafe(cell.y);
            writer.WriteValueSafe(bubble);
            manager.CustomMessagingManager.SendNamedMessageToAll(PickupMessage, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    static void OnPickup(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string definitionName);
        reader.ReadValueSafe(out int x);
        reader.ReadValueSafe(out int y);
        reader.ReadValueSafe(out bool bubble);

        if (Active && PickupSpawner.Instance != null)
        {
            PickupSpawner.Instance.SpawnFromNetwork(definitionName, new Vector2Int(x, y), bubble);
        }
    }

    // --- koniec rundy (ogłasza host) ---

    public static void SendEnd(string winnerId)
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null || !manager.IsServer || !manager.IsListening)
        {
            return;
        }

        using (FastBufferWriter writer = PackId(winnerId ?? ""))
        {
            manager.CustomMessagingManager.SendNamedMessageToAll(EndMessage, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    static void OnEnd(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string winnerId);

        if (Active && MatchManager.Instance != null && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            MatchManager.Instance.OnlineEnd(winnerId);
        }
    }

    // --- koniec meczu: powrót do poczekalni ---

    // Host (albo gra bez internetu) kończy mecz: wszyscy wracają do poczekalni, w której host może zmienić zasady i zacząć od nowa.
    public static void HostBackToLobby()
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (Active && manager != null && manager.IsServer && manager.IsListening)
        {
            using (FastBufferWriter writer = PackId(""))
            {
                manager.CustomMessagingManager.SendNamedMessageToAll(LobbyMessage, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        ReturnToLobby();
    }

    static void OnLobby(ulong sender, FastBufferReader reader)
    {
        if (Active && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            ReturnToLobby();
        }
    }

    // Kończy rundy, zeruje wygrane i wczytuje scenę od nowa już jako poczekalnię (połączenie i pokój zostają te same).
    static void ReturnToLobby()
    {
        bool host = IsHost;

        End();
        MatchManager.ResetScores();
        LobbyNet.States.Clear();
        GameRules.Reset();

        if (!OnlineSession.Connected)
        {
            LobbyState.BeginHost(null);
        }
        else if (host)
        {
            LobbyState.BeginHost(OnlineSession.Code);
        }
        else
        {
            LobbyState.BeginClient(OnlineSession.Code);
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // --- kopnięcie bomby ---

    public static void SendKick(Vector2Int cell, Vector2Int direction)
    {
        using (FastBufferWriter writer = PackKick(MyId, cell, direction))
        {
            Relay(KickMessage, writer);
        }
    }

    static FastBufferWriter PackKick(string id, Vector2Int cell, Vector2Int direction)
    {
        FastBufferWriter writer = new FastBufferWriter(96, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(cell.x);
        writer.WriteValueSafe(cell.y);
        writer.WriteValueSafe(direction.x);
        writer.WriteValueSafe(direction.y);
        return writer;
    }

    static void OnKick(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out int x);
        reader.ReadValueSafe(out int y);
        reader.ReadValueSafe(out int dx);
        reader.ReadValueSafe(out int dy);

        Vector2Int cell = new Vector2Int(x, y);
        Vector2Int direction = new Vector2Int(Mathf.Clamp(dx, -1, 1), Mathf.Clamp(dy, -1, 1));

        using (FastBufferWriter writer = PackKick(id, cell, direction))
        {
            Forward(KickMessage, writer, sender);
        }

        if (Active && id != MyId && BombRegistry.TryGet(cell, out Bomb bomb))
        {
            bomb.Kick(direction);
        }
    }

    // --- mina ---

    // Położyłem minę na tym polu.
    public static void SendMine(Vector2Int cell)
    {
        using (FastBufferWriter writer = PackCell(MyId, cell))
        {
            Relay(MineMessage, writer);
        }
    }

    // Wszedłem na minę na tym polu i wybuchła.
    public static void SendMineHit(Vector2Int cell)
    {
        using (FastBufferWriter writer = PackCell(MyId, cell))
        {
            Relay(MineHitMessage, writer);
        }
    }

    static FastBufferWriter PackCell(string id, Vector2Int cell)
    {
        FastBufferWriter writer = new FastBufferWriter(96, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(cell.x);
        writer.WriteValueSafe(cell.y);
        return writer;
    }

    static void OnMine(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out int x);
        reader.ReadValueSafe(out int y);

        Vector2Int cell = new Vector2Int(x, y);

        using (FastBufferWriter writer = PackCell(id, cell))
        {
            Forward(MineMessage, writer, sender);
        }

        OnlineSlot owner = Players.Find(p => p.id == id);

        if (Active && id != MyId && owner != null && BombPrefab != null)
        {
            Mine.Place(cell, owner.slot, BombPrefab);
        }
    }

    static void OnMineHit(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out int x);
        reader.ReadValueSafe(out int y);

        Vector2Int cell = new Vector2Int(x, y);

        using (FastBufferWriter writer = PackCell(id, cell))
        {
            Forward(MineHitMessage, writer, sender);
        }

        if (Active && id != MyId)
        {
            Mine.ExplodeAt(cell);
        }
    }

    // --- klątwa (odwrócone sterowanie) ---

    // Mój gracz podniósł klątwę i trafiła ona w postać victim (kopię cudzego gracza u mnie). Mówimy to wszystkim.
    public static void SendCurse(PlayerPickups victim, string definitionName)
    {
        MatchManager match = MatchManager.Instance;
        string targetId = "";

        if (match != null)
        {
            foreach (Combatant combatant in match.Slots)
            {
                if (combatant != null && combatant.pickups == victim)
                {
                    targetId = combatant.onlineId ?? "";
                }
            }
        }

        if (targetId.Length == 0)
        {
            return;
        }

        using (FastBufferWriter writer = PackCurse(MyId, targetId, definitionName))
        {
            Relay(CurseMessage, writer);
        }
    }

    static FastBufferWriter PackCurse(string senderId, string targetId, string definitionName)
    {
        FastBufferWriter writer = new FastBufferWriter(224, Allocator.Temp);
        writer.WriteValueSafe(senderId);
        writer.WriteValueSafe(targetId);
        writer.WriteValueSafe(definitionName);
        return writer;
    }

    static void OnCurse(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string senderId);
        reader.ReadValueSafe(out string targetId);
        reader.ReadValueSafe(out string definitionName);

        using (FastBufferWriter writer = PackCurse(senderId, targetId, definitionName))
        {
            Forward(CurseMessage, writer, sender);
        }

        MatchManager match = MatchManager.Instance;

        // ten, kto rzucił klątwę, pokazał ją już u siebie
        if (!Active || match == null || PickupSpawner.Instance == null || senderId == MyId)
        {
            return;
        }

        PickupDefinition definition = PickupSpawner.Instance.FindDefinition(definitionName);

        foreach (Combatant combatant in match.Slots)
        {
            if (combatant == null || combatant.onlineId != targetId || !combatant.IsAlive || combatant.pickups == null)
            {
                continue;
            }

            // fioletowy okrąg i efekt widzą wszyscy (u innych to tylko wygląd kopii gracza),
            // a naprawdę klątwa działa na telefonie trafionego gracza, bo tylko on steruje swoją postacią
            PulseRing.Spawn(combatant.pickups.transform.position, new Color(0.78f, 0.35f, 1f, 1f));

            if (definition != null)
            {
                combatant.pickups.Apply(definition);
            }
        }
    }

    // --- emotki ---

    public static void SendEmote(int emote)
    {
        using (FastBufferWriter writer = PackEmote(MyId, emote))
        {
            Relay(EmoteMessage, writer);
        }
    }

    static FastBufferWriter PackEmote(string id, int emote)
    {
        FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(emote);
        return writer;
    }

    static void OnEmote(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out int emote);

        using (FastBufferWriter writer = PackEmote(id, emote))
        {
            Forward(EmoteMessage, writer, sender);
        }

        // w poczekalni nie ma jeszcze slotów rundy: emotkę pokazuje poczekalnia nad postacią tego gracza
        if (!Active && LobbyState.InLobby && id != MyId && emote >= 0 && emote < Emotes.Count)
        {
            LobbyEmote?.Invoke(id, (Emote)emote);
            return;
        }

        OnlineSlot slot = Players.Find(p => p.id == id);

        if (Active && id != MyId && slot != null && slot.slot > 0 && emote >= 0 && emote < Emotes.Count)
        {
            Emotes.Show(slot.slot, (Emote)emote);
        }
    }

    // --- wyjście z gry (Quit) ---

    // Mówi pozostałym, że wychodzę. Wołać tuż przed opuszczeniem sesji.
    public static void SendLeave()
    {
        if (!Active)
        {
            return;
        }

        using (FastBufferWriter writer = PackId(MyId))
        {
            Relay(LeaveMessage, writer);
        }
    }

    static void OnLeave(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string id);

        using (FastBufferWriter writer = PackId(id))
        {
            Forward(LeaveMessage, writer, sender);
        }

        if (Active && id != MyId)
        {
            PlayerLeft(id);
        }
    }

    // Ktoś wyszedł (wiadomość albo zniknął z sesji): znika z areny i z następnych rund. Gdy wyszedł host, kończymy grę.
    public static void PlayerLeft(string id)
    {
        OnlineSlot slot = Players.Find(p => p.id == id);

        if (slot == null)
        {
            return;
        }

        bool hostLeft = slot.isHost && !IsHost;
        Players.Remove(slot);

        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.OnlineLeft(id);
        }

        if (hostLeft)
        {
            MainMenu.ReturnToMenu();
        }
    }
}
