using UnityEngine;

// Wygląd jednego zwierzątka (skin). Utwórz: Create > Bomb Game > Player Skin.
// Skiny przypisuje się do slotów w MatchManager (Slot Skins). W przyszłości każdy gracz wybierze własny,
// a ta sama klasa posłuży do sklepu i zapisów. Wymiary grafik są w przewodniku dla grafika.
[CreateAssetMenu(fileName = "Skin_", menuName = "Bomb Game/Player Skin", order = 1)]
public class PlayerSkin : ScriptableObject
{
    public string displayName = "Zwierzak";

    [Header("Postać w grze (jedna grafika na kierunek)")]
    [Tooltip("Zalecane płótno 192 x 256 px (128 px = jedno pole), punkt obrotu (0,5 ; 0,25) w środku pola, na stopach.")]
    public Sprite front;
    public Sprite back;
    [Tooltip("Lewa strona. Puste = gra odbija grafikę prawej strony, więc wystarczą trzy rysunki.")]
    public Sprite left;
    public Sprite right;

    [Header("Karta gracza w HUD")]
    [Tooltip("Buźka zwierzaka, zalecane 256 x 256 px. Puste = biała buźka zastępcza w kolorze slotu.")]
    public Sprite portrait;
    [Tooltip("Buźka po śmierci (np. oczy X). Puste = buźka zastępcza.")]
    public Sprite portraitDead;

    [Header("Barwa")]
    [Tooltip("Dodatkowe zabarwienie postaci w grze. Biały = bez zmian.")]
    public Color tint = Color.white;
}
