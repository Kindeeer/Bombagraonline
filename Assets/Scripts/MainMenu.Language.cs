using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static UiKit;

// Wybór języka w menu: w prawym dolnym rogu jest flaga aktualnego języka. Po kliknięciu flagi rozsuwają się w lewo
// (po jednej na język) i wybierasz tę, której chcesz. Flagi są rysowane w kodzie.
// Przełącznik jest na stronach: menu główne, ustawienia i ustawienia w pauzie.
public partial class MainMenu
{
    const float FlagWidth = 150f;
    const float FlagHeight = 100f;
    const float FlagGap = 22f;

    class LanguagePicker
    {
        public RectTransform root;
        public readonly List<FlagButton> flags = new List<FlagButton>();
        public bool open;
        public Coroutine animation;
    }

    class FlagButton
    {
        public Language language;
        public RectTransform rect;
        public CanvasGroup group;
        public Image back;
        public float x;
        public float alpha;
    }

    readonly List<LanguagePicker> pickers = new List<LanguagePicker>();
    static Sprite[] flagSprites;

    void BuildLanguagePickers()
    {
        foreach (Page page in new[] { Page.Main, Page.Settings, Page.PauseSettings })
        {
            if (pages.TryGetValue(page, out RectTransform rect) && rect.Find("LanguagePicker") == null)
            {
                pickers.Add(BuildPicker(rect));
            }
        }

        Loc.Changed += OnLanguageChanged;
    }

    LanguagePicker BuildPicker(RectTransform page)
    {
        LanguagePicker picker = new LanguagePicker();

        picker.root = NewRect("LanguagePicker", page);
        picker.root.anchorMin = picker.root.anchorMax = new Vector2(1f, 0f);
        picker.root.pivot = new Vector2(1f, 0f);
        picker.root.anchoredPosition = new Vector2(-50f, 42f);
        picker.root.sizeDelta = new Vector2(FlagWidth, FlagHeight);

        foreach (Language language in Enum.GetValues(typeof(Language)))
        {
            FlagButton flag = new FlagButton { language = language };
            Language chosen = language;
            LanguagePicker owner = picker;

            Button button = NewButton(picker.root, "", Color.white, null, 20f);
            button.name = "Flag_" + Loc.CodeOf(language);
            WireClick(button, () => OnFlagClicked(owner, chosen), true);

            flag.rect = (RectTransform)button.transform;
            flag.rect.anchorMin = flag.rect.anchorMax = new Vector2(1f, 0f);
            flag.rect.pivot = new Vector2(1f, 0f);
            flag.rect.sizeDelta = new Vector2(FlagWidth, FlagHeight);
            flag.back = button.GetComponent<Image>();
            flag.group = button.gameObject.AddComponent<CanvasGroup>();

            Image image = NewImage("Flag", button.transform, Color.white, false);
            image.sprite = FlagSprite(language);
            image.preserveAspect = true;
            image.raycastTarget = false;
            Stretch(image.rectTransform, 9f);

            picker.flags.Add(flag);
        }

        ArrangePicker(picker, true);
        return picker;
    }

    void OnFlagClicked(LanguagePicker picker, Language language)
    {
        if (!picker.open)
        {
            picker.open = true;
            ArrangePicker(picker, false);
            return;
        }

        picker.open = false;

        if (language != Loc.Current)
        {
            Loc.Current = language;
        }
        else
        {
            ArrangePicker(picker, false);
        }
    }

    // Aktualny język stoi z prawej, pozostałe rozsuwają się w lewo (po otwarciu) albo chowają się za nim (po zamknięciu).
    void ArrangePicker(LanguagePicker picker, bool instant)
    {
        int rank = 1;

        foreach (FlagButton flag in picker.flags)
        {
            bool current = flag.language == Loc.Current;
            int slot = current ? 0 : rank++;

            flag.x = picker.open ? -slot * (FlagWidth + FlagGap) : 0f;
            flag.alpha = picker.open || current ? 1f : 0f;
            flag.back.color = current ? MenuBuilder.OrangeColor : MenuBuilder.CardColor;
            flag.group.blocksRaycasts = flag.alpha > 0.5f;
            flag.group.interactable = flag.alpha > 0.5f;
            flag.rect.SetSiblingIndex(current ? picker.flags.Count - 1 : 0);
        }

        if (instant || !isActiveAndEnabled)
        {
            foreach (FlagButton flag in picker.flags)
            {
                flag.rect.anchoredPosition = new Vector2(flag.x, 0f);
                flag.group.alpha = flag.alpha;
            }

            return;
        }

        if (picker.animation != null)
        {
            StopCoroutine(picker.animation);
        }

        picker.animation = StartCoroutine(SlideFlags(picker));
    }

    IEnumerator SlideFlags(LanguagePicker picker)
    {
        float elapsed = 0f;
        const float duration = 0.22f;

        List<Vector2> fromPosition = new List<Vector2>();
        List<float> fromAlpha = new List<float>();

        foreach (FlagButton flag in picker.flags)
        {
            fromPosition.Add(flag.rect.anchoredPosition);
            fromAlpha.Add(flag.group.alpha);
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / duration));

            for (int i = 0; i < picker.flags.Count; i++)
            {
                FlagButton flag = picker.flags[i];
                flag.rect.anchoredPosition = new Vector2(Mathf.Lerp(fromPosition[i].x, flag.x, t), 0f);
                flag.group.alpha = Mathf.Lerp(fromAlpha[i], flag.alpha, t);
            }

            yield return null;
        }

        foreach (FlagButton flag in picker.flags)
        {
            flag.rect.anchoredPosition = new Vector2(flag.x, 0f);
            flag.group.alpha = flag.alpha;
        }

        picker.animation = null;
    }

    void CollapsePickers()
    {
        foreach (LanguagePicker picker in pickers)
        {
            if (picker.open)
            {
                picker.open = false;
                ArrangePicker(picker, true);
            }
        }
    }

    // Po zmianie języka odświeżamy napisy budowane z kodu na stronie, którą akurat widać.
    void OnLanguageChanged()
    {
        foreach (LanguagePicker picker in pickers)
        {
            if (picker.root != null)
            {
                ArrangePicker(picker, false);
            }
        }

        if (!IsOpen)
        {
            return;
        }

        switch (current)
        {
            case Page.Skins:
                RebuildSkins();
                break;
            case Page.Shop:
                RefreshShop();
                break;
            case Page.Edit:
                RefreshEdit();
                break;
            case Page.Multi:
            case Page.Join:
                RefreshMultiplayer();
                break;
            case Page.Main:
                RefreshProfileBadge();
                break;
            case Page.Profile:
                RefreshProfile();
                break;
            case Page.Icons:
                RebuildIcons();
                break;
            case Page.Missions:
                RefreshMissions();
                break;
        }
    }

    // ---------- Flagi rysowane w kodzie ----------

    static Sprite FlagSprite(Language language)
    {
        if (flagSprites == null)
        {
            flagSprites = new Sprite[Enum.GetValues(typeof(Language)).Length];
        }

        int index = (int)language;

        if (flagSprites[index] != null)
        {
            return flagSprites[index];
        }

        const int width = 150;
        const int height = 100;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;

                // 2 x 2 próbki na piksel wygładzają krawędzie
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float u = (x + (sx + 0.5f) / 2f) / width;
                        float v = (y + (sy + 0.5f) / 2f) / height;

                        if (!InsideRoundedFlag(u, v))
                        {
                            continue;
                        }

                        Color c = FlagColor(language, u, v);
                        r += c.r;
                        g += c.g;
                        b += c.b;
                        a += 1f;
                    }
                }

                if (a > 0f)
                {
                    pixels[y * width + x] = new Color32((byte)(r / a * 255f), (byte)(g / a * 255f), (byte)(b / a * 255f), (byte)(a / 4f * 255f));
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        flagSprites[index] = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        flagSprites[index].hideFlags = HideFlags.HideAndDontSave;
        return flagSprites[index];
    }

    // Zaokrąglone rogi flagi (promień 10% wysokości, współrzędne 0..1 w poziomie i pionie).
    static bool InsideRoundedFlag(float u, float v)
    {
        const float aspect = 1.5f;
        const float radius = 0.12f;

        float x = u * aspect;
        float y = v;
        float cx = Mathf.Clamp(x, radius, aspect - radius);
        float cy = Mathf.Clamp(y, radius, 1f - radius);
        float dx = x - cx;
        float dy = y - cy;
        return dx * dx + dy * dy <= radius * radius;
    }

    static Color FlagColor(Language language, float u, float v)
    {
        if (language == Language.Polish)
        {
            return v >= 0.5f ? new Color(0.97f, 0.97f, 0.97f) : new Color(0.86f, 0.10f, 0.23f);
        }

        // Wielka Brytania (Union Jack) na polu 3:2
        const float aspect = 1.5f;
        Vector2 p = new Vector2(u * aspect - aspect * 0.5f, v - 0.5f);

        Color blue = new Color(0.04f, 0.14f, 0.45f);
        Color white = Color.white;
        Color red = new Color(0.80f, 0.08f, 0.17f);

        float cross = Mathf.Min(Mathf.Abs(p.x), Mathf.Abs(p.y));

        if (cross < 0.095f)
        {
            return red;
        }

        if (cross < 0.17f)
        {
            return white;
        }

        // przekątne od rogu do rogu
        Vector2 d1 = new Vector2(aspect, 1f).normalized;
        Vector2 d2 = new Vector2(aspect, -1f).normalized;
        float line1 = Mathf.Abs(p.x * d1.y - p.y * d1.x);
        float line2 = Mathf.Abs(p.x * d2.y - p.y * d2.x);
        float diagonal = Mathf.Min(line1, line2);

        if (diagonal < 0.04f)
        {
            return red;
        }

        if (diagonal < 0.115f)
        {
            return white;
        }

        return blue;
    }
}
