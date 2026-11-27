using System.Collections.Generic;
using UnityEngine;

// Odłamki chrom: epicka chroma wypada ze skrzynki w 2 odłamkach (1/2, 2/2), legendarna w 3 (1/3, 2/3, 3/3).
// Chroma odblokowuje się dopiero po zebraniu wszystkich odłamków. Zwykła i rzadka wypadają w całości, jak dawniej.
// Każdy odłamek losuje się od nowa spośród wszystkich brakujących chrom (tak jak cała chroma), więc kolejne odłamki
// mogą trafiać do różnych chrom.
// Zapisane na urządzeniu (PlayerPrefs), tak jak chromy.
public static class ChromaShards
{
    const string Key = "chroma_shards";

    static Dictionary<string, int> counts;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        counts = null;
    }

    // Ile odłamków potrzeba, żeby odblokować chromę tej rzadkości (1 = wypada w całości).
    public static int Needed(ChromaRarity rarity)
    {
        switch (rarity)
        {
            case ChromaRarity.Epic: return 2;
            case ChromaRarity.Legendary: return 3;
            default: return 1;
        }
    }

    static Dictionary<string, int> Counts
    {
        get
        {
            if (counts == null)
            {
                counts = new Dictionary<string, int>();

                foreach (string entry in PlayerPrefs.GetString(Key, "").Split(';'))
                {
                    int split = entry.LastIndexOf('=');

                    if (split > 0 && int.TryParse(entry.Substring(split + 1), out int count) && count > 0)
                    {
                        counts[entry.Substring(0, split)] = count;
                    }
                }
            }

            return counts;
        }
    }

    static string Token(string skinLabel, int chroma)
    {
        return skinLabel + "#" + chroma;
    }

    // Ile odłamków tej chromy gracz już ma.
    public static int Get(string skinLabel, int chroma)
    {
        return Counts.TryGetValue(Token(skinLabel, chroma), out int count) ? count : 0;
    }

    // Dodaje jeden odłamek i zwraca, ile ich teraz jest. Po zebraniu kompletu licznik znika (chroma i tak jest już odblokowana).
    public static int Add(string skinLabel, int chroma, int needed)
    {
        string token = Token(skinLabel, chroma);
        int count = Get(skinLabel, chroma) + 1;

        if (count >= needed)
        {
            Counts.Remove(token);
        }
        else
        {
            Counts[token] = count;
        }

        Save();
        return count;
    }

    // Usuwa wszystkie odłamki (zerowanie postępu na starcie Play w edytorze, patrz PlayerWallet).
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(Key);
        counts = null;
    }

    static void Save()
    {
        List<string> entries = new List<string>();

        foreach (KeyValuePair<string, int> pair in Counts)
        {
            entries.Add(pair.Key + "=" + pair.Value);
        }

        PlayerPrefs.SetString(Key, string.Join(";", entries));
        PlayerPrefs.Save();
    }
}
