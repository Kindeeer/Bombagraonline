using System.Collections.Generic;
using UnityEngine;

// Rozsypka w pył: sprite gracza rozpada się na małe kwadratowe kawałki. Rozpad zaczyna się od głowy i schodzi w dół,
// kawałki spadają i układają się w kupkę u stóp postaci. Po chwili kupka blednie razem z cieniem i znika.
// Kawałki wycinamy z tej samej tekstury co postać, więc efekt działa z każdą grafiką (także z własnymi skinami).
public class DeathDust : MonoBehaviour
{
    // Przyspieszenie spadania kawałków (jednostki świata na sekundę do kwadratu).
    const float Gravity = 14f;
    // O ile zmniejszają się kawałki, gdy leżą w kupce.
    const float LandedScale = 0.55f;

    class Piece
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector3 start;
        public Vector3 target;
        public Vector3 startScale;
        public Vector3 endScale;
        public Color color;
        public float startAngle;
        public float endAngle;
        public float delay;
        public float fallTime;
    }

    class Faded
    {
        public SpriteRenderer renderer;
        public Color color;
    }

    readonly List<Piece> pieces = new List<Piece>();
    readonly List<Faded> fadeAlong = new List<Faded>();
    float time;
    float endOfFall;
    float holdSeconds;
    float fadeSeconds;

    // Tworzy pył z aktualnego sprite'a. Zwraca null, gdy nie ma czego rozsypać (wtedy nic nie jest ukryte).
    // crumbleSeconds: ile trwa rozpad od głowy do stóp. fadeWithPile: np. cień, który ma zniknąć razem z kupką.
    public static DeathDust Play(SpriteRenderer source, int columns = 8, int rows = 8, float crumbleSeconds = 0.8f,
        SpriteRenderer[] fadeWithPile = null, float holdSeconds = 0.6f, float fadeSeconds = 0.7f)
    {
        if (source == null || source.sprite == null || columns < 1 || rows < 1)
        {
            return null;
        }

        Sprite sprite = source.sprite;
        Texture2D texture = sprite.texture;
        Rect rect = sprite.textureRect;
        float pixelsPerUnit = sprite.pixelsPerUnit;

        // pivot liczony od lewego dolnego rogu wyciętego fragmentu tekstury
        Vector2 pivot = sprite.pivot - sprite.textureRectOffset;

        Transform sourceTransform = source.transform;
        Matrix4x4 toWorld = sourceTransform.localToWorldMatrix;
        Vector3 scale = sourceTransform.lossyScale;
        Quaternion rotation = sourceTransform.rotation;

        // kupka leży na dole sprite'a (u stóp), a jej rozmiar zależy od wielkości postaci
        Bounds bounds = source.bounds;
        float groundY = bounds.min.y;
        float centerX = bounds.center.x;
        float pileHalfWidth = Mathf.Max(0.15f, bounds.extents.x * 0.9f);
        float moundHeight = Mathf.Max(0.1f, bounds.size.y * 0.16f);

        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;
        float landedHalf = 0.5f * Mathf.Max(cellWidth, cellHeight) / pixelsPerUnit * Mathf.Abs(scale.y) * LandedScale;

        GameObject root = new GameObject("DeathDust");
        DeathDust dust = root.AddComponent<DeathDust>();
        dust.holdSeconds = Mathf.Max(0f, holdSeconds);
        dust.fadeSeconds = Mathf.Max(0.05f, fadeSeconds);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Rect part = new Rect(rect.x + column * cellWidth, rect.y + row * cellHeight, cellWidth, cellHeight);

                if (texture.isReadable && IsEmpty(texture, part))
                {
                    continue;
                }

                Vector2 offsetPixels = new Vector2((column + 0.5f) * cellWidth, (row + 0.5f) * cellHeight) - pivot;
                Vector3 local = new Vector3(
                    offsetPixels.x / pixelsPerUnit * (source.flipX ? -1f : 1f),
                    offsetPixels.y / pixelsPerUnit * (source.flipY ? -1f : 1f),
                    0f);

                GameObject go = new GameObject("Dust");
                go.transform.SetParent(root.transform, false);
                go.transform.position = toWorld.MultiplyPoint3x4(local);
                go.transform.rotation = rotation;
                go.transform.localScale = scale;

                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = Sprite.Create(texture, part, new Vector2(0.5f, 0.5f), pixelsPerUnit);
                renderer.color = source.color;
                renderer.sharedMaterial = source.sharedMaterial;
                renderer.sortingLayerID = source.sortingLayerID;
                renderer.sortingOrder = source.sortingOrder + 1;

                // miejsce w kupce: najwięcej kawałków przy środku, najwyżej pośrodku (kopczyk)
                float spread = Random.value + Random.value - 1f;
                float height = (1f - Mathf.Abs(spread)) * moundHeight * Random.Range(0.25f, 1f);

                Piece piece = new Piece();
                piece.transform = go.transform;
                piece.renderer = renderer;
                piece.start = go.transform.position;
                piece.target = new Vector3(centerX + spread * pileHalfWidth, groundY + landedHalf + height, piece.start.z);
                piece.startScale = scale;
                piece.endScale = scale * LandedScale;
                piece.color = source.color;
                piece.startAngle = rotation.eulerAngles.z;
                piece.endAngle = piece.startAngle + Random.Range(-60f, 60f);

                // wyższe kawałki ruszają pierwsze, więc postać rozsypuje się od góry
                piece.delay = (1f - (row + 0.5f) / rows) * Mathf.Max(0.1f, crumbleSeconds) + Random.Range(0f, 0.1f);
                float drop = Mathf.Max(0.02f, piece.start.y - piece.target.y);
                piece.fallTime = Mathf.Max(0.12f, Mathf.Sqrt(2f * drop / Gravity));

                dust.endOfFall = Mathf.Max(dust.endOfFall, piece.delay + piece.fallTime);
                dust.pieces.Add(piece);
            }
        }

        if (dust.pieces.Count == 0)
        {
            Destroy(root);
            return null;
        }

        if (fadeWithPile != null)
        {
            foreach (SpriteRenderer other in fadeWithPile)
            {
                if (other != null)
                {
                    Faded faded = new Faded();
                    faded.renderer = other;
                    faded.color = other.color;
                    dust.fadeAlong.Add(faded);
                }
            }
        }

        return dust;
    }

    void Update()
    {
        time += Time.deltaTime;

        // po upadku wszystkich kawałków kupka chwilę leży, potem blednie
        float fade = 1f;
        float fadeStart = endOfFall + holdSeconds;

        if (time > fadeStart)
        {
            fade = 1f - (time - fadeStart) / fadeSeconds;

            if (fade <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            Piece piece = pieces[i];
            float age = time - piece.delay;

            if (age >= 0f)
            {
                float k = Mathf.Clamp01(age / piece.fallTime);
                float slide = Mathf.SmoothStep(0f, 1f, k);

                piece.transform.position = new Vector3(
                    Mathf.Lerp(piece.start.x, piece.target.x, slide),
                    Mathf.Lerp(piece.start.y, piece.target.y, k * k),
                    piece.start.z);
                piece.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(piece.startAngle, piece.endAngle, slide));
                piece.transform.localScale = Vector3.Lerp(piece.startScale, piece.endScale, slide);
            }

            Color color = piece.color;
            color.a *= fade;
            piece.renderer.color = color;
        }

        foreach (Faded faded in fadeAlong)
        {
            if (faded.renderer != null)
            {
                Color color = faded.color;
                color.a *= fade;
                faded.renderer.color = color;
            }
        }
    }

    // Sprite'y kawałków tworzymy w kodzie, więc sprzątamy je razem z pyłem. Cień po zniknięciu kupki zostaje ukryty.
    void OnDestroy()
    {
        foreach (Piece piece in pieces)
        {
            if (piece.renderer != null && piece.renderer.sprite != null)
            {
                Destroy(piece.renderer.sprite);
            }
        }

        foreach (Faded faded in fadeAlong)
        {
            if (faded.renderer != null)
            {
                faded.renderer.enabled = false;
            }
        }
    }

    // Czy kawałek tekstury jest przezroczysty (wtedy nie ma po co go rozsypywać).
    static bool IsEmpty(Texture2D texture, Rect part)
    {
        int x = Mathf.FloorToInt(part.x);
        int y = Mathf.FloorToInt(part.y);
        int width = Mathf.Max(1, Mathf.FloorToInt(part.width));
        int height = Mathf.Max(1, Mathf.FloorToInt(part.height));

        foreach (Color pixel in texture.GetPixels(x, y, width, height))
        {
            if (pixel.a > 0.04f)
            {
                return false;
            }
        }

        return true;
    }
}
