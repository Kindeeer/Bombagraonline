using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Duży napis na środku ekranu na starcie rundy (READY, GO!). Tworzy własny Canvas, więc nie wymaga zmian w HUD.
// Używa go MatchManager: Show("READY", ...) i Show("GO!", ...) jako coroutine.
public class RoundIntro : MonoBehaviour
{
    TextMeshProUGUI label;

    public static RoundIntro Create()
    {
        return new GameObject("RoundIntro").AddComponent<RoundIntro>();
    }

    void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        RectTransform rect = (RectTransform)textObject.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1400f, 360f);

        label = textObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = false;
        label.fontSize = 240f;
        label.raycastTarget = false;
        label.outlineWidth = 0.3f;
        label.outlineColor = new Color32(45, 20, 35, 255);
        label.enabled = false;
    }

    // Pokazuje napis na podany czas: wskakuje z powiększeniem, a pod koniec blednie.
    public IEnumerator Show(string text, Color color, float seconds, float fontSize = 240f)
    {
        label.fontSize = fontSize;
        label.text = text;
        label.color = color;
        label.enabled = true;

        float time = 0f;
        float duration = Mathf.Max(0.1f, seconds);

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / duration);

            float scale = k < 0.15f ? Mathf.Lerp(0.4f, 1.2f, k / 0.15f)
                : k < 0.3f ? Mathf.Lerp(1.2f, 1f, (k - 0.15f) / 0.15f)
                : 1f;

            label.rectTransform.localScale = Vector3.one * scale;
            label.alpha = k > 0.8f ? 1f - (k - 0.8f) / 0.2f : 1f;

            yield return null;
        }

        label.enabled = false;
    }
}
