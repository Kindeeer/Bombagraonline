using UnityEngine;

// Krótkotrwały efekt z jednego sprite'a: rośnie lub maleje, zanika i znika.
public class PickupFx : MonoBehaviour
{
    SpriteRenderer spriteRenderer;
    Color color;
    Vector3 velocity;
    float gravity;
    float startScale;
    float endScale;
    float life;
    float age;

    public static PickupFx Spawn(Sprite sprite, Vector3 position, float startScale, float endScale, Color color,
        float life, int sortingOrder, Transform parent = null, Vector3 velocity = default, float gravity = 0f)
    {
        GameObject go = new GameObject("Fx");

        if (parent != null)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
        }
        else
        {
            go.transform.position = position;
        }

        go.transform.localScale = Vector3.one * startScale;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        PickupFx fx = go.AddComponent<PickupFx>();
        fx.spriteRenderer = renderer;
        fx.color = color;
        fx.velocity = velocity;
        fx.gravity = gravity;
        fx.startScale = startScale;
        fx.endScale = endScale;
        fx.life = Mathf.Max(0.01f, life);
        return fx;
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = age / life;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        velocity.y -= gravity * Time.deltaTime;
        transform.localPosition += velocity * Time.deltaTime;
        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);

        Color current = color;
        current.a *= 1f - t * t;
        spriteRenderer.color = current;
    }
}
