using UnityEngine;
using UnityEngine.Rendering;

// Efekty chromy wokół postaci w świecie gry (ogień, błyski, śnieg itd.). Cząstki są zwykłymi sprite'ami, które zostają w miejscu,
// gdzie powstały, więc za biegnącą postacią ciągnie się ślad. Część cząstek rysuje się za postacią, część przed nią,
// a wszystkie biorą udział w tej samej kolejności rysowania co postać i bloki (blok stojący bliżej kamery je zasłania).
public class WorldChromaFx : ChromaFxEmitter
{
    // o tyle cząstka jest "bliżej" albo "dalej" niż postać w kolejności rysowania
    const float DepthStep = 0.02f;

    SpriteRenderer body;
    Transform holder;

    // obiekt, według którego sortuje się postać (grupa sortowania gracza albo sam sprite), i jej warstwa
    Transform sortAnchor;
    int sortingLayer;
    int sortingOrder;

    public void Configure(SpriteRenderer bodyRenderer, ChromaFxLook look)
    {
        body = bodyRenderer;

        if (body != null)
        {
            SortingGroup group = body.GetComponentInParent<SortingGroup>();
            sortAnchor = group != null ? group.transform : body.transform;
            sortingLayer = group != null ? group.sortingLayerID : body.sortingLayerID;
            sortingOrder = group != null ? group.sortingOrder : body.sortingOrder;
        }

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

    protected override void CreateView(Particle particle, Sprite sprite)
    {
        if (holder == null)
        {
            holder = new GameObject("ChromaFx").transform;
        }

        GameObject view = new GameObject("P");
        view.transform.SetParent(holder, false);

        SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerID = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        particle.view = view;

        // miejsce w kolejności rysowania: tuż przed postacią albo tuż za nią (oś sortowania to Y + Z, patrz PlayerVisual)
        float anchorSort = sortAnchor != null ? sortAnchor.position.y + sortAnchor.position.z : particle.position.y;
        particle.sort = anchorSort + (particle.front ? -DepthStep : DepthStep);
    }

    protected override void ApplyView(Particle particle, float alpha, float scale, Color color)
    {
        if (particle.view == null)
        {
            return;
        }

        Transform t = particle.view.transform;
        t.position = new Vector3(particle.position.x, particle.position.y, particle.sort - particle.position.y);
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
