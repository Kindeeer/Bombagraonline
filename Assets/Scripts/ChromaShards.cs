using System.Collections.Generic;
using UnityEngine;

// Odłamki chrom: epicka chroma wypada ze skrzynki w 2 odłamkach (1/2, 2/2), legendarna w 3 (1/3, 2/3, 3/3).
// Chroma odblokowuje się dopiero po zebraniu wszystkich odłamków. Zwykła i rzadka wypadają w całości, jak dawniej.
// Gdy skrzynka wylosuje epicką albo legendarną chromę, a gracz zbiera już odłamki jakiejś chromy tej rzadkości,
// odłamek trafia do niej (tej z największą liczbą odłamków), żeby kolekcja szła do przodu, a nie rozsypywała się po wielu chromach.
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

    // Chroma tej samej rzadkości, której odłamki gracz już zbiera (i którą ta skrzynka może dać). false, gdy żadnej nie zbiera.
    public static bool FindStarted(GameArt art, ChestType chest, ChromaRarity rarity, out int skinIndex, out int chromaIndex)
    {
        skinIndex = -1;
        chromaIndex = -1;
        int best = 0;

        for (int skin = 0; skin < art.SkinCount; skin++)
        {
            SkinChroma[] chromas = art.players[skin].ChromaList;
            string label = art.players[skin].label;

            for (int c = 0; c < chromas.Length; c++)
            {
                int have = Get(label, c);

                if (have > best && chromas[c].rarity == rarity && chest.Accepts(chromas[c]) && !PlayerWallet.Owns(label, c))
                {
                    best = have;
                    skinIndex = skin;
                    chromaIndex = c;
                }
            }
        }

        return skinIndex >= 0;
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
