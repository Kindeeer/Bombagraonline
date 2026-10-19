using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Celowanie atomówką: gdy gracz podniesie atomówkę (albo wciśnie Spację z atomówką w slocie Buff), postać staje w miejscu,
// kamera pokazuje całą mapę, a na środku pojawia się czerwony kwadrat 7 x 7 obszaru wybuchu. Obszar podąża za myszką
// albo palcem. Kliknięcie w mapę tylko wskazuje miejsce. Atomówkę zrzuca przycisk akcji (na ekranie podpisany ATOM,
// na klawiaturze Spacja). Kliknięcie w panele HUD nie przesuwa obszaru. Po 12 sekundach bez wyboru celowanie się anuluje (atomówka zostaje).
public class NukeTargeting : MonoBehaviour
{
    const float TimeoutSeconds = 12f;

    static NukeTargeting active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        active = null;
    }

    public static bool IsActive => active != null;

    // Przycisk akcji (ATOM albo Spacja) zrzuca atomówkę w wybrane miejsce.
    public static void ConfirmActive()
    {
        if (active != null)
        {
            active.Drop();
        }
    }

    PlayerPickups owner;
    PlayerMovement movement;
    ArenaCamera arenaCamera;
    ArenaCamera.ViewMode previousMode;
    Vector2Int targetCell;
    Vector2 lastPointer;
    bool pointerMoved;
    Camera cam;
    SpriteRenderer marker;
    GameObject hint;
    float age;

    public static void Begin(PlayerPickups pickups, PlayerMovement playerMovement)
    {
        if (active != null)
        {
            return;
        }

        // zapomnij o przycisku akcji wciśniętym tuż przed podniesieniem, żeby nie zrzucić atomówki przypadkiem
        TouchInput.ConsumeBomb();

        GameObject go = new GameObject("NukeTargeting");
        active = go.AddComponent<NukeTargeting>();
        active.Setup(pickups, playerMovement);
    }

    void Setup(PlayerPickups pickups, PlayerMovement playerMovement)
    {
        owner = pickups;
        movement = playerMovement;
        movement.Frozen = true;

        // kamera pokazuje całą mapę, żeby było widać, gdzie można zrzucić atomówkę
        arenaCamera = FindAnyObjectByType<ArenaCamera>();
        cam = arenaCamera != null ? arenaCamera.GetComponent<Camera>() : Camera.main;

        if (arenaCamera != null)
        {
            previousMode = arenaCamera.Mode;
            arenaCamera.SetMode(ArenaCamera.ViewMode.Whole);
        }

        GameObject area = new GameObject("TargetArea");
        area.transform.SetParent(transform, false);
        area.transform.localScale = Vector3.one * (Nuke.BlastRadius * 2 + 1);
        marker = area.AddComponent<SpriteRenderer>();
        marker.sprite = Nuke.SquareSprite();
        marker.sortingOrder = 2;
        marker.color = new Color(1f, 0.15f, 0.1f, 0.3f);

        // czerwony obszar 7 x 7 stoi na początku na środku mapy (a więc ekranu), dopóki gracz nie ruszy myszką
        targetCell = new Vector2Int(GridMap.Width / 2, GridMap.Height / 2);
        marker.transform.position = GridMap.Instance.GridToWorld(targetCell);

        BuildHint();
    }

    // Napis na górze ekranu z instrukcją (własny Canvas, nie rusza HUD).
    void BuildHint()
    {
        GameObject canvasObject = new GameObject("NukeHint", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        TextMeshProUGUI label = UiKit.NewText(canvasObject.transform, "WSKAŻ MIEJSCE NA MAPIE I NACIŚNIJ ATOM!", 64f,
            new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.Center, FontStyles.Bold);
        label.outlineWidth = 0.3f;
        label.outlineColor = new Color32(60, 10, 10, 255);
        UiKit.Anchor(label.rectTransform, 0.2f, 0.9f, 0.8f, 0.99f);
        hint = canvasObject;
    }

    void Update()
    {
        // w pauzie czas celowania stoi
        if (MatchManager.InputLocked)
        {
            return;
        }

        age += Time.deltaTime;

        PlayerHealth health = owner != null ? owner.GetComponent<PlayerHealth>() : null;

        if (owner == null || health == null || health.IsDead || age > TimeoutSeconds || cam == null || GridMap.Instance == null)
        {
            End();
            return;
        }

        // lekkie pulsowanie obszaru
        marker.color = new Color(1f, 0.15f, 0.1f, 0.22f + 0.12f * Mathf.Sin(age * 8f));

        if (!TryReadPointer(out Vector2 screen, out bool held))
        {
            return;
        }

        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
        Vector2Int cell = GridMap.Instance.WorldToGrid(world);
        bool onMap = cell.x >= 1 && cell.y >= 1 && cell.x < GridMap.Width - 1 && cell.y < GridMap.Height - 1;
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // obszar idzie za myszką dopiero po jej poruszeniu (na początku stoi na środku)
        if (!pointerMoved && (screen - lastPointer).sqrMagnitude > 4f && lastPointer != Vector2.zero)
        {
            pointerMoved = true;
        }

        lastPointer = screen;

        // obszar podąża za myszką albo palcem (przeciągnięcie po mapie); kliknięcie w mapę nic nie zrzuca
        if (onMap && !overUi && (pointerMoved || held))
        {
            targetCell = cell;
            marker.transform.position = GridMap.Instance.GridToWorld(cell);
        }
    }

    // Zrzuca atomówkę na pole, na którym stoi czerwony obszar.
    void Drop()
    {
        // krótka ochrona przed przypadkowym zrzutem tuż po podniesieniu
        if (age < 0.35f || owner == null)
        {
            return;
        }

        owner.LaunchNuke(targetCell);
        End();
    }

    static bool TryReadPointer(out Vector2 position, out bool held)
    {
#if ENABLE_INPUT_SYSTEM
        Pointer pointer = Pointer.current;

        if (pointer == null)
        {
            position = default;
            held = false;
            return false;
        }

        position = pointer.position.ReadValue();
        held = pointer.press.isPressed;
        return true;
#else
        position = Input.mousePosition;
        held = Input.GetMouseButton(0);
        return true;
#endif
    }

    void End()
    {
        if (movement != null)
        {
            movement.Frozen = false;
        }

        // kamera wraca do widoku sprzed celowania
        if (arenaCamera != null)
        {
            arenaCamera.SetMode(previousMode == ArenaCamera.ViewMode.Whole ? ArenaCamera.ViewMode.Medium : previousMode);
            arenaCamera = null;
        }

        if (active == this)
        {
            active = null;
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (movement != null)
        {
            movement.Frozen = false;
        }

        if (arenaCamera != null)
        {
            arenaCamera.SetMode(previousMode == ArenaCamera.ViewMode.Whole ? ArenaCamera.ViewMode.Medium : previousMode);
        }

        if (active == this)
        {
            active = null;
        }
    }
}
