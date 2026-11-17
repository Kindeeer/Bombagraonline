using System.Collections.Generic;
using UnityEngine;

// Rodzaj skrzynki: cena, z jakiej puli losuje chromy i jak wygląda. Nową skrzynkę dopisuje się w metodzie Build.
//
// Balans (monety z wygranych rund: 10 za rundę):
// - Mała (40): tylko zwykłe i rzadkie chromy, tania na start.
// - Zwykła (100): wszystko, szanse bazowe (legendarna ok. 4%).
// - Motywowa (200): chromy jednego motywu, lepsze szanse na rzadsze (legendarna ok. 9%).
// - Szczęśliwa (400): wszystko, znacznie lepsze szanse na epickie i legendarne.
// - Epicka (600): tylko epickie i legendarne.
// - Legendarna (1500): zawsze legendarna chroma.
public class ChestType
{
    public string id;
    public string polish;
    public string english;
    public string polishInfo;
    public string englishInfo;
    public int price;

    // null = chromy z każdego motywu
    public ChromaTheme? theme;

    // Zakres rzadkości, jakie skrzynka może dać.
    public ChromaRarity minRarity;
    public ChromaRarity maxRarity = ChromaRarity.Legendary;

    // Mnożniki szans dla zwykłej, rzadkiej, epickiej i legendarnej chromy (1 = szanse bazowe).
    public float[] boost = { 1f, 1f, 1f, 1f };

    // Kolory skrzynki: korpus, wieczko, okucia i poświata.
    public Color body;
    public Color lid;
    public Color accent;
    public Color glow;

    public string Name => Loc.P(polish, english);
    public string Info => Loc.P(polishInfo, englishInfo);

    // Czy chroma może wypaść z tej skrzynki.
    public bool Accepts(SkinChroma chroma)
    {
        return (!theme.HasValue || chroma.theme == theme.Value) && chroma.rarity >= minRarity && chroma.rarity <= maxRarity;
    }

    // Waga losowania chromy o danej rzadkości w tej skrzynce.
    public int Weight(ChromaRarity rarity)
    {
        return Mathf.Max(1, Mathf.RoundToInt(SkinChroma.RarityWeight(rarity) * boost[(int)rarity]));
    }
}

public static class Chests
{
    const string SelectedKey = "chest_selected";

    static readonly Color Gold = new Color(0.98f, 0.78f, 0.22f);

    static List<ChestType> list;
    static int selected = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        list = null;
        selected = -1;
    }

    // Wszystkie skrzynki w kolejności przełączania w menu.
    public static List<ChestType> All
    {
        get
        {
            if (list == null)
            {
                list = Build();
            }

            return list;
        }
    }

    static List<ChestType> Build()
    {
        List<ChestType> chests = new List<ChestType>
        {
            new ChestType
            {
                id = "small", polish = "MAŁA", english = "SMALL", price = 40, maxRarity = ChromaRarity.Rare,
                polishInfo = "Tania skrzynka: zwykłe i rzadkie chromy.", englishInfo = "A cheap chest: common and rare chromas.",
                body = new Color(0.50f, 0.36f, 0.22f), lid = new Color(0.62f, 0.46f, 0.28f), accent = new Color(0.85f, 0.85f, 0.88f), glow = new Color(0.95f, 0.9f, 0.7f)
            },
            new ChestType
            {
                id = "classic", polish = "ZWYKŁA", english = "CLASSIC", price = 100,
                polishInfo = "Wszystkie chromy ze wszystkich motywów.", englishInfo = "Every chroma from every theme.",
                body = new Color(0.52f, 0.28f, 0.13f), lid = new Color(0.66f, 0.37f, 0.17f), accent = Gold, glow = new Color(1f, 0.85f, 0.4f)
            },
            new ChestType
            {
                id = "lucky", polish = "SZCZĘŚLIWA", english = "LUCKY", price = 400, boost = new[] { 0.5f, 1f, 2.5f, 5f },
                polishInfo = "Wszystkie chromy, ale dużo większa szansa na epickie i legendarne.", englishInfo = "Every chroma, but much better odds for epic and legendary.",
                body = new Color(0.15f, 0.45f, 0.25f), lid = new Color(0.20f, 0.60f, 0.32f), accent = Gold, glow = new Color(0.7f, 1f, 0.5f)
            },
            new ChestType
            {
                id = "epic", polish = "EPICKA", english = "EPIC", price = 600, minRarity = ChromaRarity.Epic,
                polishInfo = "Tylko epickie i legendarne chromy.", englishInfo = "Only epic and legendary chromas.",
                body = new Color(0.40f, 0.18f, 0.55f), lid = new Color(0.55f, 0.28f, 0.72f), accent = new Color(1f, 0.85f, 0.4f), glow = new Color(0.78f, 0.40f, 1f)
            },
            new ChestType
            {
                id = "legendary", polish = "LEGENDARNA", english = "LEGENDARY", price = 1500, minRarity = ChromaRarity.Legendary,
                polishInfo = "Zawsze legendarna chroma.", englishInfo = "Always a legendary chroma.",
                body = new Color(0.12f, 0.09f, 0.08f), lid = new Color(0.22f, 0.17f, 0.10f), accent = new Color(1f, 0.78f, 0.2f), glow = new Color(1f, 0.75f, 0.2f)
            }
        };

        float[] themeBoost = { 1f, 1.5f, 2f, 3f };

        chests.Add(new ChestType
        {
            id = "ice", polish = "LODOWA", english = "ICE", price = 200, theme = ChromaTheme.Ice, boost = themeBoost,
            polishInfo = "Lód, szron, zorza i diament.", englishInfo = "Ice, frost, aurora and diamond.",
            body = new Color(0.45f, 0.65f, 0.85f), lid = new Color(0.65f, 0.82f, 0.95f), accent = new Color(0.92f, 0.98f, 1f), glow = new Color(0.6f, 0.9f, 1f)
        });
        chests.Add(new ChestType
        {
            id = "fire", polish = "OGNISTA", english = "FIRE", price = 200, theme = ChromaTheme.Fire, boost = themeBoost,
            polishInfo = "Żar, ogień, magma i feniks.", englishInfo = "Ember, fire, magma and phoenix.",
            body = new Color(0.45f, 0.10f, 0.06f), lid = new Color(0.65f, 0.15f, 0.08f), accent = new Color(1f, 0.65f, 0.15f), glow = new Color(1f, 0.5f, 0.15f)
        });
        chests.Add(new ChestType
        {
            id = "nature", polish = "LEŚNA", english = "FOREST", price = 200, theme = ChromaTheme.Nature, boost = themeBoost,
            polishInfo = "Las, trucizna, jesień i kwiaty.", englishInfo = "Forest, toxic, autumn and blossom.",
            body = new Color(0.22f, 0.42f, 0.18f), lid = new Color(0.32f, 0.58f, 0.25f), accent = new Color(0.95f, 0.85f, 0.4f), glow = new Color(0.6f, 1f, 0.5f)
        });
        chests.Add(new ChestType
        {
            id = "candy", polish = "CUKIERKOWA", english = "CANDY", price = 200, theme = ChromaTheme.Candy, boost = themeBoost,
            polishInfo = "Truskawki, jagody, wata i pastelowa tęcza.", englishInfo = "Strawberry, berry, cotton candy and pastel rainbow.",
            body = new Color(0.95f, 0.50f, 0.70f), lid = new Color(1f, 0.68f, 0.82f), accent = Color.white, glow = new Color(1f, 0.7f, 0.9f)
        });
        chests.Add(new ChestType
        {
            id = "cosmic", polish = "KOSMICZNA", english = "COSMIC", price = 200, theme = ChromaTheme.Cosmic, boost = themeBoost,
            polishInfo = "Noc, neon, galaktyka i supernowa.", englishInfo = "Night, neon, galaxy and supernova.",
            body = new Color(0.12f, 0.10f, 0.35f), lid = new Color(0.22f, 0.15f, 0.50f), accent = new Color(0.5f, 0.95f, 1f), glow = new Color(0.6f, 0.5f, 1f)
        });

        return chests;
    }

    // Skrzynka wybrana w menu (pamiętana na telefonie).
    public static int SelectedIndex
    {
        get
        {
            if (selected < 0)
            {
                selected = PlayerPrefs.GetInt(SelectedKey, 1);
            }

            return Mathf.Clamp(selected, 0, All.Count - 1);
        }
        set
        {
            int count = All.Count;
            selected = ((value % count) + count) % count;
            PlayerPrefs.SetInt(SelectedKey, selected);
        }
    }

    public static ChestType Selected => All[SelectedIndex];
}
