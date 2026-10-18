using UnityEngine;
using UnityEngine.EventSystems;

// Joystick ekranowy. Wisi na panelu sterowania w HUD: dotyk gdziekolwiek na panelu daje kierunek
// względem jego środka. Gra chodzi po osiach, więc z wychylenia wybieramy jedną oś (bez skosów).
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Tooltip("Gałka, która pokazuje, gdzie jest palec. Puste = joystick działa, ale gałki nie widać.")]
    public RectTransform knob;

    [Tooltip("Jak daleko od środka trzeba przesunąć palec, żeby postać ruszyła (ułamek promienia).")]
    [Range(0.05f, 0.9f)] public float deadZone = 0.25f;

    [Tooltip("Ile razy druga oś musi być większa, żeby zmienić kierunek w pionie na poziom (i odwrotnie). Zapobiega drganiu na skosie.")]
    [Min(1f)] public float axisSwitchBias = 1.3f;

    [Tooltip("Jak daleko od środka może odejść gałka (ułamek promienia panelu).")]
    [Range(0.2f, 1f)] public float knobTravel = 0.6f;

    RectTransform area;
    int activePointer = int.MinValue;
    Vector2Int current;

    void Awake()
    {
        area = (RectTransform)transform;
    }

    void OnDisable()
    {
        Release();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // drugi palec na tym samym panelu ignorujemy
        if (activePointer != int.MinValue)
        {
            return;
        }

        activePointer = eventData.pointerId;
        Apply(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointer)
        {
            Apply(eventData);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointer)
        {
            Release();
        }
    }

    void Apply(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            return;
        }

        // środek panelu to punkt zerowy, a promień to połowa krótszego boku
        Vector2 offset = local - area.rect.center;
        float radius = Mathf.Max(1f, Mathf.Min(area.rect.width, area.rect.height) * 0.5f);
        Vector2 stick = Vector2.ClampMagnitude(offset / radius, 1f);

        if (knob != null)
        {
            knob.anchoredPosition = stick * radius * knobTravel;
        }

        current = ChooseDirection(stick);
        TouchInput.Move = current;
    }

    Vector2Int ChooseDirection(Vector2 stick)
    {
        if (stick.magnitude < deadZone)
        {
            return Vector2Int.zero;
        }

        float absX = Mathf.Abs(stick.x);
        float absY = Mathf.Abs(stick.y);

        // przy zmianie osi wymagamy wyraźnej przewagi nowej osi
        bool horizontal;

        if (current.x != 0)
        {
            horizontal = absX * axisSwitchBias >= absY;
        }
        else if (current.y != 0)
        {
            horizontal = absX > absY * axisSwitchBias;
        }
        else
        {
            horizontal = absX >= absY;
        }

        return horizontal
            ? new Vector2Int(stick.x > 0f ? 1 : -1, 0)
            : new Vector2Int(0, stick.y > 0f ? 1 : -1);
    }

    void Release()
    {
        activePointer = int.MinValue;
        current = Vector2Int.zero;
        TouchInput.Move = Vector2Int.zero;

        if (knob != null)
        {
            knob.anchoredPosition = Vector2.zero;
        }
    }
}
