using System;
using UnityEngine;

// Liczniki, które gra prowadzi dla gracza (ekran PROFIL).
public enum PlayerStat
{
    RoundsPlayed,
    RoundsWon,
    PlayerKills,
    BotKills,
    Deaths,
    BombsPlaced,
    PickupsCollected,
    CoinsEarned,
    WinStreak,
    BestWinStreak,
    OnlineRounds
}

// Co gracz dostał za ostatnią rundę (pokazuje to okienko wyników).
public struct RoundProgress
{
    public int xp;
    public int levelBefore;
    public int levelAfter;
    public int levelCoins;

    public bool LeveledUp => levelAfter > levelBefore;
}

// Postęp gracza: statystyki, doświadczenie i poziom konta. Wszystko zapisane na urządzeniu (PlayerPrefs), tak jak monety.
//
// Doświadczenie (XP) dostaje się za rozegrane rundy, wygrane i zabicia. Poziom wynika z sumy zdobytego XP:
// każdy kolejny poziom wymaga trochę więcej niż poprzedni. Za każdy nowy poziom wpadają monety, a co piąty poziom większa premia.
public static class PlayerProgress
{
    public const int XpForRound = 10;
    public const int XpForWin = 20;
    public const int XpForPlayerKill = 6;
    public const int XpForBotKill = 2;

    const string StatPrefix = "stat_";
    const string XpKey = "xp_total";
    const string SkinRoundsPrefix = "skin_rounds_";

    static int[] stats;
    static int totalXp = -1;

    public static RoundProgress LastRound { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        stats = null;
        totalXp = -1;
        LastRound = default;
    }

    // ---------- Statystyki ----------

    static int[] Stats
    {
        get
        {
            if (stats == null)
            {
                Array kinds = Enum.GetValues(typeof(PlayerStat));
                stats = new int[kinds.Length];

                foreach (PlayerStat stat in kinds)
                {
                    stats[(int)stat] = PlayerPrefs.GetInt(StatPrefix + stat, 0);
                }
            }

            return stats;
        }
    }

    public static int Get(PlayerStat stat)
    {
        return Stats[(int)stat];
    }

    public static void Add(PlayerStat stat, int amount = 1)
    {
        Set(stat, Get(stat) + amount);
    }

    static void Set(PlayerStat stat, int value)
    {
        Stats[(int)stat] = Mathf.Max(0, value);
        PlayerPrefs.SetInt(StatPrefix + stat, Stats[(int)stat]);
    }

    // Procent wygranych rund (0-100).
    public static int WinPercent
    {
        get
        {
            int played = Get(PlayerStat.RoundsPlayed);
            return played > 0 ? Mathf.RoundToInt(100f * Get(PlayerStat.RoundsWon) / played) : 0;
        }
    }

    // Postać, którą gracz rozegrał najwięcej rund ("" = jeszcze żadnej).
    public static string FavoriteSkin(GameArt art)
    {
        string best = "";
        int most = 0;

        if (art != null && art.players != null)
        {
            foreach (PlayerSprites skin in art.players)
            {
                int rounds = PlayerPrefs.GetInt(SkinRoundsPrefix + skin.label, 0);

                if (rounds > most)
                {
                    most = rounds;
                    best = skin.label;
                }
            }
        }

        return best;
    }

    // ---------- Doświadczenie i poziom ----------

    public static int TotalXp
    {
        get
        {
            if (totalXp < 0)
            {
                totalXp = PlayerPrefs.GetInt(XpKey, 0);
            }

            return totalXp;
        }
    }

    // Ile XP trzeba zebrać na danym poziomie, żeby wejść na następny.
    public static int XpToNext(int level)
    {
        return 60 + 30 * (Mathf.Max(1, level) - 1);
    }

    // Monety za wejście na dany poziom (co piąty poziom dodatkowa premia).
    public static int LevelReward(int level)
    {
        return 15 + 5 * level + (level % 5 == 0 ? 100 : 0);
    }

    public static int Level
    {
        get
        {
            Split(TotalXp, out int level, out _);
            return level;
        }
    }

    // XP zebrane na obecnym poziomie (pasek postępu to XpInLevel / XpToNext(Level)).
    public static int XpInLevel
    {
        get
        {
            Split(TotalXp, out _, out int rest);
            return rest;
        }
    }

    static void Split(int xp, out int level, out int rest)
    {
        level = 1;
        rest = xp;

        while (rest >= XpToNext(level))
        {
            rest -= XpToNext(level);
            level++;
        }
    }

    // ---------- Zdarzenia z rozgrywki ----------

    // Koniec rundy, w której brał udział lokalny gracz. Liczy statystyki, przyznaje XP i (przy awansie) monety za poziomy.
    public static void RoundFinished(bool won, int playerKills, int botKills, bool online, string skinLabel)
    {
        Add(PlayerStat.RoundsPlayed);

        if (online)
        {
            Add(PlayerStat.OnlineRounds);
        }

        if (won)
        {
            Add(PlayerStat.RoundsWon);
            Add(PlayerStat.WinStreak);

            if (Get(PlayerStat.WinStreak) > Get(PlayerStat.BestWinStreak))
            {
                Set(PlayerStat.BestWinStreak, Get(PlayerStat.WinStreak));
            }
        }
        else
        {
            Set(PlayerStat.WinStreak, 0);
        }

        if (!string.IsNullOrEmpty(skinLabel))
        {
            PlayerPrefs.SetInt(SkinRoundsPrefix + skinLabel, PlayerPrefs.GetInt(SkinRoundsPrefix + skinLabel, 0) + 1);
        }

        int xp = XpForRound + (won ? XpForWin : 0) + playerKills * XpForPlayerKill + botKills * XpForBotKill;
        int before = Level;
        int coins = AddXp(xp);

        LastRound = new RoundProgress { xp = xp, levelBefore = before, levelAfter = Level, levelCoins = coins };
        PlayerPrefs.Save();
    }

    // Dodaje doświadczenie (runda albo misja). Przy awansie wypłaca monety za każdy nowy poziom i zwraca ich sumę.
    public static int AddXp(int amount)
    {
        int before = Level;

        totalXp = TotalXp + Mathf.Max(0, amount);
        PlayerPrefs.SetInt(XpKey, totalXp);

        int coins = 0;

        for (int level = before + 1; level <= Level; level++)
        {
            coins += LevelReward(level);
        }

        if (coins > 0)
        {
            PlayerWallet.AddCoins(coins);
        }

        return coins;
    }
}

// Ikonka profilu gracza: buźka dowolnej postaci, niezależnie od skina, którym gra. Zwykłe buźki ma każdy,
// a buźka w kolorach chromy odblokowuje się razem z tą chromą (masz skina = masz ikonkę).
public static class PlayerAvatar
{
    const string SkinKey = "avatar_skin";
    const string ChromaKey = "avatar_chroma";

    public static bool Unlocked(string skinLabel, int chroma)
    {
        return chroma < 0 || PlayerWallet.Owns(skinLabel, chroma);
    }

    public static void Set(string skinLabel, int chroma)
    {
        PlayerPrefs.SetString(SkinKey, skinLabel ?? "");
        PlayerPrefs.SetInt(ChromaKey, chroma);
        PlayerPrefs.Save();
    }

    // Wybrana ikonka: numer postaci i chromy (-1 = zwykła). Gdy nic nie wybrano, jest to zwykła buźka obecnego skina.
    public static void Current(GameArt art, out int skinIndex, out int chroma)
    {
        skinIndex = 0;
        chroma = -1;

        if (art == null || art.SkinCount == 0)
        {
            return;
        }

        string saved = PlayerPrefs.GetString(SkinKey, "");
        int found = saved.Length > 0 ? Array.FindIndex(art.players, p => p.label == saved) : -1;

        if (found < 0)
        {
            skinIndex = Mathf.Clamp(SkinSelection.Index, 0, art.SkinCount - 1);
            return;
        }

        skinIndex = found;
        int wanted = PlayerPrefs.GetInt(ChromaKey, -1);

        if (wanted >= 0 && wanted < art.players[found].ChromaList.Length && Unlocked(saved, wanted))
        {
            chroma = wanted;
        }
    }

    // Wygląd ikonki (buźka i efekty chromy). null, gdy w grze nie ma skinów.
    public static PlayerSprites Look(GameArt art)
    {
        if (art == null || art.SkinCount == 0)
        {
            return null;
        }

        Current(art, out int skinIndex, out int chroma);
        return art.SkinLook(skinIndex, chroma) ?? art.players[skinIndex];
    }
}
