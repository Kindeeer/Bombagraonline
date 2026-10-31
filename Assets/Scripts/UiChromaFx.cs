using UnityEngine;
using UnityEngine.UI;

// Efekty chromy na obrazku postaci w interfejsie (podgląd w EDIT, karta po otwarciu skrzynki).
// Cząstki są obrazkami wewnątrz podanego prostokąta, który oznacza całą postać.
public class UiChromaFx : ChromaFxEmitter
{
    RectTransform target;
    RectTransform holder;

    protected override float Delta => Time.unscaledDeltaTime;
    protected override float RateScale => 0.7f;

    public void Configure(RectTransform bodyRect, ChromaFxLook look)
    {
        if (bodyRect != target)
        {
            Clear();

            if (holder != null)
            {
                Destroy(holder.gameObject);
                holder = null;
            }
        }

        target = bodyRect;
        SetLook(look);
    }

    protected override bool GetBody(out Vector2 feet, out float height, out float width)
    {
        feet = Vector2.zero;
        height = 1f;
        width = 1f;

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        Rect rect = target.rect;
        height = rect.height;
        width = rect.width;

        // układ cząstek: środek prostokąta to (0,0), stopy są na dole
        feet = new Vector2(rect.center.x, rect.yMin);
        return true;
    }

    protected override void CreateView(Particle particle, int spriteKind)
    {
        if (holder == null)
        {
            GameObject go = new GameObject("ChromaFx", typeof(RectTransform));
            holder = (RectTransform)go.transform;
            holder.SetParent(target, false);
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = Vector2.zero;
        }

        GameObject view = new GameObject("P", typeof(RectTransform), typeof(Image));
        view.transform.SetParent(holder, false);

        Image image = view.GetComponent<Image>();
        image.sprite = ChromaFx.SpriteOf(spriteKind);
        image.raycastTarget = false;
        particle.view = view;
    }

    protected override void ApplyView(Particle particle, float alpha, float scale, Color color)
    {
        if (particle.view == null)
        {
            return;
        }

        RectTransform rect = (RectTransform)particle.view.transform;
        rect.anchoredPosition = particle.position;
        rect.sizeDelta = Vector2.one * (particle.size * scale);
        rect.localRotation = Quaternion.Euler(0f, 0f, particle.rotation);

        color.a = alpha;
        particle.view.GetComponent<Image>().color = color;
    }

    protected override void DestroyView(Particle particle)
    {
        if (particle.view != null)
        {
            Destroy(particle.view);
        }
    }
}
