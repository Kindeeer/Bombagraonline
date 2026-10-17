using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Jednorazowe ustawienie sceny (Tools > Bomb Game > Set Up Scene):
// - zapisuje obrazki zastępcze jako pliki PNG w Assets/Sprites/UI i Assets/Sprites/Pickups (istniejących nie nadpisuje),
// - przypisuje ikony przedmiotom, które nie mają własnej,
// - dodaje fontannę do sceny,
// - buduje Canvas HUD w Hierarchii i zapisuje go jako Assets/Prefabs/HUD.prefab.
// Potem wszystko zmieniasz już w edytorze. Ten plik działa tylko w edytorze i nie trafia do gry.
public static class SceneSetup
{
    const string UiFolder = "Assets/Sprites/UI";
    const string PickupFolder = "Assets/Sprites/Pickups";
    const string PrefabFolder = "Assets/Prefabs";
    const string PrefabPath = PrefabFolder + "/HUD.prefab";
    const float SidePanelWidth = 0.2f;

    static readonly Color CardColor = new Color(0.97f, 0.84f, 0.68f);
    static readonly Color BoxColor = new Color(0.78f, 0.86f, 1f);
    static readonly Color Ink = new Color(0.15f, 0.12f, 0.15f);
    static readonly Color BadgeColor = new Color(0.85f, 0.15f, 0.15f);

    static Font font;
    static Sprite panelSprite;
    static Sprite keySprite;

    [MenuItem("Tools/Bomb Game/Set Up Scene")]
    static void SetUpScene()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        AssignPickupIcons();
        CreateFountain();
        CreateMatchManager();
        CreateHud();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Bomb Game: scena gotowa. Zapisz ją (Ctrl+S).");
    }

    // ---------- Obrazki ----------

    static Sprite Bake(Sprite sprite, string folder, string fileName)
    {
        string path = folder + "/" + fileName + ".png";

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, sprite.texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = sprite.pixelsPerUnit;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            settings.spriteBorder = sprite.border;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite PickupIconFile(PickupIcon icon)
    {
        return Bake(PickupArt.Icon(icon), PickupFolder, icon.ToString());
    }

    static void AssignPickupIcons()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:PickupDefinition"))
        {
            PickupDefinition definition = AssetDatabase.LoadAssetAtPath<PickupDefinition>(AssetDatabase.GUIDToAssetPath(guid));

            if (definition == null || definition.icon != null || definition.placeholderIcon == PickupIcon.None)
            {
                continue;
            }

            Undo.RecordObject(definition, "Assign Pickup Icon");
            definition.icon = PickupIconFile(definition.placeholderIcon);
            EditorUtility.SetDirty(definition);
        }

        AssetDatabase.SaveAssets();
    }

    // ---------- Fontanna ----------

    static void CreateFountain()
    {
        if (Object.FindAnyObjectByType<ChocolateFountain>() != null)
        {
            return;
        }

        GameObject fountain = new GameObject("ChocolateFountain");
        fountain.AddComponent<ChocolateFountain>();
        Undo.RegisterCreatedObjectUndo(fountain, "Create Fountain");
    }
    // ---------- Boty i punkty ----------

    static void CreateMatchManager()
    {
        if (Object.FindAnyObjectByType<MatchManager>() != null)
        {
            return;
        }

        GameObject manager = new GameObject("MatchManager");
        manager.AddComponent<MatchManager>();
        Undo.RegisterCreatedObjectUndo(manager, "Create MatchManager");
    }

    // ---------- HUD ----------

    static void CreateHud()
    {
        GameHud existing = Object.FindAnyObjectByType<GameHud>();

        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("Bomb Game: HUD już jest w scenie. Usuń go, jeśli chcesz zbudować od nowa.");
            return;
        }

        panelSprite = Bake(PickupArt.Panel(), UiFolder, "Panel");
        keySprite = Bake(PickupArt.KeyCap(), UiFolder, "KeyCap");

        GameObject root = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        GameHud hud = root.AddComponent<GameHud>();
        hud.faceAlive = Bake(PickupArt.Face(false), UiFolder, "Face");
        hud.faceDead = Bake(PickupArt.Face(true), UiFolder, "FaceDead");
        hud.bombIcon = PickupIconFile(PickupIcon.Bomb);

        hud.safeArea = CreateRect("SafeArea", root.transform, Vector2.zero, Vector2.one);
        hud.leftPanel = CreateRect("Left", hud.safeArea, Vector2.zero, new Vector2(SidePanelWidth, 1f), new Vector2(16f, 16f), new Vector2(-16f, -16f));
        hud.rightPanel = CreateRect("Right", hud.safeArea, new Vector2(1f - SidePanelWidth, 0f), Vector2.one, new Vector2(16f, 16f), new Vector2(-16f, -16f));

        BuildPlayers(hud, CreateRect("Players", hud.leftPanel, new Vector2(0f, 0.42f), Vector2.one));
        BuildControls(CreatePanel("Controls", hud.leftPanel, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.38f), BoxColor));
        BuildBuff(hud, CreatePanel("Buff", hud.rightPanel, new Vector2(0.06f, 0.54f), new Vector2(0.94f, 0.98f), BoxColor));
        BuildAction(hud, CreatePanel("Action", hud.rightPanel, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.46f), BoxColor));

        Undo.RegisterCreatedObjectUndo(root, "Create HUD");

        Directory.CreateDirectory(PrefabFolder);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
        Selection.activeGameObject = root;
    }

    static void BuildPlayers(GameHud hud, RectTransform parent)
    {
        hud.playerSlots = new GameHud.PlayerSlot[4];

        for (int i = 0; i < 4; i++)
        {
            int column = i % 2;
            int row = i / 2;

            Vector2 min = new Vector2(column * 0.5f, 1f - (row + 1) * 0.5f);
            Vector2 max = new Vector2((column + 1) * 0.5f, 1f - row * 0.5f);

            RectTransform card = CreatePanel("Player" + (i + 1), parent, min, max, CardColor, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            CreateText("Number", card, (i + 1).ToString(), 40, Ink, new Vector2(0f, 0.78f), new Vector2(1f, 0.98f));

            GameHud.PlayerSlot slot = new GameHud.PlayerSlot();
            slot.background = card.GetComponent<Image>();
            slot.face = CreateImage("Face", card, hud.faceAlive, hud.playerColors[i], new Vector2(0.14f, 0.18f), new Vector2(0.86f, 0.8f), false);
            slot.label = CreateText("Label", card, "", 26, Ink, new Vector2(0f, 0.02f), new Vector2(1f, 0.2f));
            hud.playerSlots[i] = slot;
        }
    }

    static void BuildControls(RectTransform panel)
    {
        CreateText("Title", panel, "MOVE", 30, Ink, new Vector2(0f, 0.8f), new Vector2(1f, 0.97f));
        CreateKey(panel, "W", new Vector2(0.38f, 0.44f), new Vector2(0.62f, 0.74f));
        CreateKey(panel, "A", new Vector2(0.12f, 0.1f), new Vector2(0.36f, 0.4f));
        CreateKey(panel, "S", new Vector2(0.38f, 0.1f), new Vector2(0.62f, 0.4f));
        CreateKey(panel, "D", new Vector2(0.64f, 0.1f), new Vector2(0.88f, 0.4f));
    }

    static void BuildBuff(GameHud hud, RectTransform panel)
    {
        CreateText("Title", panel, "BUFF", 30, Ink, new Vector2(0f, 0.84f), new Vector2(1f, 0.98f));
        hud.buffEmpty = CreateText("Empty", panel, "?", 120, new Color(Ink.r, Ink.g, Ink.b, 0.35f), new Vector2(0.15f, 0.2f), new Vector2(0.85f, 0.84f)).gameObject;
        hud.buffIcon = CreateImage("Icon", panel, null, Color.white, new Vector2(0.18f, 0.22f), new Vector2(0.82f, 0.82f), false);
        hud.buffIcon.enabled = false;
        hud.buffCount = CreateText("Count", panel, "", 40, Ink, new Vector2(0.55f, 0.2f), new Vector2(0.96f, 0.38f), TextAnchor.MiddleRight);
        hud.buffName = CreateText("Name", panel, "", 26, Ink, new Vector2(0f, 0.03f), new Vector2(1f, 0.2f));
    }

    static void BuildAction(GameHud hud, RectTransform panel)
    {
        hud.actionIcon = CreateImage("Icon", panel, hud.bombIcon, Color.white, new Vector2(0.18f, 0.32f), new Vector2(0.82f, 0.92f), false);
        hud.actionBadge = CreateText("Badge", panel, "", 44, BadgeColor, new Vector2(0.6f, 0.72f), new Vector2(0.96f, 0.94f), TextAnchor.MiddleRight);
        CreateKey(panel, "SPACE", new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.28f));
    }

    static void CreateKey(RectTransform parent, string label, Vector2 anchorMin, Vector2 anchorMax)
    {
        Image key = CreateImage("Key " + label, parent, keySprite, Color.white, anchorMin, anchorMax, true);
        CreateText("Label", key.rectTransform, label, 36, Ink, Vector2.zero, Vector2.one);
    }

    // ---------- Pomocnicze ----------

    static RectTransform CreateRect(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    static Image CreateImage(string objectName, Transform parent, Sprite sprite, Color color, Vector2 anchorMin, Vector2 anchorMax, bool sliced)
    {
        Image image = CreateRect(objectName, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = !sliced;
        image.raycastTarget = false;
        return image;
    }

    static RectTransform CreatePanel(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color,
        Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        Image image = CreateImage(objectName, parent, panelSprite, color, anchorMin, anchorMax, true);
        image.rectTransform.offsetMin = offsetMin;
        image.rectTransform.offsetMax = offsetMax;
        return image.rectTransform;
    }

    static Text CreateText(string objectName, Transform parent, string value, int size, Color color, Vector2 anchorMin, Vector2 anchorMax,
        TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        Text text = CreateRect(objectName, parent, anchorMin, anchorMax).gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = alignment;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 10;
        text.resizeTextMaxSize = size;
        text.raycastTarget = false;
        return text;
    }
}