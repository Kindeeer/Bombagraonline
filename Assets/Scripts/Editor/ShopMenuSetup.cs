using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Raz buduje w scenie strony SKRZYNKI i EDYCJA oraz przycisk CHESTS (zwykłe obiekty Canvas w MainMenuCanvas), żeby były widoczne
// i edytowalne w Hierarchii jeszcze przed wciśnięciem Play. Gra szuka ich po nazwach, więc nazw nie zmieniaj.
// Dodatkowo menu Bomb Game ma przełącznik: czy monety i chromy mają się zerować przy każdym Play w edytorze.
// Ten plik działa tylko w edytorze.
public static class ShopMenuSetup
{
    const string PrefKey = "BombaGra.ShopMenuBuilt.v1";
    const string GeneratedFolder = "Assets/Sprites/UI/Generated/";
    const string KeepMenuPath = "Bomb Game/Zachowuj monety i chromy między uruchomieniami Play";

    [InitializeOnLoadMethod]
    static void AutoRun()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(PrefKey, false) || Application.isPlaying)
            {
                return;
            }

            MainMenu menu = Object.FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include);

            if (menu != null && Build(menu))
            {
                EditorPrefs.SetBool(PrefKey, true);
            }
        };
    }

    [MenuItem(KeepMenuPath)]
    static void ToggleKeep()
    {
        EditorPrefs.SetBool(PlayerWallet.KeepProgressPref, !EditorPrefs.GetBool(PlayerWallet.KeepProgressPref, false));
    }

    [MenuItem(KeepMenuPath, true)]
    static bool ToggleKeepCheck()
    {
        Menu.SetChecked(KeepMenuPath, EditorPrefs.GetBool(PlayerWallet.KeepProgressPref, false));
        return true;
    }

    static bool Build(MainMenu menu)
    {
        Sprite rays = SaveRays();
        Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(GeneratedFolder + "UI_Rounded.png");
        Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>(GeneratedFolder + "UI_Circle.png");

        if (rays == null)
        {
            return false;
        }

        bool sceneWasDirty = EditorSceneManager.GetActiveScene().isDirty;

        // obiekty w scenie nie mogą wskazywać sprite'ów z pamięci, więc budujemy je z plików
        UiKit.SetSpriteOverrides(rounded, circle, null);
        MainMenu.SetShopSpriteOverrides(rays);

        bool built;

        try
        {
            built = menu.BuildShopInEditor();
        }
        finally
        {
            UiKit.SetSpriteOverrides(null, null, null);
            MainMenu.SetShopSpriteOverrides(null);
        }

        if (!built)
        {
            return false;
        }

        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        if (!sceneWasDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }

        Debug.Log("Bomb Game: strony SKRZYNKI i EDYCJA oraz przycisk CHESTS są w scenie (MainMenuCanvas)."
            + (sceneWasDirty ? " Zapisz scenę (Ctrl+S)." : ""));
        return true;
    }

    // Promienie rysowane w kodzie zapisujemy jako plik PNG.
    static Sprite SaveRays()
    {
        string path = GeneratedFolder + "Shop_Rays.png";

        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
        {
            Texture2D texture = MainMenu.CreateRaysTexture();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
