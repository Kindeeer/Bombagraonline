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
    Rainbow
}

// Chroma to wariant kolorystyczny skina (np. tęczowy Bóbr). Domyślnie gra przekolorowuje grafiki skina w locie,
// a gdy grafik narysuje własne sprite'y chromy, one zastępują przekolorowanie (osobno dla każdego kierunku i buźki).
[Serializable]
public class SkinChroma
{
    public string label = "Chroma";
    public ChromaRarity rarity = ChromaRarity.Common;
    public ChromaMode mode = ChromaMode.Gradient;
    [Tooltip("Tryb Gradient: cienie, środek i światła postaci.")]
    public Color dark = new Color(0.12f, 0.04f, 0.25f);
    public Color mid = new Color(0.55f, 0.30f, 0.85f);
    public Color light = new Color(0.92f, 0.82f, 1f);

    [Header("Własne grafiki chromy (opcjonalnie, puste = przekolorowanie z kodu)")]
    public Sprite front;
    public Sprite back;
    public Sprite left;
    public Sprite right;
    public Sprite portrait;
    public Sprite portraitDead;

    public SkinChroma() { }

    SkinChroma(string label, ChromaRarity rarity, ChromaMode mode, Color dark, Color mid, Color light)
    {
        this.label = label;
        this.rarity = rarity;
        this.mode = mode;
        this.dark = dark;
        this.mid = mid;
        this.light = light;
    }

    // Wartość identyfikująca wygląd przekolorowania (do pamięci podręcznej).
    public int LookKey => HashCode.Combine((int)mode, dark, mid, light);

    // Cztery chromy, które dostaje każdy skin, dopóki w GameArt nie ma własnej listy.
    static SkinChroma[] defaults;

    public static SkinChroma[] Defaults
    {
        get
        {
            if (defaults == null)
            {
                defaults = new[]
                {
                    new SkinChroma("Fioletowy", ChromaRarity.Common, ChromaMode.Gradient,
                        new Color(0.12f, 0.04f, 0.25f), new Color(0.55f, 0.30f, 0.85f), new Color(0.92f, 0.82f, 1f)),
                    new SkinChroma("Lodowy", ChromaRarity.Rare, ChromaMode.Gradient,
                        new Color(0.03f, 0.15f, 0.30f), new Color(0.30f, 0.70f, 0.95f), new Color(0.90f, 0.98f, 1f)),
                    new SkinChroma("Złoty", ChromaRarity.Epic, ChromaMode.Gradient,
                        new Color(0.30f, 0.17f, 0.02f), new Color(0.95f, 0.70f, 0.12f), new Color(1f, 0.95f, 0.60f)),
                    new SkinChroma("Tęczowy", ChromaRarity.Legendary, ChromaMode.Rainbow,
                        Color.black, Color.white, Color.white)
                };
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

    static Color32 Shade(Color32 pixel, SkinChroma chroma, float nx, float ny)
    {
        float lum = (0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b) / 255f;
        Color color;

        if (chroma.mode == ChromaMode.Rainbow)
        {
            float hue = Mathf.Repeat(ny * 1.3f + nx * 0.4f, 1f);
            float value = Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(lum * 1.15f));
            float saturation = lum < 0.12f ? 0f : 0.62f;
            color = Color.HSVToRGB(hue, saturation, value);
        }
        else
        {
            float t = Mathf.Clamp01(lum);
            color = t < 0.55f ? Color.Lerp(chroma.dark, chroma.mid, t / 0.55f) : Color.Lerp(chroma.mid, chroma.light, (t - 0.55f) / 0.45f);
        }

        return new Color32((byte)Mathf.RoundToInt(color.r * 255f), (byte)Mathf.RoundToInt(color.g * 255f),
            (byte)Mathf.RoundToInt(color.b * 255f), pixel.a);
    }
}
