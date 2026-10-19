using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Efekt przycisku: po najechaniu (myszka) jaśniejsza nakładka, po kliknięciu ciemniejsza nakładka i lekkie zmniejszenie.
// Jeśli grafik wpisał w GameArt osobne grafiki przycisku na najechanie i kliknięcie, zamiast nakładki zmieniają się grafiki.
// Na telefonie najechania nie ma, więc widać tylko efekt kliknięcia.
// Pola są zapisywane w scenie, więc efekt działa też na przyciskach zbudowanych w Hierarchii.
public class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.26f);
    static readonly Color PressedColor = new Color(0f, 0f, 0f, 0.34f);

    [SerializeField] Button button;
    [SerializeField] Image target;
    [SerializeField] Image overlay;
    [SerializeField] Sprite normalSprite;
    [SerializeField] Sprite hoverSprite;
    [SerializeField] Sprite pressedSprite;

    // pierwszy kolor po kliknięciu trzyma się chwilę, żeby szybkie kliknięcie było widać
    const float PressedHoldSeconds = 0.14f;

    Canvas canvas;
    bool hover;
    bool polledHover;
    bool down;
    float pressedUntil;
    int lastState = -1;

    public void Init(Button owner, Image image, Image overlayImage)
    {
        button = owner;
        target = image;
        overlay = overlayImage;
        normalSprite = image.sprite;
        Apply();
    }

    // Własne grafiki przycisku (null = bez zmiany tego stanu).
    public void SetSprites(Sprite normal, Sprite hoverState, Sprite pressedState)
    {
        if (normal != null)
        {
            normalSprite = normal;
            target.sprite = normal;
        }

        hoverSprite = hoverState;
        pressedSprite = pressedState;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hover = true;
        Apply();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hover = false;
        down = false;
        Apply();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        down = true;
        pressedUntil = Time.unscaledTime + PressedHoldSeconds;
        Apply();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        down = false;
        Apply();
    }

    // Myszka jest czytana też wprost, bo w Simulatorze (mysz udaje dotyk) EventSystem nie zgłasza najechania.
    // Stan odświeżamy co klatkę, a kolor po kliknięciu zostaje na ułamek sekundy.
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;

        if (mouse != null)
        {
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            bool over = RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, mouse.position.ReadValue(), eventCamera);

            if (over != polledHover)
            {
                polledHover = over;
            }
        }
#endif

        int state = StateNow();

        if (state != lastState)
        {
            Apply();
        }
    }

    // 0 = zwykły, 1 = najechany, 2 = wciśnięty
    int StateNow()
    {
        bool active = button == null || button.interactable;

        if (!active)
        {
            return 0;
        }

        if (down || Time.unscaledTime < pressedUntil)
        {
            return 2;
        }

        return hover || polledHover ? 1 : 0;
    }

    void OnDisable()
    {
        hover = false;
        polledHover = false;
        down = false;
        pressedUntil = 0f;

        if (target != null && overlay != null)
        {
            Apply();
        }
    }

    void Apply()
    {
        if (target == null || overlay == null)
        {
            return;
        }

        int state = StateNow();
        lastState = state;
        bool isHover = state == 1;
        bool isDown = state == 2;

        bool spriteState = false;

        if (isDown && pressedSprite != null)
        {
            target.sprite = pressedSprite;
            spriteState = true;
        }
        else if (isHover && hoverSprite != null)
        {
            target.sprite = hoverSprite;
            spriteState = true;
        }
        else if (normalSprite != null)
        {
            target.sprite = normalSprite;
        }

        // nakładka ma kształt przycisku, więc pokrywa tylko jego powierzchnię
        overlay.sprite = target.sprite;
        overlay.type = target.type;
        overlay.color = spriteState ? Color.clear : isDown ? PressedColor : isHover ? HoverColor : Color.clear;

        transform.localScale = isDown ? new Vector3(0.96f, 0.96f, 1f) : Vector3.one;
    }
}
