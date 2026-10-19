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
    public Sprite[] honeyBlockVariants;
    public Sprite[] sodaBlockVariants;
    [Tooltip("Płaska kałuża po miodzie (128 x 128 px).")]
    public Sprite[] honeyPuddleVariants;
    public Sprite[] sodaPuddleVariants;

    // Stare pojedyncze pola z pierwszej wersji GameArt. Zostają ukryte, żeby nie zgubić wcześniej wpisanych grafik:
    // przy pierwszej edycji trafiają na listy powyżej.
    [HideInInspector] public Sprite floor;
    [HideInInspector] public Sprite border;
    [HideInInspector] public Sprite pillar;
    [HideInInspector] public Sprite cookie;
    [HideInInspector] public Sprite heavyCookie;
    [HideInInspector] public Sprite honeyBlock;
    [HideInInspector] public Sprite sodaBlock;
    [HideInInspector] public Sprite honeyPuddle;
    [HideInInspector] public Sprite sodaPuddle;

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
        return index >= 0 ? players[index] : null;
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

        // boty: kolejne skiny z pominięciem wybranego (przy 4 skinach i wyborze pierwszego: bot 1 = skin 2, bot 2 = skin 3...)
        int other = (slot - 1) % (count - 1);
        return other < selected ? other : other + 1;
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

        MigrateLegacyFields();
        EditorApplication.delayCall += ApplyToTiles;
    }

    // Przenosi grafiki wpisane w starej wersji (pojedyncze pola) na listy wariantów.
    void MigrateLegacyFields()
    {
        floorVariants = Migrated(floorVariants, ref floor);
        borderVariants = Migrated(borderVariants, ref border);
        pillarVariants = Migrated(pillarVariants, ref pillar);
        cookieVariants = Migrated(cookieVariants, ref cookie);
        heavyCookieVariants = Migrated(heavyCookieVariants, ref heavyCookie);
        honeyBlockVariants = Migrated(honeyBlockVariants, ref honeyBlock);
        sodaBlockVariants = Migrated(sodaBlockVariants, ref sodaBlock);
        honeyPuddleVariants = Migrated(honeyPuddleVariants, ref honeyPuddle);
        sodaPuddleVariants = Migrated(sodaPuddleVariants, ref sodaPuddle);
    }

    static Sprite[] Migrated(Sprite[] list, ref Sprite legacy)
    {
        if (legacy == null)
        {
            return list;
        }

        Sprite old = legacy;
        legacy = null;
        return list == null || list.Length == 0 ? new[] { old } : list;
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

    // Uzupełnia puste pola tym, co gra już teraz używa (kafelki, bomba, ogień, ikony, buźki, gracz z P1).
    public void FillEmptyFromProject()
    {
        Undo.RecordObject(this, "Fill Game Art");

        MigrateLegacyFields();

        floorVariants = FilledList(floorVariants, "Tile_Ground");
        borderVariants = FilledList(borderVariants, "Tile_Border");
        pillarVariants = FilledList(pillarVariants, "Tile_Pillar");
        cookieVariants = FilledList(cookieVariants, "Tile_Cookie");
        heavyCookieVariants = FilledList(heavyCookieVariants, "Tile_HeavyCookie");
        honeyBlockVariants = FilledList(honeyBlockVariants, "Tile_Honey");
        sodaBlockVariants = FilledList(sodaBlockVariants, "Tile_Soda");
        honeyPuddleVariants = FilledList(honeyPuddleVariants, "Tile_Honey_Puddle");
        sodaPuddleVariants = FilledList(sodaPuddleVariants, "Tile_Soda_Puddle");

        bomb = bomb != null ? bomb : PrefabSprite("Assets/Prefabs/Bomb_0.prefab");
        flame = flame != null ? flame : PrefabSprite("Assets/Prefabs/Flame.prefab");

        uiPanel = uiPanel != null ? uiPanel : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Panel.png");
        uiKeyCap = uiKeyCap != null ? uiKeyCap : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/KeyCap.png");
        uiFace = uiFace != null ? uiFace : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Face.png");
        uiFaceDead = uiFaceDead != null ? uiFaceDead : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/FaceDead.png");

        // ikony przedmiotów z istniejących definicji
        foreach (string guid in AssetDatabase.FindAssets("t:PickupDefinition"))
        {
            PickupDefinition definition = AssetDatabase.LoadAssetAtPath<PickupDefinition>(AssetDatabase.GUIDToAssetPath(guid));

            if (definition == null || definition.icon == null)
            {
                continue;
            }

            switch (definition.placeholderIcon)
            {
                case PickupIcon.Bomb: iconBomb = iconBomb != null ? iconBomb : definition.icon; break;
                case PickupIcon.Range: iconRange = iconRange != null ? iconRange : definition.icon; break;
                case PickupIcon.Speed: iconSpeed = iconSpeed != null ? iconSpeed : definition.icon; break;
                case PickupIcon.Hammer: iconHammer = iconHammer != null ? iconHammer : definition.icon; break;
                case PickupIcon.Rocket: iconRocket = iconRocket != null ? iconRocket : definition.icon; break;
                case PickupIcon.Skull: iconSkull = iconSkull != null ? iconSkull : definition.icon; break;
            }
        }

        // postać i cień z gracza w scenie
        PlayerVisual visual = FindAnyObjectByType<PlayerVisual>();

        if (visual != null && players != null && players.Length > 0 && players[0] != null)
        {
            PlayerSprites p1 = players[0];
            p1.front = p1.front != null ? p1.front : visual.front;
            p1.back = p1.back != null ? p1.back : visual.back;
            p1.left = p1.left != null ? p1.left : visual.left;
            p1.right = p1.right != null ? p1.right : visual.right;

            if (playerShadow == null)
            {
                foreach (SpriteRenderer renderer in visual.transform.root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.name == "Shadow" && renderer.sprite != null)
                    {
                        playerShadow = renderer.sprite;
                        break;
                    }
                }
            }
        }

        EditorUtility.SetDirty(this);
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

    // Pusta lista dostaje jeden wariant z pliku kafelka, niepusta zostaje bez zmian.
    static Sprite[] FilledList(Sprite[] list, string tileName)
    {
        if (list != null && list.Length > 0)
        {
            return list;
        }

        Sprite sprite = TileSprite(tileName);
        return sprite != null ? new[] { sprite } : list;
    }

    static Sprite TileSprite(string tileName)
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(TilesFolder + tileName + ".asset");
        return tile != null ? tile.sprite : null;
    }

    static Sprite PrefabSprite(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        SpriteRenderer renderer = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;
        return renderer != null ? renderer.sprite : null;
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
}
