using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Fontanna z czekolady na środku planszy (domyślnie 3x3 pola). Co jakiś czas tryska jak hydrant i wyrzuca bąbelki z przedmiotami.
// Obiekt fontanny jest w scenie. Pozycję ustawia sam według GridMap, a grafiki bierze z obiektu GameArt.
[RequireComponent(typeof(PickupSpawner))]
public class ChocolateFountain : MonoBehaviour
{
    enum Phase { Idle, Telegraph, Gush }

    class Drop
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector2 velocity;
        public float landY;
        public float size;
        public bool splash;
        public bool active;
    }

    const float SpoutHeight = 0.79f;
    const float RimHeight = 0.46f;

    static readonly Color DropDark = new Color(0.36f, 0.18f, 0.08f);
    static readonly Color DropLight = new Color(0.62f, 0.36f, 0.17f);
    static readonly Color RippleColor = new Color(0.78f, 0.52f, 0.30f, 0.8f);

    [Header("Pozycja i rozmiar")]
    [Tooltip("Zaznaczone: środek planszy. Odznacz, żeby wpisać pole ręcznie.")]
    public bool useMapCenter = true;
    [Tooltip("Środkowe pole fontanny.")]
    public Vector2Int cell = new Vector2Int(7, 7);
    [Tooltip("Ile pól zajmuje fontanna w każdą stronę (3 = obszar 3x3). Te pola stają się nieprzechodnie.")]
    [Min(1)] public int size = 3;
    [Tooltip("Powiększenie grafiki fontanny.")]
    public float visualScale = 2.3f;
    [Tooltip("Przesunięcie grafiki w pionie względem środkowego pola.")]
    public float visualOffsetY = -0.35f;

    [Header("Strumień jak z hydrantu")]
    public float firstBurstDelay = 15f;
    public float burstInterval = 25f;
    public float burstIntervalRandom = 5f;
    [Tooltip("Ostrzeżenie przed strumieniem: fontanna się trzęsie.")]
    public float telegraphTime = 1.2f;
    public float gushTime = 1.6f;
    [Tooltip("Wysokość strumienia w jednostkach fontanny (mnożona przez Visual Scale).")]
    public float streamHeight = 1.4f;

    [Header("Bąbelki z przedmiotami")]
    public int minBubbles = 2;
    public int maxBubbles = 4;
    public float bubbleFlightTime = 1f;
    public float bubbleArcHeight = 2.2f;

    [Header("Animacja płynącej czekolady")]
    public float jetRate = 12f;
    public float rimRate = 20f;
    public float gushRate = 70f;
    public float gravity = 6f;
    public int maxDrops = 260;

    [Header("Test")]
    [Tooltip("Klawisz B od razu uruchamia strumień.")]
    public bool debugBurstKey = true;

    GridMap map;
    Phase phase = Phase.Idle;
    float nextBurstIn;
    float phaseTime;
    float time;

    float[] bubbleTimes = new float[0];
    int bubblesLaunched;

    float jetBudget;
    float rimBudget;
    float gushBudget;
    float streamLevel;

    Transform body;
    SpriteRenderer bodyRenderer;
    Transform stream;
    SpriteRenderer streamRenderer;

    readonly List<Drop> drops = new List<Drop>();
    readonly Stack<Drop> freeDrops = new Stack<Drop>();

    void Start()
    {
        map = GridMap.Instance;

        if (map == null)
        {
            Debug.LogError("ChocolateFountain: w scenie nie ma GridMap.", this);
            enabled = false;
            return;
        }

        if (useMapCenter)
        {
            cell = new Vector2Int(GridMap.Width / 2, GridMap.Height / 2);
        }

        int low = -(size / 2);
        int high = low + size - 1;

        for (int x = low; x <= high; x++)
        {
            for (int y = low; y <= high; y++)
            {
                map.ClaimCell(cell + new Vector2Int(x, y));
            }
        }

        transform.position = map.GridToWorld(cell) + Vector3.up * visualOffsetY;
        transform.localScale = Vector3.one * visualScale;

        BuildVisuals();
        ScheduleNextBurst(firstBurstDelay);
    }

    void BuildVisuals()
    {
        SortingGroup group = GetComponent<SortingGroup>();

        if (group == null)
        {
            group = gameObject.AddComponent<SortingGroup>();
        }

        group.sortingOrder = 1;

        body = new GameObject("Body").transform;
        body.SetParent(transform, false);
        bodyRenderer = body.gameObject.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = PickupArt.FountainBody();
        bodyRenderer.sortingOrder = 0;

        stream = new GameObject("Stream").transform;
        stream.SetParent(transform, false);
        stream.localPosition = new Vector3(0f, SpoutHeight, 0f);
        streamRenderer = stream.gameObject.AddComponent<SpriteRenderer>();
        streamRenderer.sprite = PickupArt.Stream();
        streamRenderer.sortingOrder = 3;
        streamRenderer.enabled = false;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        time += dt;

        UpdateSchedule(dt);
        UpdateEmission(dt);
        UpdateDrops(dt);
        UpdateStream();
        UpdateShake();

        if (debugBurstKey && BurstKeyPressed())
        {
            TriggerBurst();
        }
    }

    bool BurstKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.B);
#endif
    }

    // Uruchamia strumień od razu (pomija odliczanie).
    [ContextMenu("Strumień teraz")]
    public void TriggerBurst()
    {
        if (phase != Phase.Idle)
        {
            return;
        }

        phase = Phase.Telegraph;
        phaseTime = 0f;
    }

    void ScheduleNextBurst(float seconds)
    {
        nextBurstIn = Mathf.Max(1f, seconds);
    }

    void UpdateSchedule(float dt)
    {
        switch (phase)
        {
            case Phase.Idle:
                nextBurstIn -= dt;

                if (nextBurstIn <= 0f)
                {
                    TriggerBurst();
                }
                break;

            case Phase.Telegraph:
                phaseTime += dt;

                if (phaseTime >= telegraphTime)
                {
                    StartGush();
                }
                break;

            case Phase.Gush:
                phaseTime += dt;
                LaunchDueBubbles();

                if (phaseTime >= gushTime)
                {
                    phase = Phase.Idle;
                    ScheduleNextBurst(burstInterval + Random.Range(-burstIntervalRandom, burstIntervalRandom));
                }
                break;
        }
    }

    void StartGush()
    {
        phase = Phase.Gush;
        phaseTime = 0f;

        int count = Random.Range(minBubbles, Mathf.Max(minBubbles, maxBubbles) + 1);
        float span = Mathf.Max(0.1f, gushTime - 0.5f);

        bubbleTimes = new float[count];
        bubblesLaunched = 0;

        for (int i = 0; i < count; i++)
        {
            bubbleTimes[i] = 0.2f + span * (i + Random.value * 0.5f) / count;
        }
    }

    void LaunchDueBubbles()
    {
        while (bubblesLaunched < bubbleTimes.Length && phaseTime >= bubbleTimes[bubblesLaunched])
        {
            bubblesLaunched++;
            LaunchBubble();
        }
    }

    void LaunchBubble()
    {
        PickupSpawner spawner = PickupSpawner.Instance;

        if (spawner == null || !spawner.TryFindFreeCell(out Vector2Int target))
        {
            return;
        }

        PickupDefinition definition = spawner.PickFromFountain();

        if (definition == null)
        {
            return;
        }

        Vector3 from = transform.TransformPoint(new Vector3(0f, SpoutHeight + streamHeight * streamLevel, 0f));
        float flight = bubbleFlightTime + Random.Range(0f, 0.3f);

        spawner.LaunchBubble(definition, target, from, flight, bubbleArcHeight);
    }

    // ---------- Animacja płynącej czekolady ----------

    void UpdateEmission(float dt)
    {
        float activity = 1f;

        if (phase == Phase.Telegraph)
        {
            activity = 1f + 1.5f * Mathf.Clamp01(phaseTime / Mathf.Max(0.01f, telegraphTime));
        }
        else if (phase == Phase.Gush)
        {
            activity = 2.5f;
        }

        jetBudget += jetRate * activity * dt;

        while (jetBudget >= 1f)
        {
            jetBudget -= 1f;
            SpawnJetDrop();
        }

        rimBudget += rimRate * activity * dt;

        while (rimBudget >= 1f)
        {
            rimBudget -= 1f;
            SpawnRimDrop();
        }

        if (phase == Phase.Gush)
        {
            gushBudget += gushRate * streamLevel * dt;

            while (gushBudget >= 1f)
            {
                gushBudget -= 1f;
                SpawnGushDrop();
            }
        }
        else
        {
            gushBudget = 0f;
        }
    }

    // Kropla z dyszy: leci łukiem do basenu.
    void SpawnJetDrop()
    {
        LaunchDrop(
            new Vector2(Random.Range(-0.03f, 0.03f), SpoutHeight),
            new Vector2(Random.Range(-0.55f, 0.55f), Random.Range(0.9f, 1.7f)),
            Random.Range(-0.06f, 0.10f),
            Random.Range(0.08f, 0.13f),
            false);
    }

    // Kropla przelewająca się przez brzeg miski.
    void SpawnRimDrop()
    {
        float x = Random.Range(-0.34f, 0.34f);

        LaunchDrop(
            new Vector2(x, RimHeight),
            new Vector2(x * 0.25f, 0f),
            Random.Range(-0.04f, 0.06f),
            Random.Range(0.06f, 0.10f),
            Random.value < 0.4f);
    }

    // Rozbryzg z czubka strumienia: spada na podłogę wokół fontanny.
    void SpawnGushDrop()
    {
        float top = SpoutHeight + streamHeight * streamLevel;

        LaunchDrop(
            new Vector2(Random.Range(-0.08f, 0.08f), top),
            new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(0.8f, 3f)),
            Random.Range(-0.35f, 0.05f),
            Random.Range(0.08f, 0.16f),
            true);
    }

    Drop AcquireDrop()
    {
        if (freeDrops.Count > 0)
        {
            return freeDrops.Pop();
        }

        if (drops.Count >= maxDrops)
        {
            return null;
        }

        GameObject go = new GameObject("Drop");
        go.transform.SetParent(transform, false);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = PickupArt.Drop();
        renderer.sortingOrder = 2;

        Drop drop = new Drop();
        drop.transform = go.transform;
        drop.renderer = renderer;
        drops.Add(drop);
        return drop;
    }

    void LaunchDrop(Vector2 position, Vector2 velocity, float landY, float size, bool splash)
    {
        Drop drop = AcquireDrop();

        if (drop == null)
        {
            return;
        }

        drop.active = true;
        drop.velocity = velocity;
        drop.landY = landY;
        drop.size = size;
        drop.splash = splash;

        drop.transform.localPosition = position;
        drop.renderer.color = Color.Lerp(DropDark, DropLight, Random.value);
        drop.renderer.enabled = true;
        ApplyDropScale(drop);
    }

    void UpdateDrops(float dt)
    {
        for (int i = 0; i < drops.Count; i++)
        {
            Drop drop = drops[i];

            if (!drop.active)
            {
                continue;
            }

            drop.velocity.y -= gravity * dt;

            Vector3 position = drop.transform.localPosition;
            position.x += drop.velocity.x * dt;
            position.y += drop.velocity.y * dt;
            drop.transform.localPosition = position;

            if (drop.velocity.y < 0f && position.y <= drop.landY)
            {
                Land(drop, position.x);
            }
            else
            {
                ApplyDropScale(drop);
            }
        }
    }

    void ApplyDropScale(Drop drop)
    {
        float stretch = 1f + Mathf.Min(Mathf.Abs(drop.velocity.y) * 0.25f, 0.9f);
        drop.transform.localScale = new Vector3(drop.size, drop.size * stretch, 1f);
    }

    void Land(Drop drop, float x)
    {
        drop.active = false;
        drop.renderer.enabled = false;
        freeDrops.Push(drop);

        if (drop.splash)
        {
            PickupFx.Spawn(PickupArt.Ripple(), new Vector3(x, drop.landY, 0f), 0.12f, 0.4f, RippleColor, 0.45f, 1, transform);
        }
    }

    // ---------- Strumień, trzęsienie ----------

    void UpdateStream()
    {
        float target = 0f;

        if (phase == Phase.Gush)
        {
            float rise = Mathf.Clamp01(phaseTime / 0.25f);
            float fall = Mathf.Clamp01((gushTime - phaseTime) / 0.4f);
            float linear = Mathf.Min(rise, fall);
            target = 1f - (1f - linear) * (1f - linear);
        }

        streamLevel = target;

        bool visible = streamLevel > 0.01f;
        streamRenderer.enabled = visible;

        if (!visible)
        {
            return;
        }

        float wobble = 1f + Mathf.Sin(time * 35f) * 0.15f;

        stream.localScale = new Vector3(wobble * (0.5f + 0.5f * streamLevel), streamHeight * 0.5f * streamLevel, 1f);
        stream.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 18f) * 2f);
    }

    void UpdateShake()
    {
        float offset = 0f;
        Color tint = Color.white;

        if (phase == Phase.Telegraph)
        {
            float progress = Mathf.Clamp01(phaseTime / Mathf.Max(0.01f, telegraphTime));
            offset = Mathf.Sin(time * 70f) * (0.012f + 0.02f * progress);

            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 14f);
            tint = Color.Lerp(Color.white, new Color(1f, 0.8f, 0.7f), pulse * progress);
        }
        else if (phase == Phase.Gush)
        {
            offset = Mathf.Sin(time * 90f) * 0.015f;
        }

        body.localPosition = new Vector3(offset, 0f, 0f);
        bodyRenderer.color = tint;
    }
}
