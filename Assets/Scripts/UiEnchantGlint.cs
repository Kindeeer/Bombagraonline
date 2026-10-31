using UnityEngine;
using UnityEngine.UI;

// Połysk "zaklęcia" na obrazku postaci w interfejsie (podgląd w EDIT, karta po otwarciu skrzynki).
// Nakłada na obrazek drugi obrazek z tym samym sprite'em i shaderem BombGame/EnchantGlint.
[RequireComponent(typeof(Image))]
public class UiEnchantGlint : MonoBehaviour
{
    Image source;
    Image glint;
    Material material;

    public void Configure(ChromaFxLook look)
    {
        source = GetComponent<Image>();
        enabled = look.glint;

        if (!look.glint)
        {
            if (glint != null)
            {
                glint.enabled = false;
            }

            return;
        }

        if (glint == null)
        {
            Shader shader = Shader.Find(EnchantGlint.ShaderName);

            if (shader == null)
            {
                enabled = false;
                return;
            }

            GameObject go = new GameObject("Glint", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            glint = go.GetComponent<Image>();
            glint.raycastTarget = false;
            material = new Material(shader);
            glint.material = material;
        }

        material.SetColor("_GlintColor", look.glintColor);
        material.SetFloat("_Rainbow", look.glintRainbow ? 1f : 0f);
        glint.enabled = true;
        Follow();
    }

    void LateUpdate()
    {
        Follow();
    }

    void Follow()
    {
        if (glint == null || source == null)
        {
            return;
        }

        glint.sprite = source.sprite;
        glint.preserveAspect = source.preserveAspect;
    }

    void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }
}
