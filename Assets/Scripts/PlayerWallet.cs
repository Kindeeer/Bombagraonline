using System;
using System.Collections.Generic;
using UnityEngine;

// Portfel gracza: monety, odblokowane chromy skinów i założone chromy. Wszystko zapamiętane na telefonie (PlayerPrefs).
// Monety dostaje się za wygrane rundy (WinReward). Skrzynka kosztuje ChestPrice i losuje chromę, której jeszcze nie masz.
public static class PlayerWallet
{
    public const int WinReward = 10;

    // Moneta za samo rozegranie rundy (dla każdego gracza, także przegranego).
    public const int RoundReward = 1;

    // Nagroda za zabicie prawdziwego gracza (nie bota): 1-2 monety. Użyje jej tryb sieciowy, bo w grze z botami
    // nie ma kogo zabić "na prawdziwo".
    public const int PlayerKillReward = 2;

    const string CoinsKey = "coins";
    const string OwnedKey = "chromas_owned";
    const string EquippedKey = "chroma_equipped_";

    // Górna granica monet. Zapisujemy je jako int (max ok. 2,1 mld), więc zostawiamy duży zapas i nie ma przepełnienia.
    public const int MaxCoins = 999_999_999;

    static int coins = -1;
    static HashSet<string> owned;

    public static event Action Changed;

#if UNITY_EDITOR
    // W edytorze każde wciśnięcie Play zaczyna od zera (bez monet i chrom), żeby dało się testować losowanie od początku.
    // Wyłączysz to w menu Bomb Game > Zachowuj monety i chromy między uruchomieniami Play.
    public const string KeepProgressPref = "BombaGra.KeepWalletInEditor";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetForEditorTests()
    {
        if (UnityEditor.EditorPrefs.GetBool(KeepProgressPref, false))
        {
            return;
        }

        foreach (string token in PlayerPrefs.GetString(OwnedKey, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            PlayerPrefs.DeleteKey(EquippedKey + token.Split('#')[0]);
        }

        PlayerPrefs.DeleteKey(OwnedKey);
        PlayerPrefs.DeleteKey(CoinsKey);
        owned = null;
        coins = -1;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        coins = -1;
        owned = null;
        Changed = null;
    }

    public static int Coins
    {
        get
        {
            if (coins < 0)
            {
                coins = PlayerPrefs.GetInt(CoinsKey, 0);
            }

            return coins;
        }
    }

    public static void AddCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        coins = (int)Math.Min((long)Coins + amount, MaxCoins);
        PlayerProgress.Add(PlayerStat.CoinsEarned, amount);
        PlayerPrefs.SetInt(CoinsKey, coins);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // Krótki zapis liczby monet: do 9 999 pełna liczba (500, 9 999), potem 10K, 12.5K, 1.2M, 3.4B.
    public static string Format(int value)
    {
        if (value < 10_000)
        {
            return value.ToString();
        }

        if (value < 1_000_000)
        {
            return Trim(value / 1000f) + "K";
        }

        if (value < 1_000_000_000)
        {
            return Trim(value / 1_000_000f) + "M";
        }

        return Trim(value / 1_000_000_000f) + "B";
    }

    // Jedno miejsce po przecinku, bez zbędnego ".0" (12.0 -> 12, 12.54 -> 12.5).
    static string Trim(float value)
    {
        value = Mathf.Floor(value * 10f) / 10f;
        return value.ToString(value >= 100f || Mathf.Approximately(value, Mathf.Round(value)) ? "0" : "0.0", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static bool TrySpend(int amount)
    {
        if (Coins < amount)
        {
            return false;
        }

        coins = Coins - amount;
        PlayerPrefs.SetInt(CoinsKey, coins);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

    // ---------- Chromy ----------

    static string Token(string skinLabel, int chroma)
    {
        return skinLabel + "#" + chroma;
    }

    static HashSet<string> Owned
    {
        get
        {
            if (owned == null)
            {
                string saved = PlayerPrefs.GetString(OwnedKey, "");
                owned = new HashSet<string>(saved.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            }

            return owned;
        }
    }

    public static bool Owns(string skinLabel, int chroma)
    {
        return Owned.Contains(Token(skinLabel, chroma));
    }

    public static void Unlock(string skinLabel, int chroma)
    {
        if (Owned.Add(Token(skinLabel, chroma)))
        {
            PlayerPrefs.SetString(OwnedKey, string.Join(";", Owned));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    // Założona chroma skina (-1 = zwykły wygląd).
    public static int Equipped(string skinLabel)
    {
        int chroma = PlayerPrefs.GetInt(EquippedKey + skinLabel, -1);
        return chroma >= 0 && Owns(skinLabel, chroma) ? chroma : -1;
    }

    public static void Equip(string skinLabel, int chroma)
    {
        PlayerPrefs.SetInt(EquippedKey + skinLabel, chroma);
        PlayerPrefs.Save();
        SkinSelection.NotifyChanged();
        Changed?.Invoke();
    }

    // Ile chrom z puli skrzynki jeszcze nie jest Twoje.
    public static int MissingCount(GameArt art, ChestType chest)
    {
        int missing = 0;

        for (int skin = 0; skin < art.SkinCount; skin++)
        {
            SkinChroma[] chromas = art.players[skin].ChromaList;

            for (int c = 0; c < chromas.Length; c++)
            {
                if (chest.Accepts(chromas[c]) && !Owns(art.players[skin].label, c))
                {
                    missing++;
                }
            }
        }

        return missing;
    }

    // Ile chrom jest w puli skrzynki (razem ze zdobytymi).
    public static int TotalCount(GameArt art, ChestType chest)
    {
        int total = 0;

        for (int skin = 0; skin < art.SkinCount; skin++)
        {
            foreach (SkinChroma chroma in art.players[skin].ChromaList)
            {
                if (chest.Accepts(chroma))
                {
                    total++;
                }
            }
        }

        return total;
    }

    // Losuje chromę z puli skrzynki, której jeszcze nie masz (szansa zależy od rzadkości). false = masz już wszystkie z tej puli.
    public static bool Roll(GameArt art, ChestType chest, out int skinIndex, out int chromaIndex)
    {
        skinIndex = -1;
        chromaIndex = -1;
        int totalWeight = 0;

        for (int skin = 0; skin < art.SkinCount; skin++)
        {
            SkinChroma[] chromas = art.players[skin].ChromaList;

            for (int c = 0; c < chromas.Length; c++)
            {
                if (chest.Accepts(chromas[c]) && !Owns(art.players[skin].label, c))
                {
                    totalWeight += chest.Weight(chromas[c].rarity);
                }
            }
        }

        if (totalWeight <= 0)
        {
            return false;
        }

        int roll = UnityEngine.Random.Range(0, totalWeight);

        for (int skin = 0; skin < art.SkinCount; skin++)
        {
            SkinChroma[] chromas = art.players[skin].ChromaList;

            for (int c = 0; c < chromas.Length; c++)
            {
                if (!chest.Accepts(chromas[c]) || Owns(art.players[skin].label, c))
                {
                    continue;
                }

                roll -= chest.Weight(chromas[c].rarity);

                if (roll < 0)
                {
                    skinIndex = skin;
                    chromaIndex = c;
                    return true;
                }
            }
        }

        return false;
    }
}
