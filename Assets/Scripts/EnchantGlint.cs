using UnityEngine;

// Połysk "zaklęcia" na postaci w grze (jak w Minecrafcie): druga warstwa z tym samym sprite'em i shaderem BombGame/EnchantGlint,
// po której przesuwają się pasy światła. Warstwa sama podąża za sprite'em postaci (kierunek, kołysanie, rozmiar).
public class EnchantGlint : MonoBehaviour
{
    public const string ShaderName = "BombGame/EnchantGlint";

    SpriteRenderer body;
    SpriteRenderer glint;
    Material material;
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

        material.SetColor("_GlintColor", look.glintColor);
        material.SetFloat("_Rainbow", look.glintRainbow ? 1f : 0f);
        Follow();
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
    }

    void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }
}
