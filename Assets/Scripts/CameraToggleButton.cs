using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Przycisk CAM w HUD: przełącza widok kamery między średnim a bliskim. Kolor przycisku pokazuje aktualny widok.
// Cała plansza jest tylko na starcie rundy (READY), a do napisu GO! przycisk nie działa.
public class CameraToggleButton : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Tło przycisku, które zmienia kolor zależnie od widoku.")]
    public Image background;
    [Tooltip("Widok średni (domyślny).")]
    public Color offColor = new Color(0.78f, 0.86f, 1f);
    [Tooltip("Widok bliski.")]
    public Color onColor = new Color(1f, 0.86f, 0.25f);
    [Tooltip("Widok całej planszy.")]
    public Color wholeColor = new Color(0.65f, 0.95f, 0.7f);

    ArenaCamera arenaCamera;

    void Start()
    {
        arenaCamera = FindAnyObjectByType<ArenaCamera>();
        Refresh();
    }

    void Update()
    {
        // widok może zmienić się też z kodu (start rundy), więc kolor odświeżamy na bieżąco
        Refresh();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (MatchManager.InputLocked)
        {
            return;
        }

        if (arenaCamera == null)
        {
            arenaCamera = FindAnyObjectByType<ArenaCamera>();
        }

        if (arenaCamera == null)
        {
            return;
        }

        arenaCamera.CycleMode();
        Refresh();
    }

    void Refresh()
    {
        if (background == null || arenaCamera == null)
        {
            return;
        }

        switch (arenaCamera.Mode)
        {
            case ArenaCamera.ViewMode.Whole:
                background.color = wholeColor;
                break;
            case ArenaCamera.ViewMode.Close:
                background.color = onColor;
                break;
            default:
                background.color = offColor;
                break;
        }
    }
}
