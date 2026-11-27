using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Dźwięk przewijanej listy (zasady w poczekalni, skiny, chromy, ikonki): krótki dźwięk, gdy palec zaczyna przesuwać listę
// (ScrollStart), i drugi, gdy ją puszcza (ScrollEnd). Bez własnych dźwięków oba grają zwykły klik przycisku (UiClick).
// Leży na tym samym obiekcie co ScrollRect: przeciąganie trafia do obu komponentów naraz, więc przewijanie działa jak dawniej.
[RequireComponent(typeof(ScrollRect))]
public class ScrollSounds : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    public static void Attach(ScrollRect scroll)
    {
        if (scroll != null && scroll.GetComponent<ScrollSounds>() == null)
        {
            scroll.gameObject.AddComponent<ScrollSounds>();
        }
    }

    // Dokłada dźwięk do każdej przewijanej listy pod podanym obiektem (także ukrytej).
    public static void AttachAll(Transform root)
    {
        if (root == null)
        {
            return;
        }

        foreach (ScrollRect scroll in root.GetComponentsInChildren<ScrollRect>(true))
        {
            Attach(scroll);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        GameAudio.Play(AudioEvent.ScrollStart);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        GameAudio.Play(AudioEvent.ScrollEnd);
    }
}
