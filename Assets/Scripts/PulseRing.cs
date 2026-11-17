using UnityEngine;

// Okrąg rozchodzący się po ziemi jak kręgi na wodzie po wpadnięciu kropli. Pokazuje graczom, gdzie coś się stało
// (np. gdzie wylądował gracz po zamianie miejsc). Kilka fal po sobie, każda rośnie i blednie.
// Okrąg rysuje się nad blokami, żeby był widoczny także między nimi.
public class PulseRing : MonoBehaviour
{
    const int Waves = 3;
    const float Stagger = 0.22f;
    const float WaveSeconds = 1.0f;
    const float StartSize = 0.35f;
    const float EndSize = 3.4f;
    const int SortingOrder = 3;

    static Sprite ringSprite;

    SpriteRenderer[] rings;
    float age;
    Color color;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ringSprite = null;
    }

    public static void Spawn(Vector3 position, Color color)
    {
        GameObject go = new GameObject("PulseRing");
        go.transform.position = new Vector3(position.x, position.y, 0f);

        PulseRing ring = go.AddComponent<PulseRing>();
        ring.color = color;
        ring.rings = new SpriteRenderer[Waves];

        for (int i = 0; i < Waves; i++)
        {
            GameObject child = new GameObject("Wave" + i);
            child.transform.SetParent(go.transform, false);

            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = RingSprite();
            renderer.sortingOrder = SortingOrder;
            renderer.enabled = false;
            ring.rings[i] = renderer;
        }
    }

    void Update()
    {
        age += Time.deltaTime;

        for (int i = 0; i < rings.Length; i++)
        {
            float t = (age - i * Stagger) / WaveSeconds;
            bool active = t >= 0f && t <= 1f;
            rings[i].enabled = active;

            if (!active)
            {
                continue;
            }

            // fala szybko rozchodzi się na początku i zwalnia, jak koło na wodzie
            float eased = 1f - (1f - t) * (1f - t);
            float size = Mathf.Lerp(StartSize, EndSize, eased);
            rings[i].transform.localScale = Vector3.one * size;
            rings[i].color = new Color(color.r, color.g, color.b, color.a * Mathf.Pow(1f - t, 1.4f));
        }

        if (age > (Waves - 1) * Stagger + WaveSeconds)
        {
            Destroy(gameObject);
        }
    }

    // Biały cienki okrąg (średnica 1 jednostka), barwiony kolorem fali.
    static Sprite RingSprite()
    {
        if (ringSprite != null)
        {
            return ringSprite;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];
        float center = size * 0.5f;
        float outer = center - 1f;
        float inner = outer * 0.84f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float edge = Mathf.Clamp01(outer - distance + 0.5f) * Mathf.Clamp01(distance - inner + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(edge * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        ringSprite.hideFlags = HideFlags.HideAndDontSave;
        return ringSprite;
    }
}
