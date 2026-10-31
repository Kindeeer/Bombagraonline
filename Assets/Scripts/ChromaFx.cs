using System;
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

    [Tooltip("Połysk jak zaklęcie w Minecrafcie: po postaci przesuwają się pasy światła.")]
    public bool glint;
    public Color glintColor;
    public bool glintRainbow;

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
    public bool colorOverLife;
}

public static class ChromaFx
{
    public const int Star = 0;
    public const int Flame = 1;
    public const int Snowflake = 2;
    public const int Leaf = 3;
    public const int Glow = 4;
    public const int Heart = 5;

    static readonly Sprite[] sprites = new Sprite[6];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Array.Clear(sprites, 0, sprites.Length);
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
                return new FxStyle
                {
                    sprite = Flame, rate = 22f, life = 0.75f, velXMin = -0.06f, velXMax = 0.06f, velYMin = 0.35f, velYMax = 0.75f,
                    sizeMin = 0.12f, sizeMax = 0.22f, sway = 0.15f, spawnX = 0.28f, spawnYMin = 0.05f, spawnYMax = 0.65f,
                    shrink = true, colorOverLife = true
                };
            case ChromaEffect.Snow:
                return new FxStyle
                {
                    sprite = Snowflake, rate = 8f, life = 1.8f, velXMin = -0.05f, velXMax = 0.05f, velYMin = -0.45f, velYMax = -0.22f,
                    sizeMin = 0.08f, sizeMax = 0.15f, sway = 0.18f, spawnX = 0.5f, spawnYMin = 1.0f, spawnYMax = 1.15f, spin = 120f
                };
            case ChromaEffect.Leaves:
                return new FxStyle
                {
                    sprite = Leaf, rate = 6f, life = 2.0f, velXMin = -0.04f, velXMax = 0.04f, velYMin = -0.35f, velYMax = -0.18f,
                    sizeMin = 0.10f, sizeMax = 0.17f, sway = 0.25f, spawnX = 0.5f, spawnYMin = 1.0f, spawnYMax = 1.15f, spin = 160f
                };
            case ChromaEffect.Bubbles:
                return new FxStyle
                {
                    sprite = Glow, rate = 10f, life = 1.2f, velXMin = -0.03f, velXMax = 0.03f, velYMin = 0.2f, velYMax = 0.45f,
                    sizeMin = 0.06f, sizeMax = 0.14f, sway = 0.12f, spawnX = 0.35f, spawnYMin = 0f, spawnYMax = 0.3f
                };
            case ChromaEffect.Hearts:
                return new FxStyle
                {
                    sprite = Heart, rate = 5f, life = 1.3f, velXMin = -0.03f, velXMax = 0.03f, velYMin = 0.2f, velYMax = 0.35f,
                    sizeMin = 0.09f, sizeMax = 0.15f, sway = 0.12f, spawnX = 0.35f, spawnYMin = 0.3f, spawnYMax = 0.9f
                };
            case ChromaEffect.Stars:
                return new FxStyle
                {
                    sprite = Star, rate = 12f, life = 1.2f, velXMin = -0.04f, velXMax = 0.04f, velYMin = -0.03f, velYMax = 0.06f,
                    sizeMin = 0.06f, sizeMax = 0.12f, sway = 0.05f, spawnX = 0.7f, spawnYMin = -0.1f, spawnYMax = 1.2f, twinkle = true
                };
            default:
                return new FxStyle
                {
                    sprite = Star, rate = 9f, life = 0.9f, velXMin = -0.05f, velXMax = 0.05f, velYMin = 0.02f, velYMax = 0.15f,
                    sizeMin = 0.10f, sizeMax = 0.20f, sway = 0.02f, spawnX = 0.4f, spawnYMin = 0.05f, spawnYMax = 1.0f, twinkle = true
                };
        }
    }

    public static Color PickColor(ChromaFxLook look)
    {
        return look.rainbow ? Color.HSVToRGB(UnityEngine.Random.value, 0.6f, 1f) : Color.Lerp(look.colorA, look.colorB, UnityEngine.Random.value);
    }

    // Biały sprite cząstki (rysowany w kodzie, barwiony kolorem).
    public static Sprite SpriteOf(int kind)
    {
        if (sprites[kind] != null)
        {
            return sprites[kind];
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];

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

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(sum / 4f * 255f), 0, 255));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        sprites[kind] = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprites[kind].hideFlags = HideFlags.HideAndDontSave;
        return sprites[kind];
    }

    // Kształt cząstki w układzie 0..1 (zwraca krycie 0..1).
    static float Coverage(int kind, float u, float v)
    {
        float x = u - 0.5f;
        float y = v - 0.5f;

        switch (kind)
        {
            case Star:
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
                // miękka kula (bąbelek): pełny środek i delikatna krawędź
                float r = Mathf.Sqrt(x * x + y * y) / 0.48f;
                return r >= 1f ? 0f : Mathf.Clamp01((1f - r) * 3f);
            }
        }
    }
}
