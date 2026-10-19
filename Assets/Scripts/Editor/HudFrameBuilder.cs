using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Buduje w Hierarchii nakładkę HUD: tło paneli bocznych (Backdrop w panelach Left i Right) i ramkę wokół okna z planszą
// (MapFrame w SafeArea) jako zwykłe obiekty UI. Wołane z przycisku w obiekcie GameArt (Narzędzia).
// Sprite zaokrągleń zapisuje jako plik PNG w Assets/Sprites/UI/Generated (istniejącego nie nadpisuje).
// Wygląd (sprite'y, kolory, grubość ramki) ustawia się w obiekcie GameArt, sekcja "Ramka i panele HUD".
// Ten plik działa tylko w edytorze i nie trafia do gry.
public static class HudFrameBuilder
{
    const string ParentFolder = "Assets/Sprites/UI";
    const string FolderName = "Generated";

    internal static void BuildInScene()
    {
        GameHud hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);

        if (hud == null)
        {
            EditorUtility.DisplayDialog("Brak HUD", "W scenie nie ma obiektu HUD (komponent GameHud).", "OK");
            return;
        }

        Sprite rounded = EnsureRoundedSprite();

        // obiekty w scenie mogą wskazywać tylko sprite'y zapisane jako pliki
        UiKit.SetSpriteOverrides(rounded, null, null);

        try
        {
            Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Zbuduj ramkę HUD");
            hud.BuildFrameObjects();
        }
        finally
        {
            UiKit.SetSpriteOverrides(null, null, null);
        }

        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        Selection.activeGameObject = hud.gameObject;

        Debug.Log("Bomb Game: ramka HUD zbudowana w Hierarchii (Backdrop w panelach Left i Right, MapFrame w SafeArea). Zapisz scenę (Ctrl+S). " +
            "Kolory, sprite'y i grubość ustawiasz w obiekcie GameArt, sekcja Ramka i panele HUD.");
    }

    static Sprite EnsureRoundedSprite()
    {
        string folder = ParentFolder + "/" + FolderName;

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(ParentFolder, FolderName);
        }

        string path = folder + "/UI_Rounded.png";
        bool created = !File.Exists(path);

        if (created)
        {
            Texture2D texture = UiKit.CreateRoundedTexture();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = new Vector4(UiKit.RoundedRadius, UiKit.RoundedRadius, UiKit.RoundedRadius, UiKit.RoundedRadius);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
