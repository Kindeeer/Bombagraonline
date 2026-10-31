using System;
using System.Collections.Generic;
using UnityEngine;

// Waga jednego przedmiotu w losowaniu (zaawansowane ustawienia poczekalni).
[Serializable]
public class PickupWeight
{
    public string id;
    public int blocks;
    public int fountain;
}

// Wszystkie ustawienia, które host wybiera w poczekalni. To jest też to, co później pójdzie przez sieć do pozostałych graczy:
// jedna prosta paczka liczb, zapisywana jako tekst (JSON).
[Serializable]
public class LobbySettings
{
    [Tooltip("-1 = losowa arena, 0 i więcej = konkretny układ.")]
    public int arena = -1;
    public float speed = 1f;
    public float size = 1f;
    public float dropChance = 1f;
    public float fuse = 1f;
    public int startBombs = 1;
    public int startRange = 2;
    public float blocks = 1f;
    [Tooltip("Po ilu sekundach włącza się CHAOS. Najwięcej = CHAOS wyłączony.")]
    public float chaosSeconds = 75f;
    public int bots = 3;
    public List<PickupWeight> weights = new List<PickupWeight>();

    public const float ChaosOff = 180f;

    public LobbySettings Clone()
    {
        return JsonUtility.FromJson<LobbySettings>(JsonUtility.ToJson(this));
    }

    public PickupWeight WeightOf(string id)
    {
        foreach (PickupWeight weight in weights)
        {
            if (weight.id == id)
            {
                return weight;
            }
        }

        return null;
    }

    // Waga przedmiotu: własna z ustawień, a gdy jej nie ma, ta z pliku przedmiotu.
    public int WeightFromBlocks(PickupDefinition item)
    {
        PickupWeight custom = WeightOf(item.name);
        return custom != null ? custom.blocks : item.weightFromBlocks;
    }

    public int WeightFromFountain(PickupDefinition item)
    {
        PickupWeight custom = WeightOf(item.name);
        return custom != null ? custom.fountain : item.weightFromFountain;
    }

    public void SetWeights(PickupDefinition item, int fromBlocks, int fromFountain)
    {
        PickupWeight custom = WeightOf(item.name);

        if (custom == null)
        {
            custom = new PickupWeight { id = item.name };
            weights.Add(custom);
        }

        custom.blocks = Mathf.Max(0, fromBlocks);
        custom.fountain = Mathf.Max(0, fromFountain);
    }
}

// Reguły aktualnie rozgrywanej rundy. Zwykła gra (Solo) używa ustawień domyślnych, a runda z poczekalni ustawień hosta.
// Reszta gry czyta te wartości (losowanie przedmiotów, plansza, statystyki graczy), więc nic poza tym plikiem
// nie musi wiedzieć, skąd pochodzą ustawienia.
public static class GameRules
{
    static LobbySettings current = new LobbySettings();

    // true, gdy runda gra według ustawień z poczekalni (wtedy startowe wartości graczy są nadpisywane).
    public static bool Custom { get; private set; }

    public static LobbySettings Current => current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        current = new LobbySettings();
        Custom = false;
    }

    public static void Use(LobbySettings settings)
    {
        current = settings.Clone();
        Custom = true;
    }

    public static void Reset()
    {
        current = new LobbySettings();
        Custom = false;
    }

    public static int WeightFromBlocks(PickupDefinition item)
    {
        return Custom ? current.WeightFromBlocks(item) : item.weightFromBlocks;
    }

    public static int WeightFromFountain(PickupDefinition item)
    {
        return Custom ? current.WeightFromFountain(item) : item.weightFromFountain;
    }

    public static float DropChanceMultiplier => Custom ? current.dropChance : 1f;

    // Wpisuje w postać startowe statystyki i rozmiar z ustawień (tylko w rundzie z poczekalni).
    public static void ApplyToPlayer(GameObject player, float extraSpeedMultiplier)
    {
        if (!Custom)
        {
            return;
        }

        PlayerStats stats = player.GetComponent<PlayerStats>();

        if (stats != null)
        {
            stats.maxBombs = Mathf.Clamp(current.startBombs, 1, stats.maxBombsLimit);
            stats.blastRange = Mathf.Clamp(current.startRange, 1, stats.blastRangeLimit);
            stats.fuseTime = Mathf.Clamp(stats.fuseTime * current.fuse, 0.4f, 8f);
            stats.minFuseTime = Mathf.Min(stats.minFuseTime, 0.4f);
            stats.speedMultiplier = current.speed * extraSpeedMultiplier;
            stats.RefreshSpeed();
        }

        PlayerVisual visual = player.GetComponentInChildren<PlayerVisual>();

        if (visual != null)
        {
            visual.SetBodyScale(current.size);
        }
    }
}

// Stan poczekalni (multiplayer): czy jesteśmy w poczekalni, kod pokoju i ustawienia hosta.
public static class LobbyState
{
    const string LastKey = "lobby_last";

    static LobbySettings settings;

    public static bool InLobby { get; private set; }
    public static bool IsHost { get; private set; }
    public static string Code { get; private set; } = "";

    // Krótka informacja do pokazania w poczekalni (np. że połączenie z internetem się nie udało).
    public static string Notice { get; set; }

    // Zasady hosta widziane przez pozostałych graczy (tylko do odczytu).
    public static LobbySettings RemoteRules { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        InLobby = false;
        IsHost = false;
        Code = "";
        Notice = null;
        RemoteRules = null;
        settings = null;
    }

    // Ustawienia pamiętane między poczekalniami (ostatnio użyte), żeby nie trzeba było ich ustawiać od nowa.
    public static LobbySettings Settings
    {
        get
        {
            if (settings == null)
            {
                string json = PlayerPrefs.GetString(LastKey, "");
                settings = string.IsNullOrEmpty(json) ? new LobbySettings() : JsonUtility.FromJson<LobbySettings>(json) ?? new LobbySettings();
            }

            return settings;
        }
    }

    public static void SaveLast()
    {
        PlayerPrefs.SetString(LastKey, JsonUtility.ToJson(Settings));
        PlayerPrefs.Save();
    }

    public static void ReplaceSettings(LobbySettings replacement)
    {
        settings = replacement;
        SaveLast();
    }

    // code z internetu (OnlineSession.Code) albo null: wtedy powstaje kod lokalny, który nie działa przez sieć (tryb offline).
    public static void BeginHost(string code)
    {
        InLobby = true;
        IsHost = true;
        Code = string.IsNullOrEmpty(code) ? RoomCode.Generate() : code;
    }

    public static void BeginClient(string code)
    {
        InLobby = true;
        IsHost = false;
        Code = code;
    }

    public static void End()
    {
        InLobby = false;
        IsHost = false;
        Code = "";
    }
}

// Kod pokoju: 6 znaków bez łatwych do pomylenia liter i cyfr (bez O, 0, I, 1).
public static class RoomCode
{
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int Length = 6;

    public static string Generate()
    {
        char[] code = new char[Length];

        for (int i = 0; i < Length; i++)
        {
            code[i] = Alphabet[UnityEngine.Random.Range(0, Alphabet.Length)];
        }

        return new string(code);
    }

    public static string Clean(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        foreach (char c in input.ToUpperInvariant())
        {
            if (Alphabet.IndexOf(c) >= 0 && builder.Length < Length)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}

// Zapisy ustawień poczekalni. Leżą na telefonie gracza, więc każdy ma własne.
public static class LobbyPresets
{
    public const int Slots = 6;
    const string Key = "lobby_presets";

    [Serializable]
    class PresetFile
    {
        public List<string> slots = new List<string>();
    }

    static PresetFile Read()
    {
        string json = PlayerPrefs.GetString(Key, "");
        PresetFile file = string.IsNullOrEmpty(json) ? new PresetFile() : JsonUtility.FromJson<PresetFile>(json) ?? new PresetFile();

        while (file.slots.Count < Slots)
        {
            file.slots.Add("");
        }

        return file;
    }

    public static bool Has(int slot)
    {
        return slot >= 0 && slot < Slots && !string.IsNullOrEmpty(Read().slots[slot]);
    }

    public static LobbySettings Load(int slot)
    {
        if (!Has(slot))
        {
            return null;
        }

        return JsonUtility.FromJson<LobbySettings>(Read().slots[slot]);
    }

    public static void Save(int slot, LobbySettings settings)
    {
        PresetFile file = Read();
        file.slots[slot] = JsonUtility.ToJson(settings);
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(file));
        PlayerPrefs.Save();
    }
}

// Nick gracza (zapamiętany na telefonie).
public static class PlayerProfile
{
    const string Key = "nick";
    public const int MaxLength = 12;

    static string nick;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        nick = null;
    }

    public static string Nick
    {
        get
        {
            if (nick == null)
            {
                nick = Clean(PlayerPrefs.GetString(Key, ""));

                if (nick.Length == 0)
                {
                    nick = Loc.P("Gracz", "Player") + UnityEngine.Random.Range(100, 1000);
                }
            }

            return nick;
        }
        set
        {
            string cleaned = Clean(value);

            if (cleaned.Length == 0)
            {
                return;
            }

            nick = cleaned;
            PlayerPrefs.SetString(Key, nick);
            PlayerPrefs.Save();
        }
    }

    // Litery (także polskie), cyfry, spacja, podkreślnik i myślnik. Maksymalnie 12 znaków.
    public static string Clean(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        foreach (char c in value)
        {
            if ((char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-') && builder.Length < MaxLength)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Trim();
    }
}
