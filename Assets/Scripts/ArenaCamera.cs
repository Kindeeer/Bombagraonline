using UnityEngine;

// Kamera areny z trzema widokami:
// - Whole: cała plansza (na starcie rundy, podczas READY),
// - Medium: umiarkowane przybliżenie, kamera podąża za graczem (widok domyślny podczas gry),
// - Close: mocne przybliżenie za graczem.
// Przejścia są płynne. Przycisk CAM w HUD przełącza między Medium a Close (CycleMode), a cała plansza (Whole)
// jest tylko na starcie rundy: ustawia ją MatchManager na czas READY.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class ArenaCamera : MonoBehaviour
{
    public enum ViewMode
    {
        Whole,
        Medium,
        Close
    }

    [Header("Plansza (rozmiar bierze się z GridMap)")]
    [Tooltip("Dekoracyjna obwódka poza siatką, w polach z każdej strony. 0 = widać tylko ramkę z siatki.")]
    public float decorationCells = 0f;
    public float marginCells = 0.4f;
    public float topOverhangCells = 0.75f;

    [Header("Miejsce na interfejs (ułamek szerokości bezpiecznego obszaru)")]
    [Range(0f, 0.45f)] public float leftPanel = 0.27f;
    [Range(0f, 0.45f)] public float rightPanel = 0.22f;

    [Header("Obszar ekranu")]
    public bool useSafeArea = true;

    [Header("Widoki (ile wierszy planszy widać na wysokość ekranu)")]
    [Tooltip("Widok domyślny podczas gry. Mniej wierszy = większe przybliżenie.")]
    [Min(3f)] public float mediumViewRows = 11f;
    [Tooltip("Mocne przybliżenie (przycisk CAM).")]
    [Min(3f)] public float closeViewRows = 7f;
    [Tooltip("Czas płynnego przejścia między widokami (sekundy). Przybliżanie i przesuwanie kamery do gracza są ze sobą zsynchronizowane.")]
    public float zoomSeconds = 0.9f;
    [Tooltip("Wygładzenie ruchu kamery za graczem (sekundy). 0 = sztywno przyklejona. Więcej = kamera bardziej się spóźnia, ale jedzie płynniej.")]
    public float followSeconds = 0.22f;
    [Tooltip("Widok na starcie sceny. MatchManager zmienia go po READY.")]
    [SerializeField] ViewMode startMode = ViewMode.Whole;

    public ViewMode Mode => mode;

    ViewMode mode;
    Camera cam;
    int lastWidth;
    int lastHeight;
    Rect lastSafeArea;

    // aktualna liczba widocznych wierszy (<= 0 = jeszcze nieustawiona) i udział podążania za graczem (0..1).
    // Udział podążania wynika z przybliżenia (im bliżej, tym kamera mocniej jedzie za graczem), więc zoom i ruch
    // zawsze idą w parze i nie ma szarpnięcia na początku przejścia.
    float rows = -1f;
    float rowsVelocity;
    float followBlend;
    Vector2 smoothTarget;
    Vector2 smoothVelocity;
    bool hasSmoothTarget;

    // Zmienia widok. instant = true ustawia go od razu, bez płynnego przejścia.
    public void SetMode(ViewMode newMode, bool instant = false)
    {
        mode = newMode;

        if (instant)
        {
            rows = -1f;
            rowsVelocity = 0f;
            followBlend = mode == ViewMode.Whole ? 0f : 1f;
        }
    }

    // Przełącza między widokiem średnim a bliskim (przycisk CAM). Cała plansza jest tylko na starcie rundy,
    // więc jeśli kamera akurat ją pokazuje, przejdzie do widoku średniego.
    public void CycleMode()
    {
        SetMode(mode == ViewMode.Medium ? ViewMode.Close : ViewMode.Medium);
    }

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        mode = startMode;
        rows = -1f;
        followBlend = mode == ViewMode.Whole ? 0f : 1f;
        Fit();
    }

    void OnValidate()
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }

        Fit();
    }

    void LateUpdate()
    {
        bool screenChanged = Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafeArea;

        if (Application.isPlaying)
        {
            GetMetrics(out _, out _, out _, out _, out _, out _, out float fitRows);

            float mediumRows = Mathf.Min(fitRows, mediumViewRows);
            float targetRows = mode == ViewMode.Whole ? fitRows : Mathf.Min(fitRows, mode == ViewMode.Medium ? mediumViewRows : closeViewRows);
            float smooth = Mathf.Max(0.01f, zoomSeconds);

            if (rows <= 0f)
            {
                rows = targetRows;
            }

            // zoom liczymy w skali logarytmicznej: przybliżenie "razy dwa" wygląda wtedy równo na całej drodze
            float logRows = Mathf.SmoothDamp(Mathf.Log(rows), Mathf.Log(targetRows), ref rowsVelocity, smooth, Mathf.Infinity, Time.unscaledDeltaTime);
            rows = Mathf.Exp(logRows);

            if (fitRows - mediumRows > 0.01f)
            {
                float progress = Mathf.InverseLerp(Mathf.Log(fitRows), Mathf.Log(mediumRows), logRows);
                followBlend = progress * progress * (3f - 2f * progress);
            }
            else
            {
                followBlend = mode == ViewMode.Whole ? 0f : 1f;
            }

            UpdateSmoothTarget();
            Fit();
            return;
        }

        if (screenChanged)
        {
            Fit();
        }
    }

    // Miejsce na panele interfejsu po bokach (ułamek szerokości bezpiecznego obszaru). Ustawia je GameHud.
    public void SetSidePanels(float left, float right)
    {
        leftPanel = left;
        rightPanel = right;
        Fit();
    }

    // Lokalny gracz to slot 0 w MatchManager (boty zajmują kolejne sloty).
    bool TryGetPlayerPosition(out Vector2 position)
    {
        MatchManager match = MatchManager.Instance;

        if (Application.isPlaying && match != null && match.Slots[0] != null && match.Slots[0].health != null)
        {
            position = match.Slots[0].health.transform.position;
            return true;
        }

        position = default;
        return false;
    }

    void UpdateSmoothTarget()
    {
        if (!TryGetPlayerPosition(out Vector2 raw))
        {
            hasSmoothTarget = false;
            return;
        }

        if (!hasSmoothTarget || followSeconds <= 0f)
        {
            smoothTarget = raw;
            smoothVelocity = Vector2.zero;
            hasSmoothTarget = true;
            return;
        }

        smoothTarget = Vector2.SmoothDamp(smoothTarget, raw, ref smoothVelocity, followSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    // Wymiary obszaru ekranu między panelami i widok całej planszy (fitRows = ile wierszy mieści się w widoku całej planszy).
    void GetMetrics(out Rect safe, out float availableLeft, out float availableRight, out float availableWidth,
        out float availableHeight, out float fitPixelsPerUnit, out float fitRows)
    {
        safe = useSafeArea ? Screen.safeArea : new Rect(0f, 0f, Screen.width, Screen.height);

        availableLeft = safe.xMin + safe.width * leftPanel;
        availableRight = safe.xMax - safe.width * rightPanel;
        availableWidth = Mathf.Max(1f, availableRight - availableLeft);
        availableHeight = Mathf.Max(1f, safe.height);

        float extra = decorationCells + marginCells;
        float worldWidth = GridMap.Width + extra * 2f;
        float worldHeight = GridMap.Height + extra * 2f + topOverhangCells;

        fitPixelsPerUnit = Mathf.Min(availableWidth / worldWidth, availableHeight / worldHeight);
        fitRows = availableHeight / fitPixelsPerUnit;
    }

    void Fit()
    {
        if (cam == null || Screen.width == 0 || Screen.height == 0)
        {
            return;
        }

        lastWidth = Screen.width;
        lastHeight = Screen.height;
        lastSafeArea = Screen.safeArea;

        GetMetrics(out Rect safe, out float availableLeft, out float availableRight, out float availableWidth,
            out float availableHeight, out float fitPixelsPerUnit, out float fitRows);

        // kamera nigdy nie oddala się bardziej niż do widoku całej planszy
        float rowsNow = Application.isPlaying && rows > 0f ? Mathf.Min(rows, fitRows) : fitRows;
        float pixelsPerUnit = availableHeight / Mathf.Max(1f, rowsNow);

        float extra = decorationCells + marginCells;
        Vector2 focus = new Vector2(GridMap.Width * 0.5f, GridMap.Height * 0.5f + topOverhangCells * 0.5f);

        // przejście w stronę widoku za graczem; kadr nie wychodzi poza planszę z marginesami
        if (followBlend > 0.0001f && hasSmoothTarget)
        {
            float viewWidth = availableWidth / pixelsPerUnit;
            float viewHeight = availableHeight / pixelsPerUnit;

            Vector2 followFocus = new Vector2(
                ClampAxis(smoothTarget.x, viewWidth, -extra, GridMap.Width + extra),
                ClampAxis(smoothTarget.y, viewHeight, -extra, GridMap.Height + extra + topOverhangCells));

            focus = Vector2.Lerp(focus, followFocus, followBlend);
        }

        cam.orthographicSize = Screen.height / (2f * pixelsPerUnit);

        Vector2 areaCenter = new Vector2((availableLeft + availableRight) * 0.5f, safe.center.y);
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 cameraCenter = focus - (areaCenter - screenCenter) / pixelsPerUnit;

        transform.position = new Vector3(cameraCenter.x, cameraCenter.y, transform.position.z);
    }

    // Jeśli kadr jest większy niż plansza, centrujemy; inaczej trzymamy go w granicach planszy.
    static float ClampAxis(float value, float viewSize, float min, float max)
    {
        if (viewSize >= max - min)
        {
            return (min + max) * 0.5f;
        }

        return Mathf.Clamp(value, min + viewSize * 0.5f, max - viewSize * 0.5f);
    }
}
