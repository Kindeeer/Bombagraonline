using UnityEngine;

// Bezpieczny obszar ekranu dla interfejsu i kamery: Screen.safeArea pomniejszony o wycięcia wyświetlacza
// (kamerka, notch, punch hole), które zachodzą na ten obszar. HUD i kamera nie wchodzą pod kamerkę telefonu.
public static class ScreenInsets
{
    // odstęp od wycięcia (piksele)
    const float CutoutMargin = 14f;

    static Rect cached;
    static int cachedWidth;
    static int cachedHeight;
    static Rect cachedSource;
    static bool hasCache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        hasCache = false;
    }

    public static Rect SafeArea
    {
        get
        {
            Rect source = Screen.safeArea;

            if (hasCache && cachedWidth == Screen.width && cachedHeight == Screen.height && cachedSource == source)
            {
                return cached;
            }

            Rect safe = source;

            foreach (Rect cutout in Screen.cutouts)
            {
                if (!cutout.Overlaps(safe))
                {
                    continue;
                }

                // wycięcie odcina ten brzeg, do którego jest bliżej (względem środka obszaru)
                float dx = (cutout.center.x - safe.center.x) / Mathf.Max(1f, safe.width);
                float dy = (cutout.center.y - safe.center.y) / Mathf.Max(1f, safe.height);

                if (Mathf.Abs(dx) >= Mathf.Abs(dy))
                {
                    if (dx > 0f)
                    {
                        safe.xMax = Mathf.Min(safe.xMax, cutout.xMin - CutoutMargin);
                    }
                    else
                    {
                        safe.xMin = Mathf.Max(safe.xMin, cutout.xMax + CutoutMargin);
                    }
                }
                else if (dy > 0f)
                {
                    safe.yMax = Mathf.Min(safe.yMax, cutout.yMin - CutoutMargin);
                }
                else
                {
                    safe.yMin = Mathf.Max(safe.yMin, cutout.yMax + CutoutMargin);
                }
            }

            cached = safe;
            cachedWidth = Screen.width;
            cachedHeight = Screen.height;
            cachedSource = source;
            hasCache = true;
            return safe;
        }
    }
}
