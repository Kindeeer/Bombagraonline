using UnityEngine;
using UnityEngine.UI;

// Wskaźniki innych graczy poza kadrem: na brzegu okna z planszą pojawia się buźka zwierzaka ze strzałką w jego stronę.
// Gdy gracz wejdzie w kadr, wskaźnik znika. Przy widoku całej planszy wszyscy są w kadrze, więc nic się nie pokazuje.
public class OffscreenIndicators : MonoBehaviour
{
    [Tooltip("Wielkość buźki wskaźnika (ułamek wysokości ekranu).")]
    public float sizeOfScreen = 0.075f;
    [Tooltip("Ile ponad połowę pola kadru gracz musi być poza kadrem, żeby wskaźnik się pokazał (w polach planszy).")]
    public float hideMarginCells = 0f;

    class Marker
    {
        public RectTransform root;
        public Image ring;
        public Image face;
        public RectTransform arrowPivot;
        public Image arrowShadow;
        public Image arrow;
    }

    readonly Marker[] markers = new Marker[MatchManager.MaxSlots];
    Camera arenaCamera;
    RectTransform canvasRect;
    Sprite arrowSprite;
    Sprite circleSprite;

    void Awake()
    {
        GameObject canvasObject = new GameObject("OffscreenIndicators", typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        canvasRect = canvasObject.GetComponent<RectTransform>();

        arrowSprite = BuildSprite(64, (x, y) => InsideArrow(x, y));
        circleSprite = BuildSprite(64, (x, y) => (x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f) <= 0.25f);

        for (int slot = 1; slot < markers.Length; slot++)
        {
            markers[slot] = BuildMarker(slot);
        }
    }

    Marker BuildMarker(int slot)
    {
        Marker marker = new Marker();

        marker.root = NewImage("Marker_" + slot, canvasRect, null, out Image rootImage);
        rootImage.enabled = false;
        marker.ring = NewImageIn(marker.root, "Ring", circleSprite);
        marker.face = NewImageIn(marker.root, "Face", null);
        marker.face.preserveAspect = true;

        marker.arrowPivot = new GameObject("Arrow", typeof(RectTransform)).GetComponent<RectTransform>();
        marker.arrowPivot.SetParent(marker.root, false);
        marker.arrowShadow = NewImageIn(marker.arrowPivot, "Shadow", arrowSprite);
        marker.arrowShadow.color = new Color(0f, 0f, 0f, 0.75f);
        marker.arrow = NewImageIn(marker.arrowPivot, "Fill", arrowSprite);

        marker.root.gameObject.SetActive(false);
        return marker;
    }

    static RectTransform NewImage(string name, Transform parent, Sprite sprite, out Image image)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    static Image NewImageIn(Transform parent, string name, Sprite sprite)
    {
        NewImage(name, parent, sprite, out Image image);
        return image;
    }

    void LateUpdate()
    {
        MatchManager match = MatchManager.Instance;

        if (arenaCamera == null)
        {
            ArenaCamera arena = FindAnyObjectByType<ArenaCamera>();
            arenaCamera = arena != null ? arena.GetComponent<Camera>() : null;
        }

        bool show = match != null && arenaCamera != null && !MainMenu.IsOpen;

        for (int slot = 1; slot < markers.Length; slot++)
        {
            Marker marker = markers[slot];

            // przy zamykaniu sceny canvas znika wcześniej niż ten komponent
            if (marker == null || marker.root == null)
            {
                continue;
            }

            Combatant combatant = match != null ? match.Slots[slot] : null;

            if (!show || combatant == null || !combatant.IsAlive)
            {
                marker.root.gameObject.SetActive(false);
                continue;
            }

            UpdateMarker(marker, slot, combatant, match);
        }
    }

    void UpdateMarker(Marker marker, int slot, Combatant combatant, MatchManager match)
    {
        Rect view = arenaCamera.pixelRect;
        Vector3 worldPosition = combatant.health.transform.position + Vector3.up * 0.4f;
        Vector3 screen = arenaCamera.WorldToScreenPoint(worldPosition);

        float cellPixels = view.height / (arenaCamera.orthographicSize * 2f);
        float margin = hideMarginCells * cellPixels;

        bool visible = screen.x >= view.xMin - margin && screen.x <= view.xMax + margin
            && screen.y >= view.yMin - margin && screen.y <= view.yMax + margin;

        if (visible)
        {
            marker.root.gameObject.SetActive(false);
            return;
        }

        float size = Screen.height * sizeOfScreen;
        float pad = size * 1.0f;

        Vector2 center = view.center;
        Vector2 direction = new Vector2(screen.x, screen.y) - center;

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector2.up;
        }

        float halfWidth = Mathf.Max(1f, view.width * 0.5f - pad);
        float halfHeight = Mathf.Max(1f, view.height * 0.5f - pad);
        float scale = Mathf.Min(halfWidth / Mathf.Max(0.01f, Mathf.Abs(direction.x)), halfHeight / Mathf.Max(0.01f, Mathf.Abs(direction.y)));
        Vector2 position = center + direction * scale;

        marker.root.gameObject.SetActive(true);
        marker.root.anchorMin = marker.root.anchorMax = Vector2.zero;
        marker.root.pivot = new Vector2(0.5f, 0.5f);
        marker.root.anchoredPosition = position;
        marker.root.sizeDelta = new Vector2(size, size);

        Color slotColor = slot < match.slotColors.Length ? match.slotColors[slot] : Color.white;
        GameArt art = GameArt.Instance;
        PlayerSprites sprites = art != null ? art.PlayerFor(slot) : null;
        Sprite portrait = sprites != null ? sprites.Portrait : null;

        marker.ring.color = slotColor;
        Stretch(marker.ring.rectTransform, 1f);
        marker.face.sprite = portrait != null ? portrait : PickupArt.Face(false);
        marker.face.color = portrait != null ? Color.white : slotColor;
        Stretch(marker.face.rectTransform, 0.8f);

        // strzałka za buźką, wskazuje kierunek do gracza
        Vector2 unit = direction.normalized;
        float arrowSize = size * 0.5f;

        marker.arrowPivot.anchoredPosition = unit * size * 0.72f;
        marker.arrowPivot.sizeDelta = new Vector2(arrowSize, arrowSize);
        marker.arrowPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(unit.y, unit.x) * Mathf.Rad2Deg - 90f);

        marker.arrow.color = slotColor;
        Stretch(marker.arrow.rectTransform, 0.8f);
        Stretch(marker.arrowShadow.rectTransform, 1f);
    }

    // Obrazek wypełnia rodzica, pomniejszony do podanego ułamka.
    static void Stretch(RectTransform rect, float fraction)
    {
        float inset = (1f - fraction) * 0.5f;
        rect.anchorMin = new Vector2(inset, inset);
        rect.anchorMax = new Vector2(1f - inset, 1f - inset);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // Grot strzałki skierowany w górę (współrzędne 0..1).
    static bool InsideArrow(float x, float y)
    {
        // wielokąt: czubek, prawy róg, wcięcie, lewy róg
        Vector2[] poly = { new Vector2(0.5f, 0.97f), new Vector2(0.94f, 0.08f), new Vector2(0.5f, 0.34f), new Vector2(0.06f, 0.08f) };
        bool inside = false;

        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > y) != (poly[j].y > y)
                && x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    // Biały sprite z kształtu (z wygładzonymi krawędziami), barwiony później kolorem obrazka.
    static Sprite BuildSprite(int size, System.Func<float, float, bool> shape)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];

        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                int hits = 0;

                for (int sy = 0; sy < 3; sy++)
                {
                    for (int sx = 0; sx < 3; sx++)
                    {
                        if (shape((px + (sx + 0.5f) / 3f) / size, (py + (sy + 0.5f) / 3f) / size))
                        {
                            hits++;
                        }
                    }
                }

                pixels[py * size + px] = new Color32(255, 255, 255, (byte)(hits * 255 / 9));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
