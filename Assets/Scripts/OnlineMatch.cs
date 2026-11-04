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

    public static bool Active { get; private set; }
    public static int Seed { get; private set; }
    public static LobbySettings Rules { get; private set; }
    public static readonly List<OnlineSlot> Players = new List<OnlineSlot>();

    // prefab bomby do stawiania bomb przychodzących z sieci (ustawia go MatchManager)
    public static Bomb BombPrefab { get; set; }

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
    }

    public static string MyId => AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : "";

    public static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    // Chwila, w której zaczęła się ta runda (stany ruchu starsze od niej to jeszcze poczekalnia).
    public static float RoundStartedAt { get; private set; }

    // Czy przedmiot może wypaść w rundzie online. Przedmioty specjalne (atomówka, rakieta...), kopanie bomb i klątwa
    // odwracająca sterowanie zależą od rzeczy, których jeszcze nie przesyłamy przez sieć, więc online nie wypadają.
    public static bool AllowsPickup(PickupDefinition item)
    {
        return item != null
            && item.special == SpecialItem.None
            && item.effect != PickupEffect.Kick
            && item.effect != PickupEffect.ReverseControls;
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

        BeginAsHost(Random.Range(1, int.MaxValue), rules, slots);
    }

    // Kolejna runda w tym samym składzie (nowa plansza).
    public static void HostNextRound()
    {
        List<OnlineSlot> slots = new List<OnlineSlot>();

        foreach (OnlineSlot player in Players)
        {
            slots.Add(new OnlineSlot { id = player.id, nick = player.nick, skin = player.skin, chroma = player.chroma });
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

        for (int i = 0; i < slots.Count; i++)
        {
            OnlineSlot slot = slots[i];
            slot.isMe = slot.id == me;
            slot.slot = slot.isMe ? 0 : nextSlot++;
            slot.spawn = i;
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

        if (manager == null || !manager.IsServer)
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

    public static void SendBomb(Vector2Int cell, int range, float fuse)
    {
        using (FastBufferWriter writer = PackBomb(MyId, cell, range, fuse))
        {
            Relay(BombMessage, writer);
        }
    }

    static FastBufferWriter PackBomb(string owner, Vector2Int cell, int range, float fuse)
    {
        FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
        writer.WriteValueSafe(owner);
        writer.WriteValueSafe(cell.x);
        writer.WriteValueSafe(cell.y);
        writer.WriteValueSafe(range);
        writer.WriteValueSafe(fuse);
        return writer;
    }

    static void OnBomb(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string owner);
        reader.ReadValueSafe(out int x);
        reader.ReadValueSafe(out int y);
        reader.ReadValueSafe(out int range);
        reader.ReadValueSafe(out float fuse);

        Vector2Int cell = new Vector2Int(x, y);

        using (FastBufferWriter writer = PackBomb(owner, cell, range, fuse))
        {
            Forward(BombMessage, writer, sender);
        }

        if (!Active || owner == MyId || BombPrefab == null || GridMap.Instance == null || BombRegistry.HasBomb(cell))
        {
            return;
        }

        Bomb bomb = Object.Instantiate(BombPrefab);
        bomb.Init(cell, fuse, Mathf.Max(1, range));

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

        bool hostLeft = slot.spawn == 0 && !IsHost;
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
