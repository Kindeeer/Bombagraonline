using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;

// Bąbelek z przedmiotem. Może leżeć na polu albo lecieć łukiem z fontanny.
public class PickupItem : MonoBehaviour
{
    public PickupDefinition Definition { get; private set; }
    public Vector2Int Cell { get; private set; }
    public bool CanCollect => landed && !leaving;

    // Bąbelek leżący na planszy rysuje się nad blokami, żeby wystający górny kawałek bloku z rzędu przed nim go nie zasłaniał.
    const int LandedSortingOrder = 2;

    SortingGroup group;
    Transform visual;
    SpriteRenderer shadowRenderer;
    SpriteRenderer bubbleRenderer;
    SpriteRenderer iconRenderer;

    bool landed;
    bool leaving;
    float age;
    float landedAt;
    float fireImmuneUntil;
    float leaveTime;

    Vector3 flightFrom;
    Vector3 flightTo;
    float flightTime;
    float flightDuration;
    float flightArc;

    public void InitOnGround(PickupDefinition definition, Vector2Int cell, float fireImmuneSeconds)
    {
        Setup(definition, cell);
        transform.position = GridMap.Instance.GridToWorld(cell);
        fireImmuneUntil = Time.time + fireImmuneSeconds;
        Land(false);
    }

    public void InitInFlight(PickupDefinition definition, Vector2Int cell, Vector3 from, float duration, float arc)
    {
        Setup(definition, cell);

        flightFrom = from;
        flightTo = GridMap.Instance.GridToWorld(cell);
        flightDuration = Mathf.Max(0.05f, duration);
        flightArc = arc;
        flightTime = 0f;

        landed = false;
        group.sortingOrder = 3;
        shadowRenderer.enabled = false;
        transform.position = from;
        visual.localScale = Vector3.one * 0.35f;
    }

    public void Collect()
    {
        StartLeaving();
    }

    void Setup(PickupDefinition definition, Vector2Int cell)
    {
        Definition = definition;
        Cell = cell;

        group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = LandedSortingOrder;

        shadowRenderer = AddRenderer(transform, "Shadow", PickupArt.Shadow(), -1);
        shadowRenderer.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        shadowRenderer.transform.localScale = Vector3.one * 0.65f;

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);

        bubbleRenderer = AddRenderer(visual, "Bubble", PickupArt.Bubble(), 0);
        bubbleRenderer.color = definition.bubbleTint;
        iconRenderer = AddRenderer(visual, "Icon", definition.Icon, 1);

        PickupRegistry.Register(this);
    }

    static SpriteRenderer AddRenderer(Transform parent, string objectName, Sprite sprite, int order)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    void Update()
    {
        age += Time.deltaTime;

        if (leaving)
        {
            UpdateLeaving();
            return;
        }

        if (!landed)
        {
            UpdateFlight();
            return;
        }

        float appear = EaseOutBack(Mathf.Clamp01((age - landedAt) / 0.3f));
        float pulse = 1f + Mathf.Sin(age * 4f) * 0.03f;

        visual.localPosition = new Vector3(0f, 0.1f + Mathf.Sin(age * 3f) * 0.04f, 0f);
        visual.localScale = Vector3.one * (appear * pulse);

        if (Time.time >= fireImmuneUntil && GridMap.Instance != null && GridMap.Instance.IsOnFire(Cell))
        {
            Pop();
        }
    }

    void UpdateFlight()
    {
        flightTime += Time.deltaTime;
        float u = Mathf.Clamp01(flightTime / flightDuration);

        Vector3 position = Vector3.Lerp(flightFrom, flightTo, u);
        position.y += flightArc * 4f * u * (1f - u);
        transform.position = position;

        visual.localPosition = Vector3.zero;
        visual.localScale = Vector3.one * Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, u));

        if (u >= 1f)
        {
            Land(true);
        }
    }

    void Land(bool withEffect)
    {
        landed = true;
        landedAt = age;
        transform.position = GridMap.Instance.GridToWorld(Cell);
        group.sortingOrder = LandedSortingOrder;
        shadowRenderer.enabled = true;

        if (withEffect)
        {
            fireImmuneUntil = Time.time + 0.3f;

            PickupFx.Spawn(PickupArt.Ripple(), transform.position + Vector3.down * 0.3f, 0.3f, 1.1f,
                new Color(0.62f, 0.36f, 0.17f, 0.8f), 0.5f, 0);
        }
    }

    void Pop()
    {
        for (int i = 0; i < 6; i++)
        {
            float angle = (i / 6f) * Mathf.PI * 2f;
            Vector3 velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) + 0.6f, 0f) * 1.2f;

            PickupFx.Spawn(PickupArt.Drop(), transform.position + Vector3.up * 0.1f, 0.12f, 0.04f,
                new Color(0.8f, 0.95f, 1f, 0.9f), 0.4f, 4, null, velocity, 4f);
        }

        StartLeaving();
    }

    void StartLeaving()
    {
        if (leaving)
        {
            return;
        }

        leaving = true;
        leaveTime = 0f;
        shadowRenderer.enabled = false;
        PickupRegistry.Unregister(this);
    }

    void UpdateLeaving()
    {
        leaveTime += Time.deltaTime;
        float t = Mathf.Clamp01(leaveTime / 0.18f);

        visual.localScale = Vector3.one * Mathf.Lerp(1f, 1.5f, t);

        Color bubbleColor = Definition.bubbleTint;
        bubbleColor.a *= 1f - t;
        bubbleRenderer.color = bubbleColor;
        iconRenderer.color = new Color(1f, 1f, 1f, 1f - t);

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        PickupRegistry.Unregister(this);
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}

public static class PickupRegistry
{
    static readonly Dictionary<Vector2Int, PickupItem> items = new Dictionary<Vector2Int, PickupItem>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        items.Clear();
    }

    public static void Register(PickupItem item)
    {
        items[item.Cell] = item;
    }

    public static void Unregister(PickupItem item)
    {
        if (items.TryGetValue(item.Cell, out PickupItem current) && current == item)
        {
            items.Remove(item.Cell);
        }
    }

    public static bool Has(Vector2Int cell)
    {
        return items.ContainsKey(cell);
    }

    public static bool TryGet(Vector2Int cell, out PickupItem item)
    {
        return items.TryGetValue(cell, out item);
    }
}
