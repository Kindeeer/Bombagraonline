using UnityEngine;
using UnityEngine.EventSystems;

// Przycisk akcji (bomba lub przedmiot specjalny) na panelu w HUD. Dotyk zgłasza akcję do TouchInput,
// a lokalny gracz odbiera ją tak samo jak wciśnięcie Spacji.
public class ActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Podpowiedź klawisza (np. SPACE). Na telefonie jest chowana.")]
    public GameObject keyHint;

    [Range(0.7f, 1f)] public float pressedScale = 0.92f;

    void Awake()
    {
        if (keyHint != null && Application.isMobilePlatform)
        {
            keyHint.SetActive(false);
        }
    }

    void OnDisable()
    {
        transform.localScale = Vector3.one;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TouchInput.QueueBomb();
        transform.localScale = Vector3.one * pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        transform.localScale = Vector3.one;
    }
}
