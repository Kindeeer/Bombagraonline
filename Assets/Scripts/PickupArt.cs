using System;
using System.Collections.Generic;
using UnityEngine;

// Grafika zastępcza rysowana w pamięci, żeby fontanna i przedmioty działały bez własnych obrazków.
public static class PickupArt
{
    class Layers
    {
        public Color color = new Color(1f, 1f, 1f, 0f);

        public void Reset()
        {
            color = new Color(1f, 1f, 1f, 0f);
        }

        // distance mniejsze od 0 oznacza wnętrze kształtu (w pikselach tekstury)
        public void Draw(float distance, Color fill)
        {
            float coverage = Mathf.Clamp01(0.5f - distance) * fill.a;

            if (coverage <= 0f)
            {
                return;
            }

            float keep = color.a * (1f - coverage);
            float outAlpha = coverage + keep;

            color = new Color(
                (fill.r * coverage + color.r * keep) / outAlpha,
                (fill.g * coverage + color.g * keep) / outAlpha,
                (fill.b * coverage + color.b * keep) / outAlpha,
                outAlpha);
        }

        public void Outlined(float distance, Color fill, Color outline, float width)
        {
            Draw(distance - width, outline);
            Draw(distance, fill);
        }
    }

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    static readonly Color Outline = new Color(0.12f, 0.07f, 0.04f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        cache.Clear();
    }

    // ---------- Publiczne obrazki ----------

    // Biała kropla do podmalowania kolorem. Średnica 1 jednostka.
    public static Sprite Drop()
    {
        return Build("drop", 32, 32, 32f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            l.Draw(Circle(x, y, 16f, 16f, 15f), Color.white);
        });
    }

    // Pierścień na powierzchni czekolady. Szerokość 1, wysokość 0,5 jednostki.
    public static Sprite Ripple()
    {
        return Build("ripple", 64, 32, 64f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            l.Draw(Mathf.Abs(Ellipse(x, y, 32f, 16f, 28f, 12f)) - 2f, Color.white);
        });
    }

    public static Sprite Shadow()
    {
        return Build("shadow", 64, 32, 64f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            l.Draw(Ellipse(x, y, 32f, 16f, 28f, 12f), new Color(0f, 0f, 0f, 0.28f));
        });
    }

    // Bąbelek o średnicy około 0,92 jednostki.
    public static Sprite Bubble()
    {
        return Build("bubble", 96, 96, 104f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            l.Draw(Circle(x, y, 48f, 48f, 45f), new Color(0.72f, 0.92f, 1f, 0.30f));
            l.Draw(Mathf.Abs(Circle(x, y, 48f, 48f, 44f)) - 1.8f, new Color(1f, 1f, 1f, 0.9f));
            l.Draw(Ellipse(x, y, 33f, 66f, 10f, 6f), new Color(1f, 1f, 1f, 0.85f));
            l.Draw(Circle(x, y, 63f, 30f, 3f), new Color(1f, 1f, 1f, 0.5f));
        });
    }

    // Ikona zastępcza o średnicy około 0,6 jednostki. None zwraca null (bąbelek bez ikony).
    public static Sprite Icon(PickupIcon icon)
    {
        float ppu = 64f / 0.6f;
        Vector2 center = new Vector2(0.5f, 0.5f);

        switch (icon)
        {
            case PickupIcon.Bomb:
                return Build("icon_bomb", 64, 64, ppu, center, DrawBomb);
            case PickupIcon.Range:
                return Build("icon_range", 64, 64, ppu, center, DrawRange);
            case PickupIcon.Speed:
                return Build("icon_speed", 64, 64, ppu, center, DrawSpeed);
            case PickupIcon.Hammer:
                return Build("icon_hammer", 64, 64, ppu, center, DrawHammer);
            case PickupIcon.Rocket:
                return Build("icon_rocket", 64, 64, ppu, center, DrawRocket);
            case PickupIcon.Skull:
                return Build("icon_skull", 64, 64, ppu, center, DrawSkull);
            default:
                return null;
        }
    }

    // Fontanna: basen, kolumna, miska i dysza. Środek basenu leży w punkcie obrotu.
    // Szerokość 1,25 jednostki, dysza około 0,8 jednostki nad środkiem.
    public static Sprite FountainBody()
    {
        return Build("fountain_body", 160, 160, 128f, new Vector2(0.5f, 36f / 160f), (l, x, y) =>
        {
            Color dark = new Color(0.30f, 0.15f, 0.07f);
            Color milk = new Color(0.56f, 0.31f, 0.15f);
            Color cream = new Color(0.97f, 0.88f, 0.70f);
            Color pool = new Color(0.36f, 0.18f, 0.08f);
            Color shine = new Color(0.80f, 0.50f, 0.26f, 0.6f);

            // basen
            l.Draw(Ellipse(x, y, 80f, 26f, 70f, 28f), dark);
            l.Draw(Box(x, y, 80f, 31f, 70f, 5f), dark);
            l.Outlined(Ellipse(x, y, 80f, 36f, 70f, 28f), cream, dark, 1.5f);
            l.Draw(Ellipse(x, y, 80f, 36f, 61f, 22f), pool);
            l.Draw(Ellipse(x, y, 62f, 40f, 22f, 5f), shine);

            // kolumna
            l.Outlined(Box(x, y, 80f, 78f, 11f, 42f), milk, dark, 1.5f);
            l.Draw(Box(x, y, 76f, 78f, 3f, 38f), shine);

            // miska
            l.Draw(Ellipse(x, y, 80f, 102f, 44f, 14f), dark);
            l.Outlined(Ellipse(x, y, 80f, 110f, 44f, 14f), cream, dark, 1.5f);
            l.Draw(Ellipse(x, y, 80f, 110f, 37f, 10f), pool);
            l.Draw(Ellipse(x, y, 68f, 112f, 14f, 3f), shine);

            // dysza
            l.Outlined(Circle(x, y, 80f, 128f, 9f), milk, dark, 1.5f);
            l.Draw(Circle(x, y, 77f, 131f, 3f), shine);
        });
    }

    // Pionowy strumień z czekolady. Szerokość 0,31 i wysokość 2 jednostki, punkt obrotu na dole.
    public static Sprite Stream()
    {
        return Build("stream", 40, 256, 128f, new Vector2(0.5f, 0f), (l, x, y) =>
        {
            l.Draw(Capsule(x, y, 20f, 20f, 20f, 236f, 18f), new Color(0.40f, 0.20f, 0.09f));
            l.Draw(Capsule(x, y, 20f, 22f, 20f, 234f, 12f), new Color(0.55f, 0.30f, 0.14f));
            l.Draw(Capsule(x, y, 15f, 30f, 15f, 226f, 3.5f), new Color(0.86f, 0.58f, 0.34f, 0.7f));
        });
    }

    // ---------- Interfejs ----------

    // Biały panel z zaokrąglonymi rogami do rozciągania (Image Type = Sliced).
    public static Sprite Panel()
    {
        return Build("ui_panel", 64, 64, 100f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            l.Draw(RoundBox(x, y, 32f, 32f, 31f, 31f, 18f), Color.white);
        }, new Vector4(22f, 22f, 22f, 22f));
    }

    // Klawisz klawiatury: jasna nakładka na ciemniejszym spodzie.
    public static Sprite KeyCap()
    {
        return Build("ui_key", 64, 64, 100f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            l.Draw(RoundBox(x, y, 32f, 30f, 31f, 29f, 12f), new Color(0.45f, 0.48f, 0.56f));
            l.Draw(RoundBox(x, y, 32f, 35f, 28f, 26f, 10f), new Color(0.98f, 0.98f, 1f));
        }, new Vector4(16f, 16f, 16f, 18f));
    }

    // Buźka gracza (biała, do podmalowania kolorem gracza). Martwa ma oczy X.
    public static Sprite Face(bool dead)
    {
        return Build(dead ? "ui_face_dead" : "ui_face", 128, 128, 100f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            Color ink = new Color(0.12f, 0.10f, 0.12f);

            l.Outlined(Circle(x, y, 64f, 64f, 58f), Color.white, ink, 3f);

            if (dead)
            {
                float leftEye = Mathf.Min(Capsule(x, y, 36f, 62f, 52f, 82f, 3.5f), Capsule(x, y, 36f, 82f, 52f, 62f, 3.5f));
                float rightEye = Mathf.Min(Capsule(x, y, 76f, 62f, 92f, 82f, 3.5f), Capsule(x, y, 76f, 82f, 92f, 62f, 3.5f));
                l.Draw(Mathf.Min(leftEye, rightEye), ink);
                l.Draw(Capsule(x, y, 48f, 38f, 80f, 38f, 3.5f), ink);
            }
            else
            {
                l.Draw(Ellipse(x, y, 46f, 74f, 7f, 10f), ink);
                l.Draw(Ellipse(x, y, 82f, 74f, 7f, 10f), ink);
                l.Draw(Mathf.Max(Mathf.Abs(Circle(x, y, 64f, 62f, 26f)) - 3.5f, y - 50f), ink);
            }
        });
    }

    // ---------- Ikony ----------

    static void DrawBomb(Layers l, float x, float y)
    {
        l.Outlined(Capsule(x, y, 36f, 40f, 44f, 50f, 2.2f), new Color(0.65f, 0.45f, 0.20f), Outline, 1.2f);
        l.Outlined(Circle(x, y, 32f, 26f, 17f), new Color(0.16f, 0.16f, 0.20f), Outline, 1.5f);
        l.Draw(Circle(x, y, 26f, 33f, 4.5f), new Color(0.6f, 0.6f, 0.75f, 0.9f));
        l.Outlined(Circle(x, y, 46f, 52f, 5f), new Color(1f, 0.8f, 0.15f), new Color(0.9f, 0.35f, 0.05f), 1.2f);
    }

    static void DrawRange(Layers l, float x, float y)
    {
        float cross = Mathf.Min(Box(x, y, 32f, 32f, 18f, 5f), Box(x, y, 32f, 32f, 5f, 18f));
        l.Outlined(cross, new Color(1f, 0.55f, 0.10f), Outline, 1.5f);
        l.Draw(Circle(x, y, 32f, 32f, 6f), new Color(1f, 0.9f, 0.3f));
    }

    static void DrawSpeed(Layers l, float x, float y)
    {
        float first = Mathf.Min(Capsule(x, y, 16f, 16f, 29f, 32f, 3.6f), Capsule(x, y, 29f, 32f, 16f, 48f, 3.6f));
        float second = Mathf.Min(Capsule(x, y, 33f, 16f, 46f, 32f, 3.6f), Capsule(x, y, 46f, 32f, 33f, 48f, 3.6f));
        l.Outlined(Mathf.Min(first, second), new Color(0.35f, 0.90f, 0.45f), Outline, 1.5f);
    }

    static void DrawHammer(Layers l, float x, float y)
    {
        l.Outlined(Capsule(x, y, 20f, 14f, 38f, 40f, 3.8f), new Color(0.60f, 0.38f, 0.18f), Outline, 1.5f);
        l.Outlined(RotatedBox(x, y, 40f, 44f, 7f, 15f, 57.3f), new Color(0.72f, 0.75f, 0.80f), Outline, 1.5f);
        l.Draw(RotatedBox(x, y, 38f, 46f, 2.2f, 11f, 57.3f), new Color(1f, 1f, 1f, 0.45f));
    }

    static void DrawRocket(Layers l, float x, float y)
    {
        Color red = new Color(0.90f, 0.25f, 0.22f);

        l.Outlined(Circle(x, y, 32f, 11f, 5.5f), new Color(1f, 0.65f, 0.15f), Outline, 1.2f);
        l.Outlined(RotatedBox(x, y, 23f, 22f, 3.5f, 8f, -25f), new Color(0.65f, 0.15f, 0.15f), Outline, 1.2f);
        l.Outlined(RotatedBox(x, y, 41f, 22f, 3.5f, 8f, 25f), new Color(0.65f, 0.15f, 0.15f), Outline, 1.2f);
        l.Outlined(Box(x, y, 32f, 31f, 8f, 15f), red, Outline, 1.5f);
        l.Outlined(RotatedBox(x, y, 32f, 46f, 5.8f, 5.8f, 45f), new Color(1f, 0.45f, 0.40f), Outline, 1.5f);
        l.Draw(Box(x, y, 32f, 40f, 8f, 3f), red);
        l.Outlined(Circle(x, y, 32f, 32f, 4.2f), new Color(0.6f, 0.85f, 1f), Outline, 1f);
    }

    static void DrawSkull(Layers l, float x, float y)
    {
        Color bone = new Color(0.95f, 0.93f, 0.88f);
        Color hole = new Color(0.15f, 0.10f, 0.12f);

        l.Outlined(Mathf.Min(Circle(x, y, 32f, 36f, 17f), Box(x, y, 32f, 20f, 10f, 7f)), bone, Outline, 1.5f);
        l.Draw(Circle(x, y, 25f, 36f, 5f), hole);
        l.Draw(Circle(x, y, 39f, 36f, 5f), hole);
        l.Draw(RotatedBox(x, y, 32f, 27f, 2.5f, 2.5f, 45f), hole);
        l.Draw(Box(x, y, 28f, 16f, 1.2f, 4f), hole);
        l.Draw(Box(x, y, 36f, 16f, 1.2f, 4f), hole);
    }

    // ---------- Kształty (odległość ze znakiem, w pikselach) ----------

    static float Circle(float x, float y, float cx, float cy, float r)
    {
        float dx = x - cx;
        float dy = y - cy;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx;
        float dy = (y - cy) / ry;
        return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
    }

    static float Box(float x, float y, float cx, float cy, float halfWidth, float halfHeight)
    {
        float dx = Mathf.Abs(x - cx) - halfWidth;
        float dy = Mathf.Abs(y - cy) - halfHeight;
        float ox = Mathf.Max(dx, 0f);
        float oy = Mathf.Max(dy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f);
    }

    static float RoundBox(float x, float y, float cx, float cy, float halfWidth, float halfHeight, float radius)
    {
        return Box(x, y, cx, cy, halfWidth - radius, halfHeight - radius) - radius;
    }

    static float RotatedBox(float x, float y, float cx, float cy, float halfWidth, float halfHeight, float degrees)
    {
        float angle = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        float dx = x - cx;
        float dy = y - cy;
        return Box(dx * cos + dy * sin, -dx * sin + dy * cos, 0f, 0f, halfWidth, halfHeight);
    }

    static float Capsule(float x, float y, float ax, float ay, float bx, float by, float radius)
    {
        float px = x - ax;
        float py = y - ay;
        float dx = bx - ax;
        float dy = by - ay;
        float t = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
        float cx = px - dx * t;
        float cy = py - dy * t;
        return Mathf.Sqrt(cx * cx + cy * cy) - radius;
    }

    // ---------- Budowanie obrazka ----------

    static Sprite Build(string key, int width, int height, float pixelsPerUnit, Vector2 pivot, Action<Layers, float, float> draw,
        Vector4 border = default)
    {
        if (cache.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.DontSave;

        Color[] pixels = new Color[width * height];
        Layers layers = new Layers();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                layers.Reset();
                draw(layers, x + 0.5f, y + 0.5f);
                pixels[y * width + x] = layers.color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, pixelsPerUnit, 0,
            SpriteMeshType.FullRect, border);
        sprite.hideFlags = HideFlags.DontSave;
        sprite.name = key;

        cache[key] = sprite;
        return sprite;
    }
}
