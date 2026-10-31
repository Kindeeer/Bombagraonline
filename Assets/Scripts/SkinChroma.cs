using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChromaRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

public enum ChromaMode
{
    [Tooltip("Kolory zwierzaka zamieniają się na paletę trzech kolorów (ciemny, średni, jasny) według jasności.")]
    Gradient,
    [Tooltip("Tęcza przechodząca przez całe ciało.")]
    Rainbow,
    [Tooltip("Dwie palety: dolna część ciała ma pierwszą, górna drugą, a między nimi płynne przejście.")]
    Vertical
}

// Motyw chromy. Skrzynka motywowa losuje tylko chromy ze swojego motywu.
public enum ChromaTheme
{
    Classic,
    Ice,
    Fire,
    Nature,
    Candy,
    Cosmic
}

// Chroma to wariant kolorystyczny skina (np. tęczowy Bóbr). Domyślnie gra przekolorowuje grafiki skina w locie,
// a gdy grafik narysuje własne sprite'y chromy, one zastępują przekolorowanie (osobno dla każdego kierunku i buźki).
[Serializable]
public class SkinChroma
{
    public string label = "Chroma";
    [Tooltip("Nazwa po angielsku (w polskiej wersji pokazuje się Label).")]
    public string english = "";
    public ChromaRarity rarity = ChromaRarity.Common;
    public ChromaTheme theme = ChromaTheme.Classic;
    public ChromaMode mode = ChromaMode.Gradient;
    [Tooltip("Tryb Gradient i dolna część w trybie Vertical: cienie, środek i światła postaci.")]
    public Color dark = new Color(0.12f, 0.04f, 0.25f);
    public Color mid = new Color(0.55f, 0.30f, 0.85f);
    public Color light = new Color(0.92f, 0.82f, 1f);
    [Tooltip("Tryb Vertical: paleta górnej części ciała.")]
    public Color dark2 = Color.black;
    public Color mid2 = Color.gray;
    public Color light2 = Color.white;
    [Tooltip("Tryb Rainbow: nasycenie kolorów (mniej = pastelowa tęcza).")]
    [Range(0.1f, 1f)] public float saturation = 0.62f;

    [Header("Efekt wizualny (błyski, ogień, śnieg...)")]
    public ChromaEffect effect = ChromaEffect.None;
    public Color effectColor = Color.white;
    public Color effectColor2 = Color.white;
    [Tooltip("Losowe kolory tęczy zamiast dwóch kolorów efektu.")]
    public bool effectRainbow;
    [Range(0.2f, 3f)] public float effectStrength = 1f;
    [Tooltip("Połysk jak zaklęcie w Minecrafcie (shader): po postaci przesuwają się pasy światła.")]
    public bool glint;
    public Color glintColor = new Color(0.7f, 0.5f, 1f);
    public bool glintRainbow;

    public ChromaFxLook Fx => new ChromaFxLook
    {
        effect = effect,
        colorA = effectColor,
        colorB = effectColor2,
        rainbow = effectRainbow,
        strength = effectStrength,
        glint = glint,
        glintColor = glintColor,
        glintRainbow = glintRainbow
    };

    [Header("Własne grafiki chromy (opcjonalnie, puste = przekolorowanie z kodu)")]
    public Sprite front;
    public Sprite back;
    public Sprite left;
    public Sprite right;
    public Sprite portrait;
    public Sprite portraitDead;

    public SkinChroma() { }

    // Wartość identyfikująca wygląd przekolorowania (do pamięci podręcznej).
    public int LookKey
    {
        get
        {
            HashCode hash = new HashCode();
            hash.Add((int)mode);
            hash.Add(dark);
            hash.Add(mid);
            hash.Add(light);
            hash.Add(dark2);
            hash.Add(mid2);
            hash.Add(light2);
            hash.Add(saturation);
            return hash.ToHashCode();
        }
    }

    static Color C(float r, float g, float b)
    {
        return new Color(r, g, b);
    }

    static SkinChroma Gradient(string label, string english, ChromaRarity rarity, ChromaTheme theme, Color dark, Color mid, Color light)
    {
        return new SkinChroma { label = label, english = english, rarity = rarity, theme = theme, mode = ChromaMode.Gradient, dark = dark, mid = mid, light = light };
    }

    static SkinChroma Vertical(string label, string english, ChromaRarity rarity, ChromaTheme theme,
        Color dark, Color mid, Color light, Color dark2, Color mid2, Color light2)
    {
        return new SkinChroma
        {
            label = label, english = english, rarity = rarity, theme = theme, mode = ChromaMode.Vertical,
            dark = dark, mid = mid, light = light, dark2 = dark2, mid2 = mid2, light2 = light2
        };
    }

    static SkinChroma Rainbow(string label, string english, ChromaRarity rarity, ChromaTheme theme, float saturation)
    {
        return new SkinChroma { label = label, english = english, rarity = rarity, theme = theme, mode = ChromaMode.Rainbow, saturation = saturation };
    }

    static void SetGlint(string label, Color color, bool rainbow = false)
    {
        foreach (SkinChroma chroma in defaults)
        {
            if (chroma.label == label)
            {
                chroma.glint = true;
                chroma.glintColor = color;
                chroma.glintRainbow = rainbow;
            }
        }
    }

    static void SetFx(string label, ChromaEffect effect, Color a, Color b, float strength, bool rainbow = false)
    {
        foreach (SkinChroma chroma in defaults)
        {
            if (chroma.label == label)
            {
                chroma.effect = effect;
                chroma.effectColor = a;
                chroma.effectColor2 = b;
                chroma.effectStrength = strength;
                chroma.effectRainbow = rainbow;
            }
        }
    }

    // Chromy, które dostaje każdy skin, dopóki w GameArt nie ma własnej listy. Pierwsze cztery są w tej kolejności od początku
    // (zapisy graczy liczą chromy po numerze), więc nowe dopisujemy tylko na końcu.
    static SkinChroma[] defaults;

    public static SkinChroma[] Defaults
    {
        get
        {
            if (defaults == null)
            {
                defaults = new[]
                {
                    // 0-3: pierwsze chromy
                    Gradient("Fioletowy", "Purple", ChromaRarity.Common, ChromaTheme.Classic, C(0.12f, 0.04f, 0.25f), C(0.55f, 0.30f, 0.85f), C(0.92f, 0.82f, 1f)),
                    Gradient("Lodowy", "Icy", ChromaRarity.Rare, ChromaTheme.Ice, C(0.03f, 0.15f, 0.30f), C(0.30f, 0.70f, 0.95f), C(0.90f, 0.98f, 1f)),
                    Gradient("Złoty", "Golden", ChromaRarity.Epic, ChromaTheme.Classic, C(0.30f, 0.17f, 0.02f), C(0.95f, 0.70f, 0.12f), C(1f, 0.95f, 0.60f)),
                    Rainbow("Tęczowy", "Rainbow", ChromaRarity.Legendary, ChromaTheme.Classic, 0.62f),

                    // klasyczne
                    Gradient("Czekoladowy", "Chocolate", ChromaRarity.Common, ChromaTheme.Classic, C(0.10f, 0.05f, 0.02f), C(0.45f, 0.25f, 0.12f), C(0.85f, 0.62f, 0.45f)),
                    Gradient("Srebrny", "Silver", ChromaRarity.Rare, ChromaTheme.Classic, C(0.10f, 0.11f, 0.14f), C(0.55f, 0.58f, 0.65f), C(0.96f, 0.97f, 1f)),

                    // lód
                    Gradient("Szron", "Frost", ChromaRarity.Common, ChromaTheme.Ice, C(0.10f, 0.18f, 0.30f), C(0.55f, 0.75f, 0.90f), C(0.97f, 1f, 1f)),
                    Vertical("Zorza", "Aurora", ChromaRarity.Epic, ChromaTheme.Ice,
                        C(0.02f, 0.15f, 0.12f), C(0.20f, 0.85f, 0.55f), C(0.85f, 1f, 0.9f),
                        C(0.10f, 0.04f, 0.25f), C(0.60f, 0.35f, 0.95f), C(0.95f, 0.85f, 1f)),
                    Vertical("Diamentowy", "Diamond", ChromaRarity.Legendary, ChromaTheme.Ice,
                        C(0.05f, 0.20f, 0.30f), C(0.40f, 0.90f, 1f), C(0.95f, 1f, 1f),
                        C(0.30f, 0.10f, 0.30f), C(1f, 0.60f, 0.95f), C(1f, 0.95f, 1f)),

                    // ogień
                    Gradient("Żar", "Ember", ChromaRarity.Common, ChromaTheme.Fire, C(0.12f, 0.04f, 0.02f), C(0.65f, 0.20f, 0.05f), C(1f, 0.65f, 0.25f)),
                    Gradient("Ognisty", "Fiery", ChromaRarity.Rare, ChromaTheme.Fire, C(0.20f, 0.02f, 0.02f), C(0.95f, 0.25f, 0.08f), C(1f, 0.85f, 0.30f)),
                    Gradient("Magma", "Magma", ChromaRarity.Epic, ChromaTheme.Fire, C(0.04f, 0.02f, 0.02f), C(0.85f, 0.12f, 0.05f), C(1f, 0.90f, 0.40f)),
                    Vertical("Feniks", "Phoenix", ChromaRarity.Legendary, ChromaTheme.Fire,
                        C(0.20f, 0.02f, 0.02f), C(0.95f, 0.20f, 0.10f), C(1f, 0.70f, 0.30f),
                        C(0.50f, 0.25f, 0.02f), C(1f, 0.75f, 0.15f), C(1f, 1f, 0.75f)),

                    // natura
                    Gradient("Leśny", "Forest", ChromaRarity.Common, ChromaTheme.Nature, C(0.04f, 0.12f, 0.05f), C(0.20f, 0.55f, 0.22f), C(0.80f, 0.95f, 0.65f)),
                    Gradient("Toksyczny", "Toxic", ChromaRarity.Rare, ChromaTheme.Nature, C(0.02f, 0.06f, 0.02f), C(0.35f, 0.95f, 0.15f), C(0.90f, 1f, 0.60f)),
                    Vertical("Jesień", "Autumn", ChromaRarity.Epic, ChromaTheme.Nature,
                        C(0.15f, 0.06f, 0.02f), C(0.65f, 0.30f, 0.08f), C(0.95f, 0.70f, 0.35f),
                        C(0.35f, 0.12f, 0.02f), C(0.95f, 0.45f, 0.10f), C(1f, 0.85f, 0.40f)),
                    Vertical("Kwiecisty", "Blossom", ChromaRarity.Legendary, ChromaTheme.Nature,
                        C(0.05f, 0.20f, 0.08f), C(0.35f, 0.75f, 0.30f), C(0.85f, 1f, 0.70f),
                        C(0.40f, 0.10f, 0.25f), C(1f, 0.55f, 0.75f), C(1f, 0.92f, 0.95f)),

                    // cukierki
                    Gradient("Truskawkowy", "Strawberry", ChromaRarity.Common, ChromaTheme.Candy, C(0.35f, 0.04f, 0.10f), C(0.95f, 0.30f, 0.40f), C(1f, 0.85f, 0.88f)),
                    Gradient("Jagodowy", "Berry", ChromaRarity.Rare, ChromaTheme.Candy, C(0.20f, 0.04f, 0.25f), C(0.75f, 0.25f, 0.65f), C(1f, 0.80f, 0.95f)),
                    Vertical("Wata cukrowa", "Cotton Candy", ChromaRarity.Epic, ChromaTheme.Candy,
                        C(0.20f, 0.30f, 0.50f), C(0.55f, 0.75f, 1f), C(0.95f, 0.98f, 1f),
                        C(0.50f, 0.20f, 0.40f), C(1f, 0.65f, 0.85f), C(1f, 0.95f, 0.98f)),
                    Rainbow("Pastelowa tęcza", "Pastel Rainbow", ChromaRarity.Legendary, ChromaTheme.Candy, 0.35f),

                    // kosmos
                    Gradient("Nocny", "Night", ChromaRarity.Common, ChromaTheme.Cosmic, C(0.02f, 0.02f, 0.10f), C(0.12f, 0.20f, 0.50f), C(0.55f, 0.70f, 0.95f)),
                    Gradient("Neonowy", "Neon", ChromaRarity.Rare, ChromaTheme.Cosmic, C(0.04f, 0f, 0.10f), C(0.95f, 0.10f, 0.85f), C(0.30f, 1f, 1f)),
                    Vertical("Galaktyka", "Galaxy", ChromaRarity.Epic, ChromaTheme.Cosmic,
                        C(0.05f, 0.02f, 0.15f), C(0.45f, 0.15f, 0.75f), C(0.85f, 0.70f, 1f),
                        C(0.02f, 0.08f, 0.20f), C(0.10f, 0.60f, 0.90f), C(0.80f, 1f, 1f)),
                    Rainbow("Supernowa", "Supernova", ChromaRarity.Legendary, ChromaTheme.Cosmic, 0.95f)
                };

                // efekty wizualne wokół postaci
                SetFx("Złoty", ChromaEffect.Sparkle, C(1f, 0.9f, 0.4f), C(1f, 1f, 0.8f), 1f);
                SetFx("Srebrny", ChromaEffect.Sparkle, C(0.9f, 0.95f, 1f), Color.white, 0.7f);
                SetFx("Tęczowy", ChromaEffect.Sparkle, Color.white, Color.white, 1.2f, true);
                SetFx("Lodowy", ChromaEffect.Snow, C(0.8f, 0.95f, 1f), Color.white, 0.8f);
                SetFx("Szron", ChromaEffect.Snow, Color.white, C(0.8f, 0.9f, 1f), 0.6f);
                SetFx("Zorza", ChromaEffect.Sparkle, C(0.4f, 1f, 0.7f), C(0.8f, 0.5f, 1f), 1f);
                SetFx("Diamentowy", ChromaEffect.Sparkle, C(0.7f, 1f, 1f), C(1f, 0.8f, 1f), 1.6f);
                SetFx("Żar", ChromaEffect.Fire, C(1f, 0.5f, 0.1f), C(0.4f, 0.1f, 0.05f), 0.5f);
                SetFx("Ognisty", ChromaEffect.Fire, C(1f, 0.6f, 0.1f), C(0.8f, 0.1f, 0.05f), 1f);
                SetFx("Magma", ChromaEffect.Fire, C(1f, 0.4f, 0.05f), C(0.3f, 0.02f, 0.02f), 1.3f);
                SetFx("Feniks", ChromaEffect.Fire, C(1f, 0.9f, 0.4f), C(1f, 0.25f, 0.05f), 1.8f);
                SetFx("Leśny", ChromaEffect.Leaves, C(0.4f, 0.8f, 0.3f), C(0.7f, 0.95f, 0.4f), 0.6f);
                SetFx("Toksyczny", ChromaEffect.Bubbles, C(0.5f, 1f, 0.2f), C(0.2f, 0.8f, 0.1f), 1f);
                SetFx("Jesień", ChromaEffect.Leaves, C(0.95f, 0.5f, 0.1f), C(0.7f, 0.25f, 0.05f), 1f);
                SetFx("Kwiecisty", ChromaEffect.Leaves, C(1f, 0.6f, 0.8f), C(1f, 0.85f, 0.9f), 1.3f);
                SetFx("Truskawkowy", ChromaEffect.Hearts, C(1f, 0.3f, 0.4f), C(1f, 0.6f, 0.7f), 0.8f);
                SetFx("Jagodowy", ChromaEffect.Sparkle, C(0.9f, 0.4f, 0.9f), C(1f, 0.7f, 1f), 0.8f);
                SetFx("Wata cukrowa", ChromaEffect.Hearts, C(1f, 0.7f, 0.9f), C(0.7f, 0.85f, 1f), 1f);
                SetFx("Pastelowa tęcza", ChromaEffect.Hearts, Color.white, Color.white, 1.2f, true);
                SetFx("Nocny", ChromaEffect.Stars, C(0.7f, 0.8f, 1f), Color.white, 0.6f);
                SetFx("Neonowy", ChromaEffect.Sparkle, C(1f, 0.2f, 0.9f), C(0.2f, 1f, 1f), 1.2f);
                SetFx("Galaktyka", ChromaEffect.Stars, C(0.7f, 0.5f, 1f), C(0.4f, 0.9f, 1f), 1.3f);
                SetFx("Supernowa", ChromaEffect.Stars, Color.white, Color.white, 1.8f, true);

                // połysk "zaklęcia": rzadsze chromy mają przesuwające się pasy światła
                SetGlint("Złoty", C(1f, 0.85f, 0.4f));
                SetGlint("Srebrny", C(0.8f, 0.9f, 1f));
                SetGlint("Lodowy", C(0.6f, 0.9f, 1f));
                SetGlint("Zorza", C(0.4f, 1f, 0.8f));
                SetGlint("Diamentowy", C(0.7f, 1f, 1f));
                SetGlint("Ognisty", C(1f, 0.5f, 0.1f));
                SetGlint("Magma", C(1f, 0.45f, 0.1f));
                SetGlint("Feniks", C(1f, 0.7f, 0.2f));
                SetGlint("Jesień", C(1f, 0.65f, 0.2f));
                SetGlint("Kwiecisty", C(1f, 0.6f, 0.85f));
                SetGlint("Wata cukrowa", C(1f, 0.7f, 0.95f));
                SetGlint("Neonowy", C(1f, 0.2f, 0.9f));
                SetGlint("Galaktyka", C(0.6f, 0.5f, 1f));
                SetGlint("Tęczowy", Color.white, true);
                SetGlint("Pastelowa tęcza", Color.white, true);
                SetGlint("Supernowa", Color.white, true);
            }

            return defaults;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        defaults = null;
    }

    public static string RarityName(ChromaRarity rarity)
    {
        return Loc.T("rarity." + rarity);
    }

    public static Color RarityColor(ChromaRarity rarity)
    {
        switch (rarity)
        {
            case ChromaRarity.Rare: return new Color(0.30f, 0.65f, 1f);
            case ChromaRarity.Epic: return new Color(0.78f, 0.40f, 1f);
            case ChromaRarity.Legendary: return new Color(1f, 0.72f, 0.15f);
            default: return new Color(0.75f, 0.80f, 0.78f);
        }
    }

    // Waga losowania ze skrzynki (im większa, tym częściej wypada).
    public static int RarityWeight(ChromaRarity rarity)
    {
        switch (rarity)
        {
            case ChromaRarity.Rare: return 28;
            case ChromaRarity.Epic: return 13;
            case ChromaRarity.Legendary: return 4;
            default: return 55;
        }
    }
}

// Przekolorowuje sprite'y zwierzaków w locie i zapamiętuje wyniki (jedna kopia na parę sprite + chroma).
public static class ChromaBaker
{
    static readonly Dictionary<(Sprite sprite, int look), Sprite> cache = new Dictionary<(Sprite, int), Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        cache.Clear();
    }

    public static Sprite Recolor(Sprite source, SkinChroma chroma)
    {
        if (source == null || chroma == null)
        {
            return source;
        }

        (Sprite, int) key = (source, chroma.LookKey);

        if (cache.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite baked = Bake(source, chroma);
        cache[key] = baked;
        return baked;
    }

    static Sprite Bake(Sprite source, SkinChroma chroma)
    {
        Rect rect = source.rect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);

        // tekstura sprite'a nie musi być czytelna z CPU, więc kopiujemy ją przez kartę graficzną
        Texture2D texture = source.texture;
        RenderTexture target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;

        Graphics.Blit(texture, target);
        RenderTexture.active = target;

        Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);
        copy.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        Color32[] pixels = copy.GetPixels32();
        UnityEngine.Object.Destroy(copy);

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0)
            {
                continue;
            }

            int x = i % width;
            int y = i / width;
            pixels[i] = Shade(pixels[i], chroma, x / (float)width, y / (float)height);
        }

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, true)
        {
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = source.name + "_" + chroma.label
        };

        result.SetPixels32(pixels);
        result.Apply(true);

        Vector2 pivot = new Vector2(source.pivot.x / width, source.pivot.y / height);
        Sprite sprite = Sprite.Create(result, new Rect(0f, 0f, width, height), pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = result.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    static Color Ramp(Color dark, Color mid, Color light, float t)
    {
        return t < 0.55f ? Color.Lerp(dark, mid, t / 0.55f) : Color.Lerp(mid, light, (t - 0.55f) / 0.45f);
    }

    static Color32 Shade(Color32 pixel, SkinChroma chroma, float nx, float ny)
    {
        float lum = (0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b) / 255f;
        float t = Mathf.Clamp01(lum);
        Color color;

        if (chroma.mode == ChromaMode.Rainbow)
        {
            float hue = Mathf.Repeat(ny * 1.3f + nx * 0.4f, 1f);
            float value = Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(lum * 1.15f));
            float saturation = lum < 0.12f ? 0f : chroma.saturation;
            color = Color.HSVToRGB(hue, saturation, value);
        }
        else if (chroma.mode == ChromaMode.Vertical)
        {
            // dół ciała ma pierwszą paletę, góra drugą, a w środku płynne przejście
            Color bottom = Ramp(chroma.dark, chroma.mid, chroma.light, t);
            Color top = Ramp(chroma.dark2, chroma.mid2, chroma.light2, t);
            color = Color.Lerp(bottom, top, Mathf.SmoothStep(0.15f, 0.85f, ny));
        }
        else
        {
            color = Ramp(chroma.dark, chroma.mid, chroma.light, t);
        }

        return new Color32((byte)Mathf.RoundToInt(color.r * 255f), (byte)Mathf.RoundToInt(color.g * 255f),
            (byte)Mathf.RoundToInt(color.b * 255f), pixel.a);
    }
}
