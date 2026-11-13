using System;
using System.Collections.Generic;
using UnityEngine;

// Efekt wizualny chromy: błyski, ogień, śnieg, liście, bąbelki, serduszka albo gwiazdy wokół postaci.
public enum ChromaEffect
{
    None,
    Sparkle,
    Fire,
    Snow,
    Leaves,
    Bubbles,
    Hearts,
    Stars
}

// Ikonka wzoru na postaci (każda chroma ma swój wzór z jednej albo dwóch ikonek, które zsuwają się po ciele).
// Auto w pierwszym polu = ikonka pasująca do efektu chromy, w drugim polu = brak drugiej ikonki.
public enum ChromaIcon
{
    Auto,
    Sparkle,
    Flame,
    Snowflake,
    Leaf,
    Bubble,
    Heart,
    Star,
    Flower,
    Gem,
    Moon,
    Bolt,
    Drop
}

// Efekt wraz z kolorami (to dostaje postać w grze).
[Serializable]
public struct ChromaFxLook
{
    public ChromaEffect effect;
    public Color colorA;
    public Color colorB;
    [Tooltip("Losowe kolory tęczy zamiast dwóch kolorów.")]
    public bool rainbow;
    [Tooltip("Mnożnik liczby cząstek.")]
    public float strength;

    [Tooltip("Wzór na postaci: półprzezroczyste ikonki chromy zsuwają się po ciele (dawniej pasy światła).")]
    public bool glint;
    public Color glintColor;
    public bool glintRainbow;

    // ikonki wzoru (numery cząstek z ChromaFx, -1 = brak) i ich dwa kolory
    public int patternA;
    public int patternB;
    public Color patternColorA;
    public Color patternColorB;

    public bool HasParticles => effect != ChromaEffect.None && strength > 0f;

    public bool Active => HasParticles || glint;
}

// Parametry jednego rodzaju efektu. Odległości i prędkości liczone są w wysokościach postaci (1 = cała postać),
// więc efekt wygląda tak samo na małej kartce w menu i na postaci w grze.
public class FxStyle
{
    public int sprite;
    public float rate;
    public float life;
    public float velXMin, velXMax;
    public float velYMin, velYMax;
    public float sizeMin, sizeMax;
    public float sway;
    public float spawnX;
    public float spawnYMin, spawnYMax;
    public float spin;
    public bool twinkle;
    public bool shrink;
    [Tooltip("Cząstka ciemnieje pod koniec życia (ogień).")]
    public bool colorOverLife;
    [Tooltip("Kołysanie na boki zamiast obracania (płomienie, serduszka).")]
    public float wobble;
    [Tooltip("Jaka część cząstek rysuje się za postacią (reszta przed nią).")]
    public float behind;
    [Tooltip("Dodatkowe drobne cząstki tego samego efektu (np. iskry nad ogniem).")]
    public FxStyle extra;
}

public static class ChromaFx
{
    public const int Star = 0;
    public const int Flame = 1;
    public const int Snowflake = 2;
    public const int Leaf = 3;
    public const int Glow = 4;
    public const int Heart = 5;
    public const int Bubble = 6;
    public const int StarFive = 7;
    public const int Flower = 8;
    public const int Gem = 9;
    public const int Moon = 10;
    public const int Bolt = 11;
    public const int Drop = 12;

    const int KindCount = 13;

    // Grafiki cząstek leżą w Assets/Resources/ChromaFx jako "mapy cieniowania": jasność piksela mówi, który kolor chromy
    // tam trafi (czarny = ciemny kontur, szary 50% = kolor B, 85% = kolor A, biały = połysk), a kanał alfa to kształt.
    // Dzięki temu jeden rysunek płomienia daje czerwony ogień, niebieski ogień i każdy inny. Grafik może podmienić pliki
    // na własne, byle zachował ten sam podział jasności. Gdy pliku brakuje, cząstkę rysuje kod (prosty kształt).
    static readonly string[] resourceNames =
    {
        "sparkle", "flame", "snowflake", "leaf", "glow", "heart", "bubble", "star", "flower", "gem", "moon", "bolt", "drop"
    };

    // Numer cząstki dla ikonki wzoru (-1 = brak).
    public static int KindOf(ChromaIcon icon)
    {
        switch (icon)
        {
            case ChromaIcon.Sparkle: return Star;
            case ChromaIcon.Flame: return Flame;
            case ChromaIcon.Snowflake: return Snowflake;
            case ChromaIcon.Leaf: return Leaf;
            case ChromaIcon.Bubble: return Bubble;
            case ChromaIcon.Heart: return Heart;
            case ChromaIcon.Star: return StarFive;
            case ChromaIcon.Flower: return Flower;
            case ChromaIcon.Gem: return Gem;
            case ChromaIcon.Moon: return Moon;
            case ChromaIcon.Bolt: return Bolt;
            case ChromaIcon.Drop: return Drop;
            default: return -1;
        }
    }

    // Ikonka pasująca do efektu cząsteczkowego (gdy chroma nie ma własnej).
    public static int KindOf(ChromaEffect effect)
    {
        switch (effect)
        {
            case ChromaEffect.Fire: return Flame;
            case ChromaEffect.Snow: return Snowflake;
            case ChromaEffect.Leaves: return Leaf;
            case ChromaEffect.Bubbles: return Bubble;
            case ChromaEffect.Hearts: return Heart;
            case ChromaEffect.Stars: return StarFive;
            default: return Star;
        }
    }

    const float ShadeB = 0.5f;
    const float ShadeA = 0.85f;

    static readonly Color32[][] shadeMaps = new Color32[KindCount][];
    static readonly int[] shadeSizes = new int[KindCount];
    static readonly Dictionary<(int, Color32, Color32), Sprite> baked = new Dictionary<(int, Color32, Color32), Sprite>();
    static readonly Dictionary<(int, int, Color32, Color32), Texture2D> patterns = new Dictionary<(int, int, Color32, Color32), Texture2D>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Array.Clear(shadeMaps, 0, shadeMaps.Length);
        Array.Clear(styles, 0, styles.Length);
        baked.Clear();
        patterns.Clear();
    }

    static readonly FxStyle[] styles = new FxStyle[8];

    public static FxStyle StyleOf(ChromaEffect effect)
    {
        int index = (int)effect;

        if (styles[index] == null)
        {
            styles[index] = BuildStyle(effect);
        }

        return styles[index];
    }

    static FxStyle BuildStyle(ChromaEffect effect)
    {
        switch (effect)
        {
            case ChromaEffect.Fire:
                // większe płomienie liżą postać od dołu (większość za nią, więc widać sylwetkę w ogniu), nad nimi drobne iskry
                return new FxStyle
                {
                    sprite = Flame, rate = 10f, life = 0.85f, velXMin = -0.04f, velXMax = 0.04f, velYMin = 0.22f, velYMax = 0.5f,
                    sizeMin = 0.20f, sizeMax = 0.34f, sway = 0.08f, spawnX = 0.32f, spawnYMin = 0.02f, spawnYMax = 0.42f,
                    shrink = true, colorOverLife = true, wobble = 10f, behind = 0.65f,
                    extra = new FxStyle
                    {
                        sprite = Glow, rate = 7f, life = 1.0f, velXMin = -0.08f, velXMax = 0.08f, velYMin = 0.45f, velYMax = 0.85f,
                        sizeMin = 0.05f, sizeMax = 0.09f, sway = 0.2f, spawnX = 0.3f, spawnYMin = 0.2f, spawnYMax = 0.7f,
                        shrink = true, behind = 0.4f
                    }
                };
            case ChromaEffect.Snow:
                return new FxStyle
                {
                    sprite = Snowflake, rate = 6f, life = 2.0f, velXMin = -0.05f, velXMax = 0.05f, velYMin = -0.4f, velYMax = -0.2f,
                    sizeMin = 0.11f, sizeMax = 0.19f, sway = 0.16f, spawnX = 0.5f, spawnYMin = 1.0f, spawnYMax = 1.15f, spin = 70f,
                    behind = 0.35f
                };
            case ChromaEffect.Leaves:
                return new FxStyle
                {
                    sprite = Leaf, rate = 4.5f, life = 2.1f, velXMin = -0.04f, velXMax = 0.04f, velYMin = -0.34f, velYMax = -0.18f,
                    sizeMin = 0.15f, sizeMax = 0.23f, sway = 0.25f, spawnX = 0.5f, spawnYMin = 1.0f, spawnYMax = 1.15f, spin = 110f,
                    behind = 0.35f
                };
            case ChromaEffect.Bubbles:
                return new FxStyle
                {
                    sprite = Bubble, rate = 6f, life = 1.4f, velXMin = -0.03f, velXMax = 0.03f, velYMin = 0.18f, velYMax = 0.4f,
                    sizeMin = 0.10f, sizeMax = 0.20f, sway = 0.12f, spawnX = 0.38f, spawnYMin = 0f, spawnYMax = 0.4f, behind = 0.5f
                };
            case ChromaEffect.Hearts:
                return new FxStyle
                {
                    sprite = Heart, rate = 3.5f, life = 1.4f, velXMin = -0.03f, velXMax = 0.03f, velYMin = 0.18f, velYMax = 0.32f,
                    sizeMin = 0.14f, sizeMax = 0.22f, sway = 0.1f, spawnX = 0.36f, spawnYMin = 0.35f, spawnYMax = 0.95f,
                    wobble = 14f, behind = 0.3f
                };
            case ChromaEffect.Stars:
                return new FxStyle
                {
                    sprite = StarFive, rate = 7f, life = 1.3f, velXMin = -0.04f, velXMax = 0.04f, velYMin = -0.02f, velYMax = 0.07f,
                    sizeMin = 0.10f, sizeMax = 0.18f, sway = 0.05f, spawnX = 0.62f, spawnYMin = -0.05f, spawnYMax = 1.15f,
                    twinkle = true, wobble = 16f, behind = 0.5f
                };
            default:
                return new FxStyle
                {
                    sprite = Star, rate = 6f, life = 0.95f, velXMin = -0.04f, velXMax = 0.04f, velYMin = 0.02f, velYMax = 0.14f,
                    sizeMin = 0.16f, sizeMax = 0.30f, sway = 0.02f, spawnX = 0.42f, spawnYMin = 0.05f, spawnYMax = 1.0f,
                    twinkle = true, behind = 0.4f
                };
        }
    }

    // Kolor, którym barwimy gotową (pokolorowaną) cząstkę: zwykle prawie biały, przy tęczy losowy odcień.
    public static Color PickTint(ChromaFxLook look)
    {
        if (look.rainbow)
        {
            return Color.HSVToRGB(UnityEngine.Random.value, 0.5f, 1f);
        }

        float shade = UnityEngine.Random.Range(0.9f, 1f);
        return new Color(shade, shade, shade, 1f);
    }

    // Sprite cząstki w kolorach danej chromy (szerokość = 1 jednostka). Powstaje raz na zestaw kolorów i jest zapamiętany.
    public static Sprite SpriteOf(int kind, ChromaFxLook look)
    {
        // tęcza: neutralna (biała) cząstka, którą każda sztuka barwi własnym odcieniem
        Color32 a = look.rainbow ? new Color32(255, 255, 255, 255) : (Color32)Opaque(look.colorA);
        Color32 b = look.rainbow ? new Color32(255, 255, 255, 255) : (Color32)Opaque(look.colorB);

        // w mapie kolor B jest przy brzegu, a A w środku; ciemniejszy z dwóch kolorów zawsze idzie na brzeg
        if (Brightness(b) > Brightness(a))
        {
            (a, b) = (b, a);
        }

        if (baked.TryGetValue((kind, a, b), out Sprite sprite) && sprite != null)
        {
            return sprite;
        }

        sprite = Bake(kind, a, b);
        baked[(kind, a, b)] = sprite;
        return sprite;
    }

    static Color Opaque(Color color)
    {
        color.a = 1f;
        return color;
    }

    static float Brightness(Color32 color)
    {
        return color.r * 0.3f + color.g * 0.59f + color.b * 0.11f;
    }

    static Sprite Bake(int kind, Color a, Color b)
    {
        Color32[] pixels = Colored(kind, a, b, out int size);

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        texture.SetPixels32(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    // Piksele cząstki pokolorowane dwoma kolorami chromy (ciemny kontur, B przy brzegu, A w środku, jasny połysk).
    static Color32[] Colored(int kind, Color a, Color b, out int size)
    {
        Color32[] map = ShadeMap(kind, out size);
        Color32[] pixels = new Color32[map.Length];

        Color dark = new Color(b.r * 0.42f, b.g * 0.42f, b.b * 0.42f);
        Color light = Color.Lerp(a, Color.white, 0.6f);

        for (int i = 0; i < map.Length; i++)
        {
            float shade = map[i].r / 255f;
            Color color;

            if (shade <= ShadeB)
            {
                color = Color.Lerp(dark, b, shade / ShadeB);
            }
            else if (shade <= ShadeA)
            {
                color = Color.Lerp(b, a, (shade - ShadeB) / (ShadeA - ShadeB));
            }
            else
            {
                color = Color.Lerp(a, light, (shade - ShadeA) / (1f - ShadeA));
            }

            Color32 result = color;
            result.a = map[i].a;
            pixels[i] = result;
        }

        return pixels;
    }

    // ---------- Wzór na postaci ----------

    const int PatternSize = 256;
    const int PatternGrid = 3;

    // Kafelek wzoru chromy: dziewięć ikonek rozrzuconych jak nadruk na materiale (różne rozmiary i obroty), który powtarza się
    // bez szwów. Shader EnchantGlint przesuwa go w dół po sylwetce postaci. Kolory są zapisane z pomnożoną alfą.
    public static Texture2D Pattern(ChromaFxLook look)
    {
        bool rainbow = look.glintRainbow || look.rainbow;
        Color32 a = rainbow ? new Color32(255, 255, 255, 255) : (Color32)Opaque(look.patternColorA);
        Color32 b = rainbow ? new Color32(205, 205, 205, 255) : (Color32)Opaque(look.patternColorB);

        if (Brightness(b) > Brightness(a))
        {
            (a, b) = (b, a);
        }

        int kindA = Mathf.Clamp(look.patternA, 0, KindCount - 1);
        int kindB = look.patternB >= 0 && look.patternB < KindCount ? look.patternB : -1;

        if (patterns.TryGetValue((kindA, kindB, a, b), out Texture2D texture) && texture != null)
        {
            return texture;
        }

        Color32[] iconA = Colored(kindA, a, b, out int sizeA);
        int sizeB = 0;
        Color32[] iconB = kindB >= 0 ? Colored(kindB, a, b, out sizeB) : null;

        // składamy w liczbach zmiennoprzecinkowych z pomnożoną alfą (r, g, b, a na piksel)
        float[] canvas = new float[PatternSize * PatternSize * 4];
        float cell = PatternSize / (float)PatternGrid;
        System.Random random = new System.Random(977 + kindA * 31 + kindB * 17);

        for (int gy = 0; gy < PatternGrid; gy++)
        {
            for (int gx = 0; gx < PatternGrid; gx++)
            {
                int index = gy * PatternGrid + gx;
                bool second = iconB != null && (index == 1 || index == 5 || index == 6);

                float size = cell * Mathf.Lerp(0.62f, 0.9f, (float)random.NextDouble());

                // co drugi rząd przesunięty o pół pola (układ "w cegiełkę"), z lekkim rozrzutem
                float cx = (gx + 0.5f + ((float)random.NextDouble() - 0.5f) * 0.4f + (gy % 2) * 0.5f) * cell;
                float cy = (gy + 0.5f + ((float)random.NextDouble() - 0.5f) * 0.4f) * cell;
                float angle = ((float)random.NextDouble() - 0.5f) * 70f * Mathf.Deg2Rad;

                Stamp(canvas, second ? iconB : iconA, second ? sizeB : sizeA, cx, cy, size, angle);
            }
        }

        Color32[] pixels = new Color32[PatternSize * PatternSize];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(canvas[i * 4]), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(canvas[i * 4 + 1]), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(canvas[i * 4 + 2]), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(canvas[i * 4 + 3]), 0, 255));
        }

        texture = new Texture2D(PatternSize, PatternSize, TextureFormat.RGBA32, true)
        {
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave
        };

        texture.SetPixels32(pixels);
        texture.Apply(true);
        patterns[(kindA, kindB, a, b)] = texture;
        return texture;
    }

    // Wkleja obróconą ikonkę o boku size pikseli ze środkiem w (cx, cy). Współrzędne zawijają się na krawędziach kafelka.
    static void Stamp(float[] canvas, Color32[] icon, int iconSize, float cx, float cy, float size, float angle)
    {
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        int reach = Mathf.CeilToInt(size * 0.75f);
        int baseX = Mathf.RoundToInt(cx);
        int baseY = Mathf.RoundToInt(cy);

        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                float px = baseX + dx - cx;
                float py = baseY + dy - cy;

                // punkt kafelka w układzie ikonki (0..1)
                float u = (cos * px + sin * py) / size + 0.5f;
                float v = (-sin * px + cos * py) / size + 0.5f;

                if (u < 0f || u >= 1f || v < 0f || v >= 1f)
                {
                    continue;
                }

                Sample(icon, iconSize, u, v, out float r, out float g, out float b, out float alpha);

                if (alpha <= 0.5f)
                {
                    continue;
                }

                int x = ((baseX + dx) % PatternSize + PatternSize) % PatternSize;
                int y = ((baseY + dy) % PatternSize + PatternSize) % PatternSize;
                int at = (y * PatternSize + x) * 4;
                float keep = 1f - alpha / 255f;

                canvas[at] = r + canvas[at] * keep;
                canvas[at + 1] = g + canvas[at + 1] * keep;
                canvas[at + 2] = b + canvas[at + 2] * keep;
                canvas[at + 3] = alpha + canvas[at + 3] * keep;
            }
        }
    }

    // Kolor ikonki w punkcie (u, v), wygładzony między czterema sąsiednimi pikselami. Wynik ma pomnożoną alfę (0..255).
    static void Sample(Color32[] icon, int size, float u, float v, out float r, out float g, out float b, out float alpha)
    {
        float fx = u * size - 0.5f;
        float fy = v * size - 0.5f;
        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0;
        float ty = fy - y0;

        r = g = b = alpha = 0f;

        for (int oy = 0; oy <= 1; oy++)
        {
            for (int ox = 0; ox <= 1; ox++)
            {
                int x = x0 + ox;
                int y = y0 + oy;

                if (x < 0 || y < 0 || x >= size || y >= size)
                {
                    continue;
                }

                Color32 pixel = icon[y * size + x];
                float weight = (ox == 0 ? 1f - tx : tx) * (oy == 0 ? 1f - ty : ty) * (pixel.a / 255f);

                r += pixel.r * weight;
                g += pixel.g * weight;
                b += pixel.b * weight;
                alpha += 255f * weight;
            }
        }
    }

    // Mapa cieniowania danej cząstki: z pliku w Resources/ChromaFx, a gdy go nie ma (albo nie da się go odczytać), z kodu.
    static Color32[] ShadeMap(int kind, out int size)
    {
        if (shadeMaps[kind] != null)
        {
            size = shadeSizes[kind];
            return shadeMaps[kind];
        }

        Color32[] map = null;
        size = 0;

        Texture2D file = Resources.Load<Texture2D>("ChromaFx/" + resourceNames[kind]);

        if (file != null && file.width == file.height)
        {
            try
            {
                map = file.GetPixels32();
                size = file.width;
            }
            catch (Exception)
            {
                Debug.LogWarning("ChromaFx: grafika '" + resourceNames[kind] + "' nie ma włączonego Read/Write, rysuję zastępczą.");
                map = null;
            }
        }

        if (map == null)
        {
            size = 64;
            map = DrawFallback(kind, size);
        }

        shadeMaps[kind] = map;
        shadeSizes[kind] = size;
        return map;
    }

    // Zastępczy, prosty kształt bez konturu (jednolity kolor A).
    static Color32[] DrawFallback(int kind, int size)
    {
        Color32[] pixels = new Color32[size * size];
        byte shade = (byte)Mathf.RoundToInt(ShadeA * 255f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float sum = 0f;

                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        sum += Coverage(kind, (x + (sx + 0.5f) / 2f) / size, (y + (sy + 0.5f) / 2f) / size);
                    }
                }

                pixels[y * size + x] = new Color32(shade, shade, shade, (byte)Mathf.Clamp(Mathf.RoundToInt(sum / 4f * 255f), 0, 255));
            }
        }

        return pixels;
    }

    // Kształt cząstki w układzie 0..1 (zwraca krycie 0..1).
    static float Coverage(int kind, float u, float v)
    {
        float x = u - 0.5f;
        float y = v - 0.5f;

        switch (kind)
        {
            case Star:
            case StarFive:
            {
                // czterokierunkowy błysk: jasne ramiona i miękki środek
                float arms = Mathf.Pow(Mathf.Abs(x), 0.55f) + Mathf.Pow(Mathf.Abs(y), 0.55f);
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) / 0.16f);
                return arms <= 0.62f ? 1f : Mathf.Max(core, 0f);
            }
            case Flame:
            {
                // kropla z czubkiem w górę
                bool round = (x * x + (v - 0.34f) * (v - 0.34f)) <= 0.26f * 0.26f;
                bool tip = v > 0.34f && Mathf.Abs(x) <= 0.26f * (0.96f - v) / 0.62f;
                return round || tip ? 1f : 0f;
            }
            case Snowflake:
            {
                // sześć ramion gwiazdki
                float distance = float.MaxValue;

                for (int arm = 0; arm < 3; arm++)
                {
                    float angle = arm * Mathf.PI / 3f;
                    float d = Mathf.Abs(-Mathf.Sin(angle) * x + Mathf.Cos(angle) * y);
                    distance = Mathf.Min(distance, d);
                }

                return Mathf.Sqrt(x * x + y * y) <= 0.46f && distance <= 0.05f ? 1f : 0f;
            }
            case Leaf:
            {
                // wydłużona elipsa obrócona o 45 stopni
                float rx = (x + y) * 0.7071f;
                float ry = (x - y) * 0.7071f;
                return (rx * rx) / (0.44f * 0.44f) + (ry * ry) / (0.17f * 0.17f) <= 1f ? 1f : 0f;
            }
            case Heart:
            {
                float hx = x * 3.0f;
                float hy = (y + 0.02f) * 3.0f;
                float a = hx * hx + hy * hy - 1f;
                return a * a * a - hx * hx * hy * hy * hy <= 0f ? 1f : 0f;
            }
            default:
            {
                // miękka kula: pełny środek i delikatna krawędź
                float r = Mathf.Sqrt(x * x + y * y) / 0.48f;
                return r >= 1f ? 0f : Mathf.Clamp01((1f - r) * 3f);
            }
        }
    }
}
