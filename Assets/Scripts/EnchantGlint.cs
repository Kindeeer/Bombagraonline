using UnityEngine;

// Wzór chromy na postaci w grze: druga warstwa z tym samym sprite'em i shaderem BombGame/EnchantGlint, po której zsuwają się
// półprzezroczyste ikonki chromy (listki, płomyki, serduszka...). Warstwa sama podąża za sprite'em postaci (kierunek,
// kołysanie, rozmiar). Kafelek wzoru składa ChromaFx.Pattern z ikonek i kolorów danej chromy.
public class EnchantGlint : MonoBehaviour
{
    public const string ShaderName = "BombGame/EnchantGlint";

    SpriteRenderer body;
    SpriteRenderer glint;
    Material material;
    Sprite shownSprite;
    bool wanted;

    public void Configure(SpriteRenderer bodyRenderer, ChromaFxLook look)
    {
        body = bodyRenderer;
        wanted = look.glint && body != null;
        enabled = wanted;

        if (!wanted)
        {
            if (glint != null)
            {
                glint.enabled = false;
            }

            return;
        }

        if (glint == null)
        {
            Shader shader = Shader.Find(ShaderName);

            if (shader == null)
            {
                Debug.LogWarning("EnchantGlint: nie znaleziono shadera " + ShaderName + " (Assets/Resources/EnchantGlint.shader).");
                enabled = false;
                return;
            }

            GameObject go = new GameObject("Glint");
            go.transform.SetParent(body.transform, false);
            glint = go.AddComponent<SpriteRenderer>();
            material = new Material(shader);
            glint.sharedMaterial = material;
        }

        Apply(material, look);
        shownSprite = null;
        Follow();
    }

    // Wpisuje w materiał wzór danej chromy (wspólne dla postaci w grze i obrazka w interfejsie).
    public static void Apply(Material target, ChromaFxLook look)
    {
        target.SetTexture("_PatternTex", ChromaFx.Pattern(look));
        target.SetFloat("_Rainbow", look.glintRainbow || look.rainbow ? 1f : 0f);
    }

    // Mówi shaderowi, który kawałek tekstury zajmuje sprite i jakie ma proporcje, żeby ikonki nie były rozciągnięte.
    public static void ApplySprite(Material target, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
        {
            return;
        }

        Rect rect;

        try
        {
            rect = sprite.textureRect;
        }
        catch (System.Exception)
        {
            // sprite ciasno upakowany w atlasie: bierzemy całą teksturę
            rect = new Rect(0f, 0f, sprite.texture.width, sprite.texture.height);
        }

        float width = sprite.texture.width;
        float height = sprite.texture.height;

        target.SetVector("_UvRect", new Vector4(rect.x / width, rect.y / height, rect.width / width, rect.height / height));
        target.SetFloat("_Aspect", rect.height > 0f ? rect.width / rect.height : 1f);
    }

    void LateUpdate()
    {
        Follow();
    }

    void Follow()
    {
        if (glint == null || body == null)
        {
            return;
        }

        bool visible = body.enabled && body.sprite != null && body.gameObject.activeInHierarchy && body.color.a > 0.2f;
        glint.enabled = visible;

        if (!visible)
        {
            return;
        }

        glint.sprite = body.sprite;
        glint.flipX = body.flipX;
        glint.sortingLayerID = body.sortingLayerID;
        glint.sortingOrder = body.sortingOrder + 1;
        glint.color = new Color(1f, 1f, 1f, body.color.a);

        if (body.sprite != shownSprite)
        {
            shownSprite = body.sprite;
            ApplySprite(material, shownSprite);
        }
    }

    void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }
}
