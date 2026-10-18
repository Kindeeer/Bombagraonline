using UnityEngine;

// Zestaw grafik jednego motywu (np. ciasteczkowy, lodowy). Utwórz: prawy klik w Project > Create > Bomb Game > Art Theme.
// Motywy wpisuje się na listę w obiekcie GameArt, a gra wybiera jeden z nich (losowo co rundę albo wskazany numer).
// Puste pole w motywie = gra bierze grafikę z obiektu GameArt (motyw domyślny). Gracze, emotki, ikony przedmiotów
// i interfejs są wspólne dla wszystkich motywów i zostają w GameArt.
[CreateAssetMenu(fileName = "Theme_", menuName = "Bomb Game/Art Theme", order = 3)]
public class ArtTheme : ScriptableObject
{
    public string displayName = "Motyw";

    [Header("Tło za planszą")]
    [Tooltip("Zaznacz, żeby ten motyw zmieniał kolor tła kamery.")]
    public bool overrideCameraColor;
    public Color cameraColor = new Color(0.47f, 0.2f, 0.33f);

    [Header("Plansza: kafelki (po kilka wariantów, gra losuje wariant dla każdego pola)")]
    [Tooltip("Podłoga 128 x 128 px (HD 256 x 256). Dwa warianty układają się w szachownicę.")]
    public Sprite[] floorVariants;
    [Tooltip("Ramka areny 128 x 192 px (HD 256 x 384).")]
    public Sprite[] borderVariants;
    [Tooltip("Ściana twarda z układów, nie do zniszczenia.")]
    public Sprite[] pillarVariants;
    [Tooltip("Zwykły blok, ginie od jednego wybuchu.")]
    public Sprite[] cookieVariants;
    [Tooltip("Gruba czekolada, dwa wybuchy.")]
    public Sprite[] heavyCookieVariants;
    public Sprite[] honeyBlockVariants;
    public Sprite[] sodaBlockVariants;
    public Sprite[] honeyPuddleVariants;
    public Sprite[] sodaPuddleVariants;

    [Header("Fontanna")]
    public Sprite fountainBody;
    public Sprite fountainStream;
    public Sprite fountainDrop;
    public Sprite fountainRipple;

    [Header("Bomba i ogień")]
    public Sprite bomb;
    public Sprite flame;

    [Header("Bąbelki przedmiotów")]
    public Sprite pickupBubble;
    public Sprite pickupShadow;

    // Warianty grafiki dla rodzaju kafelka (TileType.Empty = podłoga).
    public Sprite[] TileVariants(TileType type)
    {
        switch (type)
        {
            case TileType.Empty: return floorVariants;
            case TileType.SolidWall: return borderVariants;
            case TileType.Pillar: return pillarVariants;
            case TileType.Cookie: return cookieVariants;
            case TileType.HeavyCookie: return heavyCookieVariants;
            case TileType.HoneyBlock: return honeyBlockVariants;
            case TileType.SodaBlock: return sodaBlockVariants;
            case TileType.HoneyPuddle: return honeyPuddleVariants;
            case TileType.SodaPuddle: return sodaPuddleVariants;
            default: return null;
        }
    }
}
