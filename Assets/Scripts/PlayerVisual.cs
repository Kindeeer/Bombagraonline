using UnityEngine;

public class PlayerVisual : MonoBehaviour
{
    public PlayerMovement movement;
    public SpriteRenderer body;

    [Header("Sprite'y kierunków")]
    public Sprite front;
    public Sprite back;
    public Sprite left;
    public Sprite right;

    [Header("Kolejność rysowania")]
    [Tooltip("Postać sortuje się po stopach i jest odrobinę niżej niż kafelki w jej rzędzie, więc zawsze stoi przed blokami obok siebie " +
        "(ręce szersze od pola nie znikają raz za blokiem, raz przed nim). Większa wartość = postać mocniej przed blokami obok.")]
    [Range(0f, 0.4f)] public float sortBias = 0.12f;

    [Header("Rozmiar")]
    [Tooltip("Skala postaci w grze. Grafiki zwierzaków mają 2 pola wysokości, przy 1 głowa wchodzi głęboko w blok nad postacią. " +
        "Mniej = mniejsza postać.")]
    [Range(0.4f, 1.2f)] public float bodyScale = 1f;

    [Header("Chodzenie (animacja z kodu)")]
    public float bobHeight = 0.05f;
    public float bobSpeed = 16f;
    public float squash = 0.05f;
    [Tooltip("Pochylenie do przodu przy chodzeniu w bok.")]
    public float tiltDegrees = 3f;
    [Tooltip("Kołysanie z boku na bok przy chodzeniu na wprost i do tyłu (postać ma jedną grafikę na kierunek, więc kroki robi kod).")]
    public float waddleDegrees = 7f;
    [Tooltip("Ile postać pochyla się w kierunku marszu, gdy idzie w bok.")]
    public float leanDegrees = 6f;
    public float settleSpeed = 12f;

    // wygląd z prefabu/sceny (zapamiętany w Awake), do którego wraca postać bez własnych grafik skina
    Sprite defaultFront;
    Sprite defaultBack;
    Sprite defaultLeft;
    Sprite defaultRight;
    Color defaultColor = Color.white;

    Transform bodyTransform;
    Vector3 basePosition;
    Vector3 baseScale = Vector3.one;
    Vector3 originalScale = Vector3.one;
    float walkTime;

    void Awake()
    {
        if (movement == null)
        {
            movement = GetComponentInParent<PlayerMovement>();
        }

        if (body == null)
        {
            body = GetComponent<SpriteRenderer>();
        }

        if (body == null)
        {
            body = GetComponentInChildren<SpriteRenderer>();
        }

        defaultFront = front;
        defaultBack = back;
        defaultLeft = left;
        defaultRight = right;
        defaultColor = body.color;

        bodyTransform = body.transform;

        // sortowanie po stopach (pivot), z lekkim przesunięciem w dół, żeby uniknąć remisu z blokami w tym samym rzędzie
        body.spriteSortPoint = SpriteSortPoint.Pivot;
        basePosition = bodyTransform.localPosition + Vector3.down * sortBias;

        // postacie z grafik mają 2 pola wysokości, więc w grze są pomniejszone, żeby głowa nie wchodziła w blok nad nimi
        originalScale = Vector3.one;
        baseScale = originalScale * bodyScale;
        bodyTransform.localScale = baseScale;
    }

    // Zmienia rozmiar postaci (ustawienie poczekalni "Wielkość postaci"). 1 = rozmiar z pola Body Scale.
    public void SetBodyScale(float multiplier)
    {
        baseScale = originalScale * bodyScale * multiplier;
        bodyTransform.localScale = baseScale;
    }

    // Ustawia grafiki postaci ze skina (GameArt). Pola skina, których brakuje, wracają do wyglądu domyślnego z prefabu,
    // więc zmiana skina nie miesza kierunków dwóch postaci. Skin bez żadnych grafik przywraca wygląd domyślny.
    public void SetSprites(PlayerSprites sprites)
    {
        bool has = sprites != null && sprites.HasSprites;

        front = has && sprites.Front != null ? sprites.Front : defaultFront;
        back = has && sprites.Back != null ? sprites.Back : defaultBack;
        left = has && sprites.Left != null ? sprites.Left : (has ? null : defaultLeft);
        right = has && sprites.Right != null ? sprites.Right : defaultRight;

        if (body != null)
        {
            body.color = has ? sprites.tint : defaultColor;
        }

        ApplyEffect(has ? sprites.fx : default);
    }

    // Efekt wizualny chromy (ogień, błyski, śnieg...) wokół postaci. Bez efektu emiter jest wyłączony.
    void ApplyEffect(ChromaFxLook look)
    {
        WorldChromaFx emitter = GetComponent<WorldChromaFx>();
        EnchantGlint glint = GetComponent<EnchantGlint>();

        if (emitter == null && look.HasParticles)
        {
            emitter = gameObject.AddComponent<WorldChromaFx>();
        }

        if (glint == null && look.glint)
        {
            glint = gameObject.AddComponent<EnchantGlint>();
        }

        if (emitter != null)
        {
            emitter.Configure(body, look);
        }

        if (glint != null)
        {
            glint.Configure(body, look);
        }
    }

    void Update()
    {
        body.sprite = SpriteFor(movement.Facing);

        // brak osobnej grafiki lewej strony: odbijamy prawą
        body.flipX = movement.Facing == Facing.Left && left == null;

        Vector3 targetPosition = basePosition;
        Vector3 targetScale = baseScale;
        float targetTilt = 0f;

        if (movement.IsMoving)
        {
            walkTime += Time.deltaTime * bobSpeed;

            float wave = Mathf.Sin(walkTime);
            targetPosition = basePosition + new Vector3(0f, Mathf.Abs(wave) * bobHeight, 0f) * bodyScale;
            targetScale = Vector3.Scale(baseScale, new Vector3(1f + wave * wave * squash, 1f - wave * wave * squash, 1f));

            // w bok: pochylenie w stronę marszu i lekkie bujanie, na wprost i do tyłu: kołysanie z nogi na nogę
            if (movement.Facing == Facing.Right)
            {
                targetTilt = -leanDegrees + wave * tiltDegrees;
            }
            else if (movement.Facing == Facing.Left)
            {
                targetTilt = leanDegrees + wave * tiltDegrees;
            }
            else
            {
                targetTilt = wave * waddleDegrees;
            }
        }
        else
        {
            walkTime = 0f;
        }

        float blend = 1f - Mathf.Exp(-settleSpeed * Time.deltaTime);

        bodyTransform.localPosition = Vector3.Lerp(bodyTransform.localPosition, targetPosition, blend);
        bodyTransform.localScale = Vector3.Lerp(bodyTransform.localScale, targetScale, blend);

        float currentTilt = Mathf.LerpAngle(bodyTransform.localEulerAngles.z, targetTilt, blend);
        bodyTransform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
    }

    // Kolejność rysowania względem bloków. Postać jest szersza niż jedno pole, więc jej ręce i uszy nachodzą na pola obok.
    // Sama wysokość na planszy nie wystarcza: stojąc równo w rzędzie postać remisowała ze ścianami z tego rzędu (raz była
    // przed nimi, raz za nimi), a idąc w górę korytarzem chowała się za ściany, które miała po bokach.
    // Dlatego liczymy "rząd stóp": najniższy rząd, którego dotyka pole postaci. Postać rysuje się przed wszystkimi blokami
    // z tego rzędu i wyższych (ściany obok nie wcinają się w ręce), ale za blokami z rzędów poniżej (efekt 2.5D zostaje:
    // blok stojący bliżej kamery zasłania jej nogi).
    // Wynik wpisujemy w pozycję Z postaci. Kamera patrzy prosto, więc Z nie przesuwa nic na ekranie, a oś sortowania
    // w Renderer2D to (0, 1, 1), czyli o kolejności decyduje suma Y + Z.
    const float FrontOfRow = 0.2f;

    void LateUpdate()
    {
        if (movement == null)
        {
            return;
        }

        Transform root = movement.transform;
        Vector3 position = root.position;
        root.position = new Vector3(position.x, position.y, SortDepth(position.y));
    }

    // Pozycja Z dla postaci stojącej na wysokości y (środek jej pola), patrz opis wyżej.
    public static float SortDepth(float y)
    {
        float footRow = Mathf.Floor(y - 0.5f + 0.001f);
        float inRow = Mathf.Clamp01(y - 0.5f - footRow);

        // dwie postacie w tym samym rzędzie: ta niżej jest z przodu
        float sortY = footRow + 0.5f - FrontOfRow + inRow * 0.1f;
        return sortY - y;
    }

    Sprite SpriteFor(Facing facing)
    {
        switch (facing)
        {
            case Facing.Up: return back;
            case Facing.Left: return left != null ? left : right;
            case Facing.Right: return right;
        }

        return front;
    }
}
