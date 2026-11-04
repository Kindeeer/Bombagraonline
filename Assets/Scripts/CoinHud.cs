using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Licznik monet w trakcie rundy (u góry ekranu pośrodku). Pokazuje wszystkie zebrane monety, także te z poprzednich rund,
// i rośnie na bieżąco. Duże liczby są skracane (10K, 1.2M). Ma własny Canvas, więc nie wymaga zmian w HUD ze sceny.
public class CoinHud : MonoBehaviour
{
    TextMeshProUGUI counter;
    RectTransform pill;
    int shown = -1;
    int locVersion = -1;
    float bump;

    void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        GameObject go = new GameObject("CoinPill", typeof(RectTransform), typeof(Image));
        pill = (RectTransform)go.transform;
        pill.SetParent(transform, false);
        pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 1f);
        pill.pivot = new Vector2(0.5f, 1f);
        pill.anchoredPosition = new Vector2(0f, -14f);
        pill.sizeDelta = new Vector2(360f, 64f);

        Image background = go.GetComponent<Image>();
        background.color = new Color(0.20f, 0.07f, 0.15f, 0.85f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Counter", typeof(RectTransform));
        textObject.transform.SetParent(pill, false);

        counter = textObject.AddComponent<TextMeshProUGUI>();
        counter.alignment = TextAlignmentOptions.Center;
        counter.fontStyle = FontStyles.Bold;
        counter.fontSize = 40f;
        counter.color = new Color(1f, 0.85f, 0.25f);
        counter.raycastTarget = false;
        counter.outlineWidth = 0.25f;
        counter.outlineColor = new Color32(45, 20, 35, 255);

        RectTransform rect = counter.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    void Update()
    {
        int coins = PlayerWallet.Coins;

        if (coins != shown || locVersion != Loc.Version)
        {
            // kiedy monet przybywa, licznik na chwilę się powiększa
            if (shown >= 0 && coins > shown)
            {
                bump = 1f;
            }

            shown = coins;
            locVersion = Loc.Version;
            counter.text = Loc.T("board.coins") + ": " + PlayerWallet.Format(coins);
        }

        bump = Mathf.MoveTowards(bump, 0f, Time.unscaledDeltaTime * 3f);
        pill.localScale = Vector3.one * (1f + 0.25f * bump);
    }
}
