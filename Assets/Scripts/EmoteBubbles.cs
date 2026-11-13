using System.Collections.Generic;
using UnityEngine;

// Dymek z emotką nad głową gracza, który ją wysłał (oprócz dymka przy jego karcie w HUD). Dymek wyskakuje, chwilę
// podskakuje nad postacią i znika. Idzie za graczem, więc widać, kto co pokazał, także u cudzych graczy online.
public class EmoteBubbles : MonoBehaviour
{
    class Bubble
    {
        public Transform root;
        public SpriteRenderer background;
        public SpriteRenderer icon;
        public float age;
    }

    const float Life = 2.5f;
    const float Size = 0.95f;
    const float HeadHeight = 1.75f;

    readonly Dictionary<int, Bubble> bubbles = new Dictionary<int, Bubble>();

    void OnEnable()
    {
        Emotes.Sent += OnEmote;
    }

    void OnDisable()
    {
        Emotes.Sent -= OnEmote;
    }

    void OnEmote(int slot, Emote emote)
    {
        // nowa emotka tego samego gracza zastępuje poprzednią
        if (!bubbles.TryGetValue(slot, out Bubble bubble) || bubble.root == null)
        {
            bubble = Build(slot);
            bubbles[slot] = bubble;
        }

        Sprite sprite = Emotes.SpriteFor(emote);
        bubble.icon.sprite = sprite;

        // każda grafika emotki, niezależnie od rozmiaru pliku, mieści się w dymku
        float extent = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 1f;
        bubble.icon.transform.localScale = Vector3.one * (Size * 0.72f / Mathf.Max(0.01f, extent));

        bubble.age = 0f;
        bubble.root.gameObject.SetActive(true);
    }

    Bubble Build(int slot)
    {
        Bubble bubble = new Bubble();
        bubble.root = new GameObject("EmoteBubble_" + slot).transform;
        bubble.root.SetParent(transform, false);

        bubble.background = NewRenderer(bubble.root, "Background", UiKit.CircleSprite(), 20);
        bubble.background.color = new Color(1f, 1f, 1f, 0.95f);

        Sprite circle = bubble.background.sprite;
        float extent = circle != null ? circle.bounds.size.x : 1f;
        bubble.background.transform.localScale = Vector3.one * (Size / Mathf.Max(0.01f, extent));

        bubble.icon = NewRenderer(bubble.root, "Icon", null, 21);
        return bubble;
    }

    static SpriteRenderer NewRenderer(Transform parent, string objectName, Sprite sprite, int order)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    void LateUpdate()
    {
        MatchManager match = MatchManager.Instance;

        foreach (KeyValuePair<int, Bubble> pair in bubbles)
        {
            Bubble bubble = pair.Value;

            if (bubble.root == null || !bubble.root.gameObject.activeSelf)
            {
                continue;
            }

            Combatant combatant = match != null && pair.Key >= 0 && pair.Key < match.Slots.Length ? match.Slots[pair.Key] : null;
            bubble.age += Time.unscaledDeltaTime;

            if (bubble.age >= Life || combatant == null || combatant.health == null || !combatant.IsAlive)
            {
                bubble.root.gameObject.SetActive(false);
                continue;
            }

            float k = bubble.age / Life;

            // wyskok z lekkim przeskokiem, potem delikatne podskakiwanie, na końcu zanikanie
            float pop = k < 0.08f ? Mathf.Lerp(0.2f, 1.2f, k / 0.08f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((k - 0.08f) / 0.08f));
            float bob = Mathf.Sin(bubble.age * 6f) * 0.05f;
            float alpha = k > 0.8f ? 1f - (k - 0.8f) / 0.2f : 1f;

            Vector3 position = combatant.health.transform.position;
            bubble.root.position = new Vector3(position.x, position.y + HeadHeight + bob, 0f);
            bubble.root.localScale = Vector3.one * pop;

            bubble.background.color = new Color(1f, 1f, 1f, 0.95f * alpha);
            bubble.icon.color = new Color(1f, 1f, 1f, alpha);
        }
    }
}
