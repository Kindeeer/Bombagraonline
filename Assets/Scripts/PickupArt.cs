using System;
using System.Collections.Generic;
using UnityEngine;

// Grafika zastępcza rysowana w pamięci, żeby fontanna i przedmioty działały bez własnych obrazków.
public static class PickupArt
{
    // Ile razy większa (w pikselach) od pierwotnego rozmiaru jest tekstura. Kształty liczymy w pierwotnych jednostkach,
    // a obrazek powstaje w większej rozdzielczości, więc przy tym samym rozmiarze na ekranie jest ostrzejszy.
    public const int Scale = 3;

    class Layers
    {
        public Color color = new Color(1f, 1f, 1f, 0f);
        // ile pikseli tekstury przypada na jedną pierwotną jednostkę (do wygładzania krawędzi)
        public float scale = 1f;

        public void Reset()
        {
            color = new Color(1f, 1f, 1f, 0f);
        }

        // distance mniejsze od 0 oznacza wnętrze kształtu (w pierwotnych jednostkach)
        public void Draw(float distance, Color fill)
        {
            float coverage = Mathf.Clamp01(0.5f - distance * scale) * fill.a;

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
    // Grafika z obiektu GameArt (jedno miejsce na wszystkie sprite'y). Puste pole = null i gra rysuje grafikę zastępczą.
    static Sprite Custom(Func<GameArt, Sprite> pick)
    {
        GameArt art = GameArt.Instance;
        return art != null ? pick(art) : null;
    }

    public static Sprite Drop()
    {
        Sprite custom = Custom(a => a.FountainDropSprite);

        if (custom != null)
        {
            return custom;
        }

        return Build("drop", 32, 32, 32f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            l.Draw(Circle(x, y, 16f, 16f, 15f), Color.white);
        });
    }

    // Pierścień na powierzchni czekolady. Szerokość 1, wysokość 0,5 jednostki.
    public static Sprite Ripple()
    {
        Sprite custom = Custom(a => a.FountainRippleSprite);

        if (custom != null)
        {
            return custom;
        }

        return Build("ripple", 64, 32, 64f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            l.Draw(Mathf.Abs(Ellipse(x, y, 32f, 16f, 28f, 12f)) - 2f, Color.white);
        });
    }

    public static Sprite Shadow()
    {
        Sprite custom = Custom(a => a.PickupShadowSprite);

        if (custom != null)
        {
            return custom;
        }

        return Build("shadow", 64, 32, 64f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            l.Draw(Ellipse(x, y, 32f, 16f, 28f, 12f), new Color(0f, 0f, 0f, 0.28f));
        });
    }

    // Bąbelek o średnicy około 0,92 jednostki.
    public static Sprite Bubble()
    {
        Sprite custom = Custom(a => a.PickupBubbleSprite);

        if (custom != null)
        {
            return custom;
        }

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
        Sprite custom = Custom(a => a.IconFor(icon));

        if (custom != null)
        {
            return custom;
        }

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
            case PickupIcon.Shield:
                return Build("icon_shield", 64, 64, ppu, center, DrawShield);
            case PickupIcon.Star:
                return Build("icon_star", 64, 64, ppu, center, DrawStar);
            case PickupIcon.Boot:
                return Build("icon_boot", 64, 64, ppu, center, DrawBoot);
            case PickupIcon.Bolt:
                return Build("icon_bolt", 64, 64, ppu, center, DrawBolt);
            case PickupIcon.Clock:
                return Build("icon_clock", 64, 64, ppu, center, DrawClock);
            case PickupIcon.Swirl:
                return Build("icon_swirl", 64, 64, ppu, center, DrawSwirl);
            case PickupIcon.Dizzy:
                return Build("icon_dizzy", 64, 64, ppu, center, DrawDizzy);
            case PickupIcon.MegaBomb:
                return Build("icon_megabomb", 64, 64, ppu, center, DrawMegaBomb);
            case PickupIcon.BombStorm:
                return Build("icon_bombstorm", 64, 64, ppu, center, DrawBombStorm);
            case PickupIcon.Nuke:
                return Build("icon_nuke", 64, 64, ppu, center, DrawNuke);
            default:
                return null;
        }
    }

    // Ikony zastępcze nowych przedmiotów (rysowane w kodzie, do czasu własnych grafik z GameArt).

    static void DrawShield(Layers l, float x, float y)
    {
        float body = Mathf.Min(Box(x, y, 32f, 38f, 15f, 13f), RotatedBox(x, y, 32f, 27f, 11f, 11f, 45f));
        l.Outlined(body, new Color(0.30f, 0.65f, 1f), Outline, 1.6f);
        l.Draw(Mathf.Min(Box(x, y, 32f, 35f, 2f, 10f), Box(x, y, 32f, 37f, 9f, 2f)), new Color(1f, 1f, 1f, 0.85f));
    }

    static void DrawStar(Layers l, float x, float y)
    {
        float star = Mathf.Min(
            Mathf.Min(RotatedBox(x, y, 32f, 32f, 5f, 19f, 0f), RotatedBox(x, y, 32f, 32f, 19f, 5f, 0f)),
            Mathf.Min(RotatedBox(x, y, 32f, 32f, 4f, 14f, 45f), RotatedBox(x, y, 32f, 32f, 14f, 4f, 45f)));
        l.Outlined(star, new Color(1f, 0.85f, 0.2f), Outline, 1.6f);
        l.Draw(Circle(x, y, 32f, 32f, 4f), new Color(1f, 1f, 0.8f));
    }

    static void DrawBoot(Layers l, float x, float y)
    {
        float boot = Mathf.Min(Box(x, y, 27f, 37f, 8f, 14f), Box(x, y, 34f, 21f, 15f, 7f));
        l.Outlined(boot, new Color(0.78f, 0.47f, 0.22f), Outline, 1.6f);
        l.Draw(Box(x, y, 34f, 15f, 15f, 2.2f), new Color(0.25f, 0.15f, 0.08f));
        l.Draw(Box(x, y, 27f, 47f, 8f, 2.5f), new Color(0.95f, 0.9f, 0.8f));
    }

    static void DrawBolt(Layers l, float x, float y)
    {
        float bolt = Mathf.Min(Capsule(x, y, 38f, 53f, 25f, 33f, 4.2f),
            Mathf.Min(Capsule(x, y, 25f, 33f, 39f, 31f, 4.2f), Capsule(x, y, 39f, 31f, 26f, 11f, 4.2f)));
        l.Outlined(bolt, new Color(1f, 0.9f, 0.2f), Outline, 1.5f);
    }

    static void DrawClock(Layers l, float x, float y)
    {
        l.Outlined(Circle(x, y, 32f, 32f, 19f), new Color(0.98f, 0.97f, 0.92f), Outline, 2f);
        l.Draw(Mathf.Min(Capsule(x, y, 32f, 32f, 32f, 44f, 1.8f), Capsule(x, y, 32f, 32f, 41f, 27f, 1.8f)), new Color(0.2f, 0.12f, 0.1f));
        l.Draw(Circle(x, y, 32f, 32f, 2.5f), new Color(0.9f, 0.25f, 0.2f));
        l.Draw(Circle(x, y, 32f, 55f, 3f), new Color(0.9f, 0.25f, 0.2f));
    }

    static void DrawSwirl(Layers l, float x, float y)
    {
        float rings = Mathf.Min(Mathf.Abs(Circle(x, y, 32f, 32f, 18f)) - 3f, Mathf.Abs(Circle(x, y, 32f, 32f, 9f)) - 2.5f);
        l.Outlined(rings, new Color(0.7f, 0.4f, 1f), Outline, 1.3f);
        l.Draw(Circle(x, y, 32f, 32f, 3f), new Color(1f, 1f, 1f));
    }

    static void DrawDizzy(Layers l, float x, float y)
    {
        Color pink = new Color(1f, 0.4f, 0.7f);
        // dwie strzałki w przeciwne strony
        float top = Mathf.Min(Capsule(x, y, 44f, 42f, 22f, 42f, 3.2f), RotatedBox(x, y, 20f, 42f, 6f, 6f, 45f));
        float bottom = Mathf.Min(Capsule(x, y, 20f, 22f, 42f, 22f, 3.2f), RotatedBox(x, y, 44f, 22f, 6f, 6f, 45f));
        l.Outlined(Mathf.Min(top, bottom), pink, Outline, 1.4f);
    }

    static void DrawMegaBomb(Layers l, float x, float y)
    {
        l.Outlined(Capsule(x, y, 36f, 44f, 46f, 55f, 2.6f), new Color(0.65f, 0.45f, 0.20f), Outline, 1.2f);
        l.Outlined(Circle(x, y, 31f, 27f, 21f), new Color(0.16f, 0.16f, 0.20f), new Color(0.95f, 0.25f, 0.2f), 2.2f);
        l.Draw(Circle(x, y, 23f, 36f, 5.5f), new Color(0.6f, 0.6f, 0.75f, 0.9f));
        l.Outlined(Circle(x, y, 48f, 56f, 5.5f), new Color(1f, 0.8f, 0.15f), new Color(0.9f, 0.35f, 0.05f), 1.2f);
    }

    // Atomówka: pionowy pocisk ze statecznikami i żółtym znakiem radiacji.
    static void DrawNuke(Layers l, float x, float y)
    {
        float fins = Mathf.Min(RotatedBox(x, y, 21f, 14f, 3.5f, 8f, 22f), RotatedBox(x, y, 43f, 14f, 3.5f, 8f, -22f));
        l.Outlined(fins, new Color(0.25f, 0.30f, 0.24f), Outline, 1.3f);
        l.Outlined(Capsule(x, y, 32f, 17f, 32f, 45f, 9.5f), new Color(0.42f, 0.50f, 0.36f), Outline, 1.6f);
        l.Outlined(Circle(x, y, 32f, 36f, 6.5f), new Color(1f, 0.85f, 0.1f), Outline, 1.2f);
        l.Draw(Circle(x, y, 32f, 36f, 2.2f), new Color(0.15f, 0.15f, 0.15f));
        l.Draw(Box(x, y, 32f, 24f, 9f, 2f), new Color(0.15f, 0.15f, 0.15f));
    }

    static void DrawBombStorm(Layers l, float x, float y)
    {
        Color dark = new Color(0.16f, 0.16f, 0.20f);
        l.Outlined(Circle(x, y, 20f, 22f, 11f), dark, Outline, 1.4f);
        l.Outlined(Circle(x, y, 44f, 22f, 11f), dark, Outline, 1.4f);
        l.Outlined(Circle(x, y, 32f, 41f, 11f), dark, Outline, 1.4f);
        Color spark = new Color(1f, 0.8f, 0.15f);
        l.Draw(Circle(x, y, 25f, 33f, 2.5f), spark);
        l.Draw(Circle(x, y, 49f, 33f, 2.5f), spark);
        l.Draw(Circle(x, y, 37f, 52f, 2.5f), spark);
    }

    // Fontanna: basen, kolumna, miska i dysza. Środek basenu leży w punkcie obrotu.
    // Szerokość 1,25 jednostki, dysza około 0,8 jednostki nad środkiem.
    public static Sprite FountainBody()
    {
        Sprite custom = Custom(a => a.FountainBodySprite);

        if (custom != null)
        {
            return custom;
        }

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
        Sprite custom = Custom(a => a.FountainStreamSprite);

        if (custom != null)
        {
            return custom;
        }

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
        Sprite custom = Custom(a => a.uiPanel);

        if (custom != null)
        {
            return custom;
        }

        return Build("ui_panel", 64, 64, 100f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            l.Draw(RoundBox(x, y, 32f, 32f, 31f, 31f, 18f), Color.white);
        }, new Vector4(22f, 22f, 22f, 22f));
    }

    // Złota korona dla lidera punktów. 64 x 48 pikseli.
    public static Sprite Crown()
    {
        Sprite custom = Custom(a => a.uiCrown);

        if (custom != null)
        {
            return custom;
        }

        return Build("ui_crown", 64, 48, 64f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            Color gold = new Color(1f, 0.82f, 0.2f);
            Color ink = new Color(0.45f, 0.28f, 0.05f);

            // trzy zęby (obrócone kwadraty) i podstawa
            float spikes = Mathf.Min(RotatedBox(x, y, 10f, 28f, 8f, 8f, 45f),
                Mathf.Min(RotatedBox(x, y, 32f, 32f, 9f, 9f, 45f), RotatedBox(x, y, 54f, 28f, 8f, 8f, 45f)));

            l.Outlined(Mathf.Min(spikes, RoundBox(x, y, 32f, 14f, 27f, 9f, 3f)), gold, ink, 2f);
            l.Draw(Circle(x, y, 10f, 38f, 3f), new Color(0.9f, 0.2f, 0.25f));
            l.Draw(Circle(x, y, 32f, 43f, 3.5f), new Color(0.3f, 0.6f, 1f));
            l.Draw(Circle(x, y, 54f, 38f, 3f), new Color(0.9f, 0.2f, 0.25f));
            l.Draw(Box(x, y, 32f, 14f, 24f, 1.5f), new Color(1f, 0.95f, 0.6f, 0.7f));
        });
    }

    // Klawisz klawiatury: jasna nakładka na ciemniejszym spodzie.
    public static Sprite KeyCap()
    {
        Sprite custom = Custom(a => a.uiKeyCap);

        if (custom != null)
        {
            return custom;
        }

        return Build("ui_key", 64, 64, 100f, new Vector2(0.5f, 0.5f), (l, x, y) =>
        {
            l.Draw(RoundBox(x, y, 32f, 30f, 31f, 29f, 12f), new Color(0.45f, 0.48f, 0.56f));
            l.Draw(RoundBox(x, y, 32f, 35f, 28f, 26f, 10f), new Color(0.98f, 0.98f, 1f));
        }, new Vector4(16f, 16f, 16f, 18f));
    }

    // Buźka gracza (biała, do podmalowania kolorem gracza). Martwa ma oczy X.
    public static Sprite Face(bool dead)
    {
        Sprite custom = Custom(a => dead ? a.uiFaceDead : a.uiFace);

        if (custom != null)
        {
            return custom;
        }

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

    // ---------- Emotki ----------

    static readonly Color EmoteInk = new Color(0.18f, 0.10f, 0.08f);

    // Emotka 64 x 64 px. Rysunek zastępczy do czasu własnych grafik (EmoteSet).
    public static Sprite EmoteIcon(Emote emote)
    {
        Sprite custom = Custom(a => a.EmoteFor(emote));

        if (custom != null)
        {
            return custom;
        }

        Vector2 center = new Vector2(0.5f, 0.5f);

        switch (emote)
        {
            case Emote.Angry:
                return Build("emote_angry", 64, 64, 100f, center, DrawEmoteAngry);
            case Emote.Laugh:
                return Build("emote_laugh", 64, 64, 100f, center, DrawEmoteLaugh);
            case Emote.Cry:
                return Build("emote_cry", 64, 64, 100f, center, DrawEmoteCry);
            default:
                return Build("emote_like", 64, 64, 100f, center, DrawEmoteLike);
        }
    }

    static void DrawEmoteAngry(Layers l, float x, float y)
    {
        l.Outlined(Circle(x, y, 32f, 32f, 28f), new Color(1f, 0.42f, 0.28f), EmoteInk, 2.5f);
        l.Draw(Ellipse(x, y, 23f, 33f, 3.6f, 4.6f), EmoteInk);
        l.Draw(Ellipse(x, y, 41f, 33f, 3.6f, 4.6f), EmoteInk);
        // brwi zbiegają się ku środkowi
        l.Draw(Mathf.Min(Capsule(x, y, 15f, 44f, 28f, 38f, 2.4f), Capsule(x, y, 36f, 38f, 49f, 44f, 2.4f)), EmoteInk);
        // usta: łuk wygięty w dół
        l.Draw(Mathf.Max(Mathf.Abs(Circle(x, y, 32f, 0f, 15f)) - 2.2f, 10f - y), EmoteInk);
    }

    static void DrawEmoteLaugh(Layers l, float x, float y)
    {
        l.Outlined(Circle(x, y, 32f, 32f, 28f), new Color(1f, 0.85f, 0.25f), EmoteInk, 2.5f);
        // zmrużone oczy ^ ^
        l.Draw(Mathf.Max(Mathf.Abs(Circle(x, y, 23f, 31f, 6f)) - 2f, 31f - y), EmoteInk);
        l.Draw(Mathf.Max(Mathf.Abs(Circle(x, y, 41f, 31f, 6f)) - 2f, 31f - y), EmoteInk);
        // szeroko otwarta buzia z językiem
        l.Outlined(Mathf.Max(Circle(x, y, 32f, 25f, 14f), y - 25f), new Color(0.55f, 0.10f, 0.15f), EmoteInk, 1.5f);
        l.Draw(Mathf.Max(Mathf.Max(Circle(x, y, 32f, 13f, 7f), Circle(x, y, 32f, 25f, 12.5f)), y - 25f), new Color(1f, 0.45f, 0.55f));
    }

    static void DrawEmoteCry(Layers l, float x, float y)
    {
        l.Outlined(Circle(x, y, 32f, 32f, 28f), new Color(1f, 0.85f, 0.25f), EmoteInk, 2.5f);
        // oczy zamknięte, łuk w dół
        l.Draw(Mathf.Max(Mathf.Abs(Circle(x, y, 23f, 36f, 5.5f)) - 2f, y - 36f), EmoteInk);
        l.Draw(Mathf.Max(Mathf.Abs(Circle(x, y, 41f, 36f, 5.5f)) - 2f, y - 36f), EmoteInk);
        // łzy
        Color tear = new Color(0.35f, 0.70f, 1f);
        l.Outlined(Capsule(x, y, 20f, 30f, 19f, 14f, 3.2f), tear, EmoteInk, 1f);
        l.Outlined(Capsule(x, y, 44f, 30f, 45f, 14f, 3.2f), tear, EmoteInk, 1f);
        // smutne usta
        l.Draw(Mathf.Max(Mathf.Abs(Circle(x, y, 32f, 0f, 13f)) - 2.2f, 8f - y), EmoteInk);
    }

    static void DrawEmoteLike(Layers l, float x, float y)
    {
        l.Outlined(Circle(x, y, 32f, 32f, 28f), new Color(0.33f, 0.55f, 1f), EmoteInk, 2.5f);
        Color skin = new Color(1f, 0.86f, 0.40f);
        // rękaw, pięść i kciuk w górę
        l.Outlined(Box(x, y, 19f, 27f, 4f, 11f), Color.white, EmoteInk, 1.5f);
        l.Outlined(RoundBox(x, y, 35f, 26f, 12f, 10f, 4f), skin, EmoteInk, 1.5f);
        l.Outlined(RoundBox(x, y, 31f, 41f, 4.5f, 10f, 3f), skin, EmoteInk, 1.5f);
        l.Draw(Box(x, y, 38f, 29f, 7f, 0.9f), EmoteInk);
        l.Draw(Box(x, y, 38f, 24f, 7f, 0.9f), EmoteInk);
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

        int textureWidth = width * Scale;
        int textureHeight = height * Scale;

        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.DontSave;

        Color[] pixels = new Color[textureWidth * textureHeight];
        Layers layers = new Layers();
        layers.scale = Scale;

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                layers.Reset();
                draw(layers, (x + 0.5f) / Scale, (y + 0.5f) / Scale);
                pixels[y * textureWidth + x] = layers.color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        // więcej pikseli na jednostkę, żeby rozmiar w grze i w HUD został taki sam
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, textureWidth, textureHeight), pivot, pixelsPerUnit * Scale, 0,
            SpriteMeshType.FullRect, border * Scale);
        sprite.hideFlags = HideFlags.DontSave;
        sprite.name = key;

        cache[key] = sprite;
        return sprite;
    }
}
