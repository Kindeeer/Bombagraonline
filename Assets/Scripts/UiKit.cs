using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Wspólne klocki do budowania interfejsu z kodu (używają ich menu główne, menu debug i HUD):
// prostokąty z zaokrąglonymi rogami, przyciski z efektem najechania, napisy TextMeshPro.
public static class UiKit
{
    static Sprite roundedSprite;
    static Sprite circleSprite;
    static Sprite gearSprite;

    // Sprite'y zapisane jako pliki (ustawia je edytor przy budowaniu menu w Hierarchii, bo obiekty w scenie nie mogą
    // wskazywać sprite'ów tworzonych w pamięci). Zwykle null: wtedy sprite'y rysuje kod.
    static Sprite roundedOverride;
    static Sprite circleOverride;
    static Sprite gearOverride;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        roundedSprite = null;
        circleSprite = null;
        gearSprite = null;
        roundedOverride = null;
        circleOverride = null;
        gearOverride = null;
    }

    public static void SetSpriteOverrides(Sprite rounded, Sprite circle, Sprite gear)
    {
        roundedOverride = rounded;
        circleOverride = circle;
        gearOverride = gear;
    }

    public static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rect, float margin = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(margin, margin);
        rect.offsetMax = new Vector2(-margin, -margin);
    }

    // Ustawia prostokąt przez kotwice w ułamkach rodzica (0-1), bez przesunięć.
    public static void Anchor(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
    {
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static Image NewImage(string name, Transform parent, Color color, bool rounded = true)
    {
        Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;

        if (rounded)
        {
            image.sprite = RoundedSprite();
            image.type = Image.Type.Sliced;
        }

        return image;
    }

    public static TextMeshProUGUI NewText(Transform parent, string text, float size, Color color,
        TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
    {
        TextMeshProUGUI label = NewRect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.fontStyle = style;
        label.raycastTarget = false;
        return label;
    }

    // Przycisk: kolor jest w obrazku, a efekt najechania i kliknięcia robi ButtonFx (jaśniejsza nakładka po najechaniu,
    // ciemniejsza i lekko zmniejszony przycisk po kliknięciu, albo własne grafiki z GameArt).
    // onClick = null tworzy sam wygląd (menu zbudowane w Hierarchii podpina akcje później przez WireClick).
    // clickSound = true odtwarza dźwięk kliknięcia (zdarzenie audio UiClick).
    public static Button NewButton(Transform parent, string text, Color color, Action onClick, float fontSize, bool clickSound = false)
    {
        Image image = NewImage("Button", parent, color);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.transition = Selectable.Transition.None;
        AddButtonFx(button, image);

        if (onClick != null)
        {
            WireClick(button, onClick, clickSound);
        }

        TextMeshProUGUI label = NewText(button.transform, text, fontSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(label.rectTransform, 6f);
        return button;
    }

    // Podpina akcję pod kliknięcie przycisku: dźwięk (opcjonalnie), akcja i zdjęcie zaznaczenia (zaznaczony przycisk
    // reagowałby potem na klawisze, np. Enter).
    public static void WireClick(Button button, Action onClick, bool clickSound = false)
    {
        button.onClick.AddListener(() =>
        {
            if (clickSound)
            {
                GameAudio.Play(AudioEvent.UiClick);
            }

            onClick?.Invoke();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        });
    }

    // Dokłada przyciskowi efekt najechania i kliknięcia. Wołać zaraz po utworzeniu przycisku, przed dodaniem jego zawartości
    // (nakładka musi leżeć pod napisem i ikonami).
    public static ButtonFx AddButtonFx(Button button, Image image)
    {
        // nakładka Fx może już istnieć (menu zbudowane wcześniej w Hierarchii), wtedy ją wykorzystujemy
        Transform existing = image.transform.Find("Fx");
        Image overlay = existing != null ? existing.GetComponent<Image>() : null;

        if (overlay == null)
        {
            overlay = NewImage("Fx", image.transform, Color.clear);
            Stretch(overlay.rectTransform);
            overlay.raycastTarget = false;
        }

        ButtonFx fx = image.gameObject.AddComponent<ButtonFx>();
        fx.Init(button, image, overlay);
        return fx;
    }

    // Napis na przycisku (pierwszy TextMeshPro w jego dzieciach).
    public static void SetButtonText(Button button, string text)
    {
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();

        if (label != null)
        {
            label.text = text;
        }
    }

    // ---------- Sprite'y rysowane w kodzie ----------

    // Prostokąt z zaokrąglonymi rogami (rozciągany jako 9-slice).
    public const int RoundedRadius = 16;

    public static Sprite RoundedSprite()
    {
        if (roundedOverride != null)
        {
            return roundedOverride;
        }

        if (roundedSprite != null)
        {
            return roundedSprite;
        }

        Texture2D texture = CreateRoundedTexture();
        roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(RoundedRadius, RoundedRadius, RoundedRadius, RoundedRadius));
        roundedSprite.hideFlags = HideFlags.HideAndDontSave;
        return roundedSprite;
    }

    public static Sprite CircleSprite()
    {
        if (circleOverride != null)
        {
            return circleOverride;
        }

        if (circleSprite != null)
        {
            return circleSprite;
        }

        Texture2D texture = CreateCircleTexture();
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        circleSprite.hideFlags = HideFlags.HideAndDontSave;
        return circleSprite;
    }

    // Zębatka (ikona ustawień), gdy grafik nie dał własnej (GameArt > uiGear).
    public static Sprite GearSprite()
    {
        if (gearOverride != null)
        {
            return gearOverride;
        }

        if (gearSprite != null)
        {
            return gearSprite;
        }

        Texture2D texture = CreateGearTexture();
        gearSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        gearSprite.hideFlags = HideFlags.HideAndDontSave;
        return gearSprite;
    }

    static Texture2D NewTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;
        return texture;
    }

    // Tekstury (też używane przez edytor, który zapisuje je jako pliki PNG).
    public static Texture2D CreateRoundedTexture()
    {
        const int size = 48;
        Texture2D texture = NewTexture(size);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 inner = new Vector2(Mathf.Clamp(point.x, RoundedRadius, size - RoundedRadius), Mathf.Clamp(point.y, RoundedRadius, size - RoundedRadius));
                float alpha = Mathf.Clamp01(RoundedRadius - Vector2.Distance(point, inner) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    public static Texture2D CreateCircleTexture()
    {
        const int size = 96;
        Texture2D texture = NewTexture(size);
        Color32[] pixels = new Color32[size * size];
        float radius = size * 0.5f - 1f;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = Mathf.Clamp01(radius - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    public static Texture2D CreateGearTexture()
    {
        const int size = 128;
        const int teeth = 8;
        float half = size * 0.5f;
        float bodyRadius = half * 0.62f;
        float toothRadius = half * 0.94f;
        float holeRadius = half * 0.26f;

        Texture2D texture = NewTexture(size);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 3 x 3 próbek na piksel wygładza krawędzie zębów
                int covered = 0;

                for (int sy = 0; sy < 3; sy++)
                {
                    for (int sx = 0; sx < 3; sx++)
                    {
                        float dx = x + (sx + 0.5f) / 3f - half;
                        float dy = y + (sy + 0.5f) / 3f - half;
                        float radius = Mathf.Sqrt(dx * dx + dy * dy);
                        float angle = Mathf.Atan2(dy, dx);
                        float outer = Mathf.Cos(teeth * angle) > -0.05f ? toothRadius : bodyRadius;

                        if (radius <= outer && radius >= holeRadius)
                        {
                            covered++;
                        }
                    }
                }

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(covered * 255 / 9));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    // Klikanie w interfejs wymaga EventSystem z modułem wejścia pasującym do ustawień projektu (nowy Input System).
    public static void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current != null ? EventSystem.current : UnityEngine.Object.FindAnyObjectByType<EventSystem>();

        if (eventSystem == null)
        {
            eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        StandaloneInputModule old = eventSystem.GetComponent<StandaloneInputModule>();

        if (old != null)
        {
            UnityEngine.Object.Destroy(old);
        }

        if (eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
#else
        if (eventSystem.GetComponent<BaseInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }
#endif
    }
}
