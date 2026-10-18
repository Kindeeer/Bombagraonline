using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Przycisk "Emotki" w HUD i okienko z czterema emotkami. Wybranie emotki wysyła ją jako lokalny gracz,
// a okienko się chowa. Po wysłaniu przez chwilę nie można wysłać następnej (żeby nie spamować).
// Na komputerze emotki można też wysyłać klawiszami 1-4.
public class EmotePicker : MonoBehaviour
{
    [Tooltip("Przycisk, który otwiera i zamyka okienko.")]
    public Button openButton;
    [Tooltip("Okienko z emotkami. Chowa się na starcie.")]
    public GameObject popup;
    [Tooltip("Cztery przyciski emotek w kolejności: Zły, Śmiech, Płacz, Super. Każdy może mieć dziecko 'Icon' (Image).")]
    public Button[] emoteButtons = new Button[Emotes.Count];

    [Tooltip("Minimalny odstęp między emotkami (sekundy).")]
    public float cooldownSeconds = 2.5f;
    [Tooltip("Slot lokalnego gracza (0 = ten, kto trzyma telefon).")]
    public int localSlot = 0;

    float readyAt;

    void Awake()
    {
        if (openButton != null)
        {
            openButton.onClick.AddListener(Toggle);
        }

        for (int i = 0; i < emoteButtons.Length; i++)
        {
            Button button = emoteButtons[i];

            if (button == null)
            {
                continue;
            }

            int index = i;
            button.onClick.AddListener(() => Choose(index));
            SetIcon(button, (Emote)index);
        }

        if (popup != null)
        {
            popup.SetActive(false);
        }
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame) Choose(0);
        if (keyboard.digit2Key.wasPressedThisFrame) Choose(1);
        if (keyboard.digit3Key.wasPressedThisFrame) Choose(2);
        if (keyboard.digit4Key.wasPressedThisFrame) Choose(3);
#endif
    }

    public void Toggle()
    {
        if (popup != null)
        {
            popup.SetActive(!popup.activeSelf);
        }
    }

    public void Choose(int index)
    {
        if (index < 0 || index >= Emotes.Count)
        {
            return;
        }

        if (Time.unscaledTime < readyAt)
        {
            return;
        }

        readyAt = Time.unscaledTime + cooldownSeconds;
        Emotes.Send(localSlot, (Emote)index);

        if (popup != null)
        {
            popup.SetActive(false);
        }
    }

    // Ikona emotki w przycisku: dziecko o nazwie "Icon" dostaje grafikę (własną albo zastępczą).
    static void SetIcon(Button button, Emote emote)
    {
        Transform iconTransform = button.transform.Find("Icon");

        if (iconTransform == null)
        {
            return;
        }

        Image icon = iconTransform.GetComponent<Image>();

        if (icon != null)
        {
            icon.sprite = Emotes.SpriteFor(emote);
            icon.enabled = icon.sprite != null;
        }
    }
}
