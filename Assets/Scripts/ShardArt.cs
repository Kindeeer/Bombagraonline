using System.Collections.Generic;
using UnityEngine;

// Grafika odłamków rysowana z kodu: chroma z odłamków wygląda jak puzzle (jak ewolucje w Clash Royale).
// Obrazek postaci jest pocięty skośnymi liniami na 2 kawałki (epicka) albo 3 (legendarna). Piece() to maska jednego kawałka
// (biały = kawałek, przezroczysty = reszta), a Cracks() to jasne pęknięcia między kawałkami, kładzione na wierzch.
// Wszystkie obrazki są kwadratowe i rozciągane na prostokąt podglądu, więc linie cięcia wypadają w tych samych miejscach.
public static class ShardArt
{
    const int Size = 128;
    // szerokość szczeliny między kawałkami (w pikselach obrazka 128 x 128)
    const float Gap = 1.6f;

    static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        cache.Clear();
    }

    // Maska kawałka nr index (od 0) przy podziale na count kawałków.
    public static Sprite Piece(int count, int index)
    {
        return Get(count * 10 + index, (x, y) =>
        {
            int piece = PieceAt(count, x, y, out float edge);
            return piece == index && edge > Gap ? 1f : 0f;
        });
    }

    // Jasne linie pęknięć między kawałkami.
    public static Sprite Cracks(int count)
    {
        return Get(1000 + count, (x, y) =>
        {
            PieceAt(count, x, y, out float edge);
            return Mathf.Clamp01(Gap + 0.6f - edge);
        });
    }

    // Do którego kawałka należy punkt (x, y), i jak daleko (w pikselach) jest do najbliższej linii cięcia.
    // 2 kawałki: jedna skośna linia (góra i dół). 3 kawałki: skośna linia oddziela dół, a górę dzieli druga linia na lewo i prawo.
    static int PieceAt(int count, float x, float y, out float edge)
    {
        float u = x / Size;
        float v = y / Size;

        if (count <= 2)
        {
            float d = (v - (0.62f - 0.24f * u)) * Size / 1.028f;
            edge = Mathf.Abs(d);
            return d > 0f ? 0 : 1;
        }

        float bottom = (v - (0.40f + 0.14f * u)) * Size / 1.01f;

        if (bottom <= 0f)
        {
            edge = -bottom;
            return 2;
        }

        float split = (u - (0.58f - 0.22f * (v - 0.40f))) * Size / 1.024f;
        edge = Mathf.Min(Mathf.Abs(split), bottom);
        return split < 0f ? 0 : 1;
    }

    static Sprite Get(int key, System.Func<float, float, float> alpha)
    {
        if (cache.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                byte a = (byte)Mathf.RoundToInt(255f * alpha(x + 0.5f, y + 0.5f));
                pixels[y * Size + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "Shard_" + key;
        cache[key] = sprite;
        return sprite;
    }
}
