using UnityEngine;
using UnityEngine.UI;

// Wzór chromy na obrazku postaci w interfejsie (podgląd w EDIT, karta po otwarciu skrzynki).
// Nakłada na obrazek drugi obrazek z tym samym sprite'em i shaderem BombGame/EnchantGlint (ikonki zsuwające się po postaci).
[RequireComponent(typeof(Image))]
public class UiEnchantGlint : MonoBehaviour
{
    Image source;
    Image glint;
    Material material;
    Sprite shownSprite;

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

        EnchantGlint.Apply(material, look);
        glint.enabled = true;
        shownSprite = null;
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

        if (source.sprite != shownSprite)
        {
            shownSprite = source.sprite;
            EnchantGlint.ApplySprite(material, shownSprite);
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
