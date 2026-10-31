using System;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Zestaw grafik jednego gracza (P1-P4): cztery kierunki postaci i buźki na kartę w HUD.
// Puste pole = gra używa swojej domyślnej grafiki. Opcjonalny skin (PlayerSkin) uzupełnia puste pola.
[Serializable]
public class PlayerSprites
{
    public string label = "P";
    [Tooltip("Opcjonalny zestaw (PlayerSkin) uzupełniający puste pola poniżej.")]
    public PlayerSkin skin;

    [Header("Postać w grze (płótno 192 x 256 px, pivot na stopach)")]
    public Sprite front;
    public Sprite back;
    [Tooltip("Puste = gra odbija prawą stronę.")]
    public Sprite left;
    public Sprite right;

    [Header("Karta gracza w HUD (256 x 256 px)")]
    public Sprite portrait;
    public Sprite portraitDead;

    [Tooltip("Dodatkowe zabarwienie postaci. Biały = bez zmian.")]
    public Color tint = Color.white;

    [Header("Chromy (warianty kolorystyczne ze skrzynek). Puste = domyślne cztery: fioletowy, lodowy, złoty, tęczowy")]
    public SkinChroma[] chromas;

    public SkinChroma[] ChromaList => chromas != null && chromas.Length > 0 ? chromas : SkinChroma.Defaults;

    // Efekt wizualny aktualnej chromy (ustawiany przy składaniu wyglądu, nie zapisywany w scenie).
    [NonSerialized] public ChromaFxLook fx;

    public Sprite Front => front != null ? front : skin != null ? skin.front : null;
    public Sprite Back => back != null ? back : skin != null ? skin.back : null;
    public Sprite Left => left != null ? left : skin != null ? skin.left : null;
    public Sprite Right => right != null ? right : skin != null ? skin.right : null;
    public Sprite Portrait => portrait != null ? portrait : skin != null ? skin.portrait : null;
    public Sprite PortraitDead => portraitDead != null ? portraitDead : skin != null ? skin.portraitDead : null;

    // Czy gracz ma choć jedną własną grafikę postaci (wtedy nie dostaje odcienia slotu).
    public bool HasSprites => Front != null || Back != null || Left != null || Right != null;
}

// JEDNO miejsce na wszystkie grafiki gry. Utwórz obiekt w Hierarchii: prawy klik > Bomb Game > Game Art,
// a potem przeciągaj sprite'y na pola poniżej. Puste pole = gra używa grafiki zastępczej, którą sama rysuje.
// Kafelki z sekcji "Plansza" są automatycznie wpisywane też w pliki Assets/Tiles.
[DisallowMultipleComponent]
public class GameArt : MonoBehaviour
{
    static GameArt instance;
    static int lastSearchFrame = -1;

    // Instancja z aktualnej sceny (szukana leniwie, bo kolejność Awake nie jest gwarantowana).
    public static GameArt Instance
    {
        get
        {
            if (instance == null && lastSearchFrame != Time.frameCount)
            {
                lastSearchFrame = Time.frameCount;
                instance = FindAnyObjectByType<GameArt>();
            }

            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        lastSearchFrame = -1;
    }

    [Header("Skiny postaci (menu Skins). Dodaj kolejną postać: powiększ listę i wpisz grafiki nowego elementu")]
    [Tooltip("Każdy element to jeden skin w menu Skins. Pierwsze 4 są domyślne, kolejne można dodawać bez limitu. " +
        "Wybrany skin gra gracz, pozostałe dostają boty po kolei. Pole Label to nazwa pod postacią w menu.")]
    public PlayerSprites[] players = DefaultPlayers();

    [Header("Motywy (opcjonalnie): zestawy grafik planszy, fontanny, bomby i ognia")]
    [Tooltip("Pliki Art Theme (Create > Bomb Game > Art Theme). Pusta lista = używane są grafiki z sekcji poniżej.")]
    public ArtTheme[] themes;
    [Tooltip("-1 = losowy motyw co rundę, 0 = pierwszy z listy, 1 = drugi itd.")]
    public int themeIndex = -1;

    [Header("Plansza: kafelki. Po kilka wariantów każdy, gra losuje wariant osobno dla każdego pola")]
    [Tooltip("Podłoga, 128 x 128 px (w HD 256 x 256). Dwa warianty układają się w szachownicę, więcej jest losowanych.")]
    public Sprite[] floorVariants;
    [Tooltip("Ramka areny, 128 x 192 px (w HD 256 x 384).")]
    public Sprite[] borderVariants;
    [Tooltip("Ściana twarda z układów, nie do zniszczenia.")]
    public Sprite[] pillarVariants;
    [Tooltip("Zwykły blok, ginie od jednego wybuchu.")]
    public Sprite[] cookieVariants;
    [Tooltip("Gruba czekolada, dwa wybuchy. Po pierwszym wybuchu zamienia się w zwykły blok.")]
    public Sprite[] heavyCookieVariants;
    [Tooltip("Gruba czekolada po pierwszym wybuchu: ta sama grafika, ale pęknięta (256 x 384 px, ten sam pivot). Puste = pęknięta wygląda jak zwykły blok.")]
    public Sprite heavyCookieCracked;
    public Sprite[] honeyBlockVariants;
    public Sprite[] sodaBlockVariants;
    [Tooltip("Środek kałuży po miodzie: wypełnia całe pole (256 x 256 px).")]
    public Sprite[] honeyPuddleVariants;
    public Sprite[] sodaPuddleVariants;
    [Tooltip("Brzeg kałuży po miodzie (256 x 256 px). Płaska krawędź (dół grafiki) przylega do reszty kałuży, " +
        "a falista (góra) zostaje na zewnątrz. Gra sama obraca brzeg we właściwą stronę.")]
    public Sprite honeyPuddleSide;
    public Sprite sodaPuddleSide;

    [Header("Fontanna")]
    public Sprite fountainBody;
    [Tooltip("Strumień czekolady, pionowy.")]
    public Sprite fountainStream;
    public Sprite fountainDrop;
    public Sprite fountainRipple;

    [Header("Bomba i ogień")]
    public Sprite bomb;
    [Tooltip("Pole ognia wybuchu (jedna grafika, obracana przez grę).")]
    public Sprite flame;

    [Header("Cienie")]
    public Sprite playerShadow;
    public Sprite pickupShadow;

    [Header("Przedmioty (bąbelki)")]
    public Sprite pickupBubble;
    public Sprite iconBomb;
    public Sprite iconRange;
    public Sprite iconSpeed;
    public Sprite iconHammer;
    public Sprite iconRocket;
    public Sprite iconSkull;
    public Sprite iconShield;
    public Sprite iconStar;
    public Sprite iconBoot;
    public Sprite iconBolt;
    public Sprite iconClock;
    public Sprite iconSwirl;
    public Sprite iconDizzy;
    public Sprite iconMegaBomb;
    public Sprite iconBombStorm;
    public Sprite iconNuke;

    [Header("Emotki (128 x 128 px, w HD 256 x 256)")]
    public Sprite emoteAngry;
    public Sprite emoteLaugh;
    public Sprite emoteCry;
    public Sprite emoteLike;

    [Header("Menu główne (puste pole = kod rysuje prosty zamiennik)")]
    [Tooltip("Tło menu na cały ekran.")]
    public Sprite menuBackground;
    [Tooltip("Logo gry nad przyciskami.")]
    public Sprite menuLogo;
    [Tooltip("Tło przycisków menu (rozciągane, 9-slice). Puste = zaokrąglony prostokąt z kodu.")]
    public Sprite menuButton;
    [Tooltip("Przycisk po najechaniu myszką. Puste = jaśniejsza nakładka z kodu.")]
    public Sprite menuButtonHover;
    [Tooltip("Przycisk po kliknięciu (wciśnięty). Puste = ciemniejsza nakładka z kodu.")]
    public Sprite menuButtonPressed;
    [Tooltip("Ramka karty skina w menu Skins (rozciągana, 9-slice).")]
    public Sprite menuCard;
    [Tooltip("Karta skina po najechaniu myszką. Puste = jaśniejsza nakładka z kodu.")]
    public Sprite menuCardHover;
    [Tooltip("Karta skina po kliknięciu. Puste = ciemniejsza nakładka z kodu.")]
    public Sprite menuCardPressed;
    [Tooltip("Tło wiersza w Settings (Sound, Music), rozciągane (9-slice).")]
    public Sprite menuRow;
    [Tooltip("Tor przełącznika ON/OFF (rozciągany, 9-slice). Gra go barwi: szary = OFF, zielony = ON, więc rysuj go w bieli.")]
    public Sprite menuSwitchTrack;
    [Tooltip("Gałka przełącznika ON/OFF (koło).")]
    public Sprite menuSwitchKnob;
    [Tooltip("Panel pauzy na środku ekranu (rozciągany, 9-slice).")]
    public Sprite menuPausePanel;

    [Header("Interfejs (HUD)")]
    [Tooltip("Tło paneli i przycisków (rozciągane, 9-slice).")]
    public Sprite uiPanel;
    [Tooltip("Podpowiedź klawisza (np. SPACE).")]
    public Sprite uiKeyCap;
    [Tooltip("Zastępcza buźka na karcie gracza (gdy gracz nie ma własnej).")]
    public Sprite uiFace;
    public Sprite uiFaceDead;
    [Tooltip("Korona lidera.")]
    public Sprite uiCrown;
    [Tooltip("Ikona na przycisku bomby.")]
    public Sprite uiBombButton;
    [Header("Ramka i panele HUD (jak nakładka w Bomb It): panele boczne i ramka wokół okna z planszą")]
    [Tooltip("Tło paneli bocznych (rozciągane, 9-slice). Puste = zaokrąglony prostokąt z kodu.")]
    public Sprite hudPanelSprite;
    [Tooltip("Ramka wokół okna z planszą (rozciągana, 9-slice, środek przezroczysty). Puste = prosta ramka z kodu.")]
    public Sprite hudMapFrameSprite;
    [Tooltip("Kolor tła paneli bocznych (przy własnym sprite'cie barwi go, biały = bez zmian).")]
    public Color hudPanelColor = new Color(0.48f, 0.18f, 0.33f, 1f);
    [Tooltip("Kolor ramki wokół planszy (przy własnym sprite'cie barwi go, biały = bez zmian).")]
    public Color hudFrameColor = new Color(0.40f, 0.20f, 0.13f, 1f);
    [Tooltip("Grubość prostej ramki z kodu (jednostki interfejsu). Własny sprite ma swoją grubość.")]
    [Range(8f, 80f)] public float hudFrameThickness = 28f;

    [Header("Interfejs: zębatka i joystick")]
    [Tooltip("Ikona zębatki (ustawienia/pauza) obok CAM i EMOTKI. Puste = zębatka rysowana w kodzie.")]
    public Sprite uiGear;
    public Sprite joystickRing;
    public Sprite joystickKnob;

    static PlayerSprites[] DefaultPlayers()
    {
        PlayerSprites[] result = new PlayerSprites[4];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new PlayerSprites { label = i == 0 ? "P1 (Ty)" : "P" + (i + 1) };
        }

        return result;
    }

    void Awake()
    {
        instance = this;
    }

    void OnEnable()
    {
        instance = this;
    }

    // Grafiki gracza dla danego slotu rundy (0 = Ty, 1-3 = boty). Slot 0 dostaje skin wybrany w menu,
    // boty dostają pozostałe skiny po kolei. Zwraca null, gdy skinów nie ma.
    public PlayerSprites PlayerFor(int slot)
    {
        int index = SkinIndexForSlot(slot);

        if (index < 0)
        {
            return null;
        }

        // gracz (slot 0) gra założoną chromą swojego skina, boty zwykłymi wyglądami
        if (slot == 0)
        {
            return SkinLook(index, PlayerWallet.Equipped(players[index].label));
        }

        return players[index];
    }

    static readonly System.Collections.Generic.Dictionary<(PlayerSprites, SkinChroma), PlayerSprites> chromaLooks =
        new System.Collections.Generic.Dictionary<(PlayerSprites, SkinChroma), PlayerSprites>();

    // Wygląd skina z daną chromą (chroma < 0 = zwykły). Własne grafiki chromy mają pierwszeństwo, resztę przekolorowuje kod.
    public PlayerSprites SkinLook(int skinIndex, int chroma)
    {
        if (skinIndex < 0 || skinIndex >= SkinCount)
        {
            return null;
        }

        PlayerSprites baseLook = players[skinIndex];
        SkinChroma[] list = baseLook.ChromaList;

        if (chroma < 0 || chroma >= list.Length)
        {
            return baseLook;
        }

        SkinChroma c = list[chroma];
        (PlayerSprites, SkinChroma) key = (baseLook, c);

        if (chromaLooks.TryGetValue(key, out PlayerSprites cached) && cached.Front != null)
        {
            return cached;
        }

        PlayerSprites look = new PlayerSprites
        {
            label = baseLook.label + " " + c.label,
            front = c.front != null ? c.front : ChromaBaker.Recolor(baseLook.Front, c),
            back = c.back != null ? c.back : ChromaBaker.Recolor(baseLook.Back, c),
            left = c.left != null ? c.left : ChromaBaker.Recolor(baseLook.Left, c),
            right = c.right != null ? c.right : ChromaBaker.Recolor(baseLook.Right, c),
            portrait = c.portrait != null ? c.portrait : ChromaBaker.Recolor(baseLook.Portrait, c),
            portraitDead = c.portraitDead != null ? c.portraitDead : ChromaBaker.Recolor(baseLook.PortraitDead, c),
            tint = baseLook.tint,
            fx = c.Fx
        };

        chromaLooks[key] = look;
        return look;
    }

    public int SkinCount => players != null ? players.Length : 0;

    // Który skin z listy dostaje dany slot rundy (-1 = brak skinów).
    public int SkinIndexForSlot(int slot)
    {
        int count = SkinCount;

        if (count == 0 || slot < 0)
        {
            return -1;
        }

        int selected = Mathf.Clamp(SkinSelection.Index, 0, count - 1);

        if (slot == 0 || count == 1)
        {
            return selected;
        }

        // boty: losowe zwierzaki, żadnych dwóch takich samych (dopóki starcza skinów) i żaden jak gracz
        if (botSkins == null || botSkinsFor != selected || botSkinsCount != count)
        {
            ShuffleBotSkins();
        }

        return botSkins[(slot - 1) % botSkins.Length];
    }

    int[] botSkins;
    int botSkinsFor = -1;
    int botSkinsCount;

    // Losuje skiny botów na nową rundę: wszystkie skiny poza wybranym przez gracza, w losowej kolejności.
    public void ShuffleBotSkins()
    {
        int count = SkinCount;
        int selected = Mathf.Clamp(SkinSelection.Index, 0, Mathf.Max(count - 1, 0));
        System.Collections.Generic.List<int> pool = new System.Collections.Generic.List<int>();

        for (int i = 0; i < count; i++)
        {
            if (i != selected)
            {
                pool.Add(i);
            }
        }

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        botSkins = pool.Count > 0 ? pool.ToArray() : new[] { selected };
        botSkinsFor = selected;
        botSkinsCount = count;
    }

    public Sprite IconFor(PickupIcon icon)
    {
        switch (icon)
        {
            case PickupIcon.Bomb: return iconBomb;
            case PickupIcon.Range: return iconRange;
            case PickupIcon.Speed: return iconSpeed;
            case PickupIcon.Hammer: return iconHammer;
            case PickupIcon.Rocket: return iconRocket;
            case PickupIcon.Skull: return iconSkull;
            case PickupIcon.Shield: return iconShield;
            case PickupIcon.Star: return iconStar;
            case PickupIcon.Boot: return iconBoot;
            case PickupIcon.Bolt: return iconBolt;
            case PickupIcon.Clock: return iconClock;
            case PickupIcon.Swirl: return iconSwirl;
            case PickupIcon.Dizzy: return iconDizzy;
            case PickupIcon.MegaBomb: return iconMegaBomb;
            case PickupIcon.BombStorm: return iconBombStorm;
            case PickupIcon.Nuke: return iconNuke;
            default: return null;
        }
    }

    // ---------- Motyw ----------

    ArtTheme chosenTheme;
    bool themeChosen;

    // Motyw tej rundy (null, gdy lista motywów jest pusta). Wybierany raz: wskazany numer albo losowy.
    public ArtTheme Theme
    {
        get
        {
            if (!themeChosen)
            {
                themeChosen = true;
                chosenTheme = PickTheme();
            }

            return chosenTheme;
        }
    }

    public bool HasTheme => Theme != null;

    ArtTheme PickTheme()
    {
        if (themes == null)
        {
            return null;
        }

        int count = 0;

        foreach (ArtTheme candidate in themes)
        {
            if (candidate != null)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return null;
        }

        int pick = themeIndex >= 0 && themeIndex < count ? themeIndex : UnityEngine.Random.Range(0, count);

        foreach (ArtTheme candidate in themes)
        {
            if (candidate != null && pick-- == 0)
            {
                return candidate;
            }
        }

        return null;
    }

    void Start()
    {
        // motyw może zmienić kolor tła za planszą
        ArtTheme theme = Application.isPlaying ? Theme : null;

        if (theme != null && theme.overrideCameraColor)
        {
            Camera camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

            if (camera != null)
            {
                camera.backgroundColor = theme.cameraColor;
            }
        }
    }

    static Sprite Themed(Sprite fromTheme, Sprite fallback)
    {
        return fromTheme != null ? fromTheme : fallback;
    }

    // Grafiki zależne od motywu: z motywu rundy, a gdy w motywie pole jest puste, z tego obiektu.
    public Sprite BombSprite { get { ArtTheme t = Theme; return Themed(t != null ? t.bomb : null, bomb); } }
    public Sprite FlameSprite { get { ArtTheme t = Theme; return Themed(t != null ? t.flame : null, flame); } }
    public Sprite PickupBubbleSprite { get { ArtTheme t = Theme; return Themed(t != null ? t.pickupBubble : null, pickupBubble); } }
    public Sprite PickupShadowSprite { get { ArtTheme t = Theme; return Themed(t != null ? t.pickupShadow : null, pickupShadow); } }
    public Sprite FountainBodySprite { get { ArtTheme t = Theme; return Themed(t != null ? t.fountainBody : null, fountainBody); } }
    public Sprite FountainStreamSprite { get { ArtTheme t = Theme; return Themed(t != null ? t.fountainStream : null, fountainStream); } }
    public Sprite FountainDropSprite { get { ArtTheme t = Theme; return Themed(t != null ? t.fountainDrop : null, fountainDrop); } }
    public Sprite FountainRippleSprite { get { ArtTheme t = Theme; return Themed(t != null ? t.fountainRipple : null, fountainRipple); } }

    // Warianty grafiki dla rodzaju kafelka (TileType.Empty = podłoga): z motywu rundy, a gdy w motywie lista jest pusta,
    // z tego obiektu. Może zwrócić pustą listę.
    public Sprite[] TileVariants(TileType type)
    {
        ArtTheme theme = Theme;
        Sprite[] fromTheme = theme != null ? theme.TileVariants(type) : null;

        if (fromTheme != null && fromTheme.Length > 0)
        {
            return fromTheme;
        }

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

    public Sprite HeavyCookieCrackedSprite { get { ArtTheme t = Theme; return Themed(t != null ? t.heavyCookieCracked : null, heavyCookieCracked); } }

    // Brzeg kałuży (obracany przez grę): z motywu rundy, a gdy w motywie pole jest puste, z tego obiektu.
    public Sprite PuddleSideSprite(TileType type)
    {
        ArtTheme t = Theme;

        switch (type)
        {
            case TileType.HoneyPuddle: return Themed(t != null ? t.honeyPuddleSide : null, honeyPuddleSide);
            case TileType.SodaPuddle: return Themed(t != null ? t.sodaPuddleSide : null, sodaPuddleSide);
            default: return null;
        }
    }

    public Sprite EmoteFor(Emote emote)
    {
        switch (emote)
        {
            case Emote.Angry: return emoteAngry;
            case Emote.Laugh: return emoteLaugh;
            case Emote.Cry: return emoteCry;
            default: return emoteLike;
        }
    }

#if UNITY_EDITOR
    const string TilesFolder = "Assets/Tiles/";

    // Po zmianie pola w Inspectorze wpisujemy kafelki w pliki Assets/Tiles (tilemapy i układy używają tych plików).
    void OnValidate()
    {
        // lista skinów ma co najmniej 4 elementy (domyślne postacie), ale może być dłuższa
        if (players == null || players.Length < 4)
        {
            PlayerSprites[] fixedPlayers = DefaultPlayers();

            if (players != null)
            {
                for (int i = 0; i < Mathf.Min(players.Length, 4); i++)
                {
                    fixedPlayers[i] = players[i];
                }
            }

            players = fixedPlayers;
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
            {
                players[i] = new PlayerSprites { label = "Skin " + (i + 1) };
            }
        }

        EditorApplication.delayCall += ApplyToTiles;
    }

    static Sprite First(Sprite[] list)
    {
        return list != null && list.Length > 0 ? list[0] : null;
    }

    // Wpisuje pierwszy wariant z sekcji "Plansza" do plików kafelków. Kolejne warianty gra losuje sama w czasie działania.
    public void ApplyToTiles()
    {
        if (this == null)
        {
            return;
        }

        SetTileSprite("Tile_Ground", First(floorVariants));
        SetTileSprite("Tile_Border", First(borderVariants));
        SetTileSprite("Tile_Pillar", First(pillarVariants));
        SetTileSprite("Tile_Cookie", First(cookieVariants));
        SetTileSprite("Tile_HeavyCookie", First(heavyCookieVariants));
        SetTileSprite("Tile_Honey", First(honeyBlockVariants));
        SetTileSprite("Tile_Soda", First(sodaBlockVariants));
        SetTileSprite("Tile_Honey_Puddle", First(honeyPuddleVariants));
        SetTileSprite("Tile_Soda_Puddle", First(sodaPuddleVariants));
    }

    static void SetTileSprite(string tileName, Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(TilesFolder + tileName + ".asset");

        if (tile == null || tile.sprite == sprite)
        {
            return;
        }

        tile.sprite = sprite;
        EditorUtility.SetDirty(tile);
    }

    static Sprite[] Copy(Sprite[] list)
    {
        return list != null ? (Sprite[])list.Clone() : null;
    }

    // Tworzy plik motywu (Assets/Themes) z grafikami wpisanymi teraz w tym obiekcie i dopisuje go do listy motywów.
    // Dzięki temu dotychczasowy wygląd staje się pierwszym motywem, a drugi dorabia się obok.
    public ArtTheme CreateThemeFromCurrent(string themeName)
    {
        ArtTheme theme = ScriptableObject.CreateInstance<ArtTheme>();
        theme.displayName = themeName;

        theme.floorVariants = Copy(floorVariants);
        theme.borderVariants = Copy(borderVariants);
        theme.pillarVariants = Copy(pillarVariants);
        theme.cookieVariants = Copy(cookieVariants);
        theme.heavyCookieVariants = Copy(heavyCookieVariants);
        theme.honeyBlockVariants = Copy(honeyBlockVariants);
        theme.sodaBlockVariants = Copy(sodaBlockVariants);
        theme.honeyPuddleVariants = Copy(honeyPuddleVariants);
        theme.sodaPuddleVariants = Copy(sodaPuddleVariants);
        theme.heavyCookieCracked = heavyCookieCracked;
        theme.honeyPuddleSide = honeyPuddleSide;
        theme.sodaPuddleSide = sodaPuddleSide;
        theme.fountainBody = fountainBody;
        theme.fountainStream = fountainStream;
        theme.fountainDrop = fountainDrop;
        theme.fountainRipple = fountainRipple;
        theme.bomb = bomb;
        theme.flame = flame;
        theme.pickupBubble = pickupBubble;
        theme.pickupShadow = pickupShadow;

        System.IO.Directory.CreateDirectory("Assets/Themes");
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Themes/Theme_" + themeName + ".asset");
        AssetDatabase.CreateAsset(theme, path);

        Undo.RecordObject(this, "Create Theme");
        int length = themes != null ? themes.Length : 0;
        System.Array.Resize(ref themes, length + 1);
        themes[length] = theme;
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        Debug.Log("Bomb Game: motyw zapisany jako " + path + " i dodany do listy motywów w GameArt.");
        return theme;
    }
#endif
}

// Wybrany przez gracza skin (menu Skins). Zapamiętany na telefonie. Numer to pozycja na liście GameArt > Skiny postaci.
public static class SkinSelection
{
    const string Key = "selected_skin";

    static int cached = -1;

    // Zmienia się przy każdym wyborze, żeby HUD i postacie wiedziały, że trzeba odświeżyć grafiki.
    public static int Version { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        cached = -1;
        Version = 0;
    }

    public static int Index
    {
        get
        {
            if (cached < 0)
            {
                cached = Mathf.Max(0, PlayerPrefs.GetInt(Key, 0));
            }

            return cached;
        }
        set
        {
            cached = Mathf.Max(0, value);
            PlayerPrefs.SetInt(Key, cached);
            Version++;
        }
    }

    // Zmieniło się założenie chromy (wygląd skina), więc HUD i postacie odświeżają grafiki.
    public static void NotifyChanged()
    {
        Version++;
    }
}
