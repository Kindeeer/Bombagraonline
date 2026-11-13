using UnityEngine;

// Kolorowy pierścień pod stopami postaci w trybie drużynowym (niebieski albo czerwony), żeby od razu było widać,
// kto jest z kim. Rysowany z kodu, leży tam gdzie cień i znika, gdy postać zginie.
public class TeamRing : MonoBehaviour
{
    const int TextureWidth = 96;
    const int TextureHeight = 52;
    // szerokość pierścienia w polach planszy
    const float WorldWidth = 0.98f;

    static Sprite ringSprite;

    PlayerHealth health;
    SpriteRenderer ring;
    float fade = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ringSprite = null;
    }

    // Dokłada pierścień do postaci (albo zmienia kolor już istniejącego).
    public static void Attach(GameObject character, Color color)
    {
        TeamRing existing = character.GetComponentInChildren<TeamRing>();

        if (existing == null)
        {
            // pierścień leży tam, gdzie cień postaci, i rysuje się w tej samej warstwie
            SpriteRenderer shadow = null;

            foreach (SpriteRenderer renderer in character.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.name == "Shadow")
                {
                    shadow = renderer;
                }
            }

            GameObject go = new GameObject("TeamRing");
            go.transform.SetParent(shadow != null ? shadow.transform.parent : character.transform, false);
            go.transform.localPosition = (shadow != null ? shadow.transform.localPosition : new Vector3(0f, -0.38f, 0f)) + new Vector3(0f, 0f, -0.01f);

            existing = go.AddComponent<TeamRing>();
            existing.health = character.GetComponent<PlayerHealth>();
            existing.ring = go.AddComponent<SpriteRenderer>();
            existing.ring.sprite = RingSprite();

            if (shadow != null)
            {
                existing.ring.sortingLayerID = shadow.sortingLayerID;
                existing.ring.sortingOrder = shadow.sortingOrder;
            }
        }

        existing.ring.color = color;
    }

    void Update()
    {
        // po śmierci pierścień szybko blednie i znika
        if (health != null && health.IsDead)
        {
            fade -= Time.deltaTime * 3f;

            if (fade <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Color color = ring.color;
            color.a = fade;
            ring.color = color;
        }
    }

    // Biała elipsa: mocna obwódka i lekko wypełniony środek. Kolor drużyny nadaje SpriteRenderer.
    static Sprite RingSprite()
    {
        if (ringSprite != null)
        {
            return ringSprite;
        }

        Texture2D texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[TextureWidth * TextureHeight];

        for (int y = 0; y < TextureHeight; y++)
        {
            for (int x = 0; x < TextureWidth; x++)
            {
                // odległość od środka elipsy: 0 = środek, 1 = brzeg
                float nx = (x + 0.5f) / TextureWidth * 2f - 1f;
                float ny = (y + 0.5f) / TextureHeight * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);

                float outer = Mathf.Clamp01((0.97f - distance) / 0.06f);
                float inner = Mathf.Clamp01((distance - 0.66f) / 0.08f);
                float alpha = outer * Mathf.Lerp(0.28f, 0.95f, inner);

                pixels[y * TextureWidth + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        ringSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureWidth, TextureHeight), new Vector2(0.5f, 0.5f), TextureWidth / WorldWidth);
        ringSprite.name = "TeamRing";
        return ringSprite;
    }
}
