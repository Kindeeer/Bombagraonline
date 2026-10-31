using UnityEngine;

// Efekty chromy wokół postaci w świecie gry (ogień, błyski, śnieg itd.). Cząstki są zwykłymi sprite'ami, które zostają w miejscu,
// gdzie powstały, więc za biegnącą postacią ciągnie się ślad.
public class WorldChromaFx : ChromaFxEmitter
{
    SpriteRenderer body;
    Transform holder;

    public void Configure(SpriteRenderer bodyRenderer, ChromaFxLook look)
    {
        body = bodyRenderer;
        SetLook(look);
    }

    protected override bool GetBody(out Vector2 feet, out float height, out float width)
    {
        feet = Vector2.zero;
        height = 1f;
        width = 1f;

        if (body == null || body.sprite == null || !body.enabled || !body.gameObject.activeInHierarchy || body.color.a < 0.2f)
        {
            return false;
        }

        Bounds bounds = body.bounds;
        feet = new Vector2(bounds.center.x, bounds.min.y);
        height = bounds.size.y;
        width = bounds.size.x;
        return true;
    }

    protected override void CreateView(Particle particle, int spriteKind)
    {
        if (holder == null)
        {
            holder = new GameObject("ChromaFx").transform;
        }

        GameObject view = new GameObject("P");
        view.transform.SetParent(holder, false);

        SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
        renderer.sprite = ChromaFx.SpriteOf(spriteKind);
        renderer.sortingLayerID = body != null ? body.sortingLayerID : 0;
        renderer.sortingOrder = (body != null ? body.sortingOrder : 1) + 1;
        particle.view = view;
    }

    protected override void ApplyView(Particle particle, float alpha, float scale, Color color)
    {
        if (particle.view == null)
        {
            return;
        }

        Transform t = particle.view.transform;
        t.position = new Vector3(particle.position.x, particle.position.y, 0f);
        t.rotation = Quaternion.Euler(0f, 0f, particle.rotation);
        t.localScale = Vector3.one * (particle.size * scale);

        color.a = alpha;
        particle.view.GetComponent<SpriteRenderer>().color = color;
    }

    protected override void DestroyView(Particle particle)
    {
        if (particle.view != null)
        {
            Destroy(particle.view);
        }
    }

    void OnDestroy()
    {
        Clear();

        if (holder != null)
        {
            Destroy(holder.gameObject);
        }
    }
}
