using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class ArenaCamera : MonoBehaviour
{
    [Header("Plansza")]
    public int arenaWidth = 17;
    public int arenaHeight = 17;
    public float marginCells = 0.4f;
    public float topOverhangCells = 0.75f;

    [Header("Miejsce na interfejs (ułamek szerokości bezpiecznego obszaru)")]
    [Range(0f, 0.45f)] public float leftPanel = 0.27f;
    [Range(0f, 0.45f)] public float rightPanel = 0.22f;

    [Header("Obszar ekranu")]
    public bool useSafeArea = true;

    Camera cam;
    int lastWidth;
    int lastHeight;
    Rect lastSafeArea;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
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

    void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafeArea)
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

    void Fit()
    {
        if (cam == null || Screen.width == 0 || Screen.height == 0)
        {
            return;
        }

        lastWidth = Screen.width;
        lastHeight = Screen.height;
        lastSafeArea = Screen.safeArea;

        Rect safe = useSafeArea ? Screen.safeArea : new Rect(0f, 0f, Screen.width, Screen.height);

        float availableLeft = safe.xMin + safe.width * leftPanel;
        float availableRight = safe.xMax - safe.width * rightPanel;
        float availableWidth = Mathf.Max(1f, availableRight - availableLeft);
        float availableHeight = Mathf.Max(1f, safe.height);

        float worldWidth = arenaWidth + marginCells * 2f;
        float worldHeight = arenaHeight + marginCells * 2f + topOverhangCells;

        float pixelsPerUnit = Mathf.Min(availableWidth / worldWidth, availableHeight / worldHeight);

        cam.orthographicSize = Screen.height / (2f * pixelsPerUnit);

        Vector2 boardCenter = new Vector2(arenaWidth * 0.5f, arenaHeight * 0.5f + topOverhangCells * 0.5f);
        Vector2 areaCenter = new Vector2((availableLeft + availableRight) * 0.5f, safe.center.y);
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 cameraCenter = boardCenter - (areaCenter - screenCenter) / pixelsPerUnit;

        transform.position = new Vector3(cameraCenter.x, cameraCenter.y, transform.position.z);
    }
}
