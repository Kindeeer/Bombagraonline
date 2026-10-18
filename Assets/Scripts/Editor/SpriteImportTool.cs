using UnityEditor;
using UnityEngine;

// Ostrość grafik (przycisk "Wyostrz import sprite'ów" w Inspectorze obiektu GameArt).
// Ustawia import wszystkich sprite'ów z Assets/Sprites (bez folderu UI) tak, żeby były jak najostrzejsze:
// - kompresja wyłączona (domyślna kompresja rozmywa krawędzie małych grafik),
// - grafiki w dużej rozdzielczości (od 256 px, czyli rysowane w 2x) dostają mipmapy i filtr trilinear,
//   dzięki czemu po zmniejszeniu na telefonie są gładkie, a nie poszarpane,
// - małe grafiki zostają bez mipmap, bo mipmapy by je tylko zmiękczyły.
// Można uruchamiać wielokrotnie, np. po wrzuceniu nowych grafik. Ten plik działa tylko w edytorze.
public static class SpriteImportTool
{
    const string SpritesFolder = "Assets/Sprites";
    const int MipmapFromSize = 256;

    internal static void Sharpen()
    {
        int changed = 0;
        int checkedCount = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // sprite'y interfejsu mają własne skalowanie, ich nie ruszamy
            if (path.Contains("/UI/"))
            {
                continue;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null || importer.textureType != TextureImporterType.Sprite)
            {
                continue;
            }

            checkedCount++;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool wantMipmaps = texture != null && Mathf.Max(texture.width, texture.height) >= MipmapFromSize;
            FilterMode filter = wantMipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;

            bool needsChange = importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.mipmapEnabled != wantMipmaps
                || importer.filterMode != filter
                || !importer.alphaIsTransparency;

            if (!needsChange)
            {
                continue;
            }

            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = wantMipmaps;
            importer.filterMode = filter;
            importer.alphaIsTransparency = true;

            if (wantMipmaps)
            {
                importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            }

            importer.SaveAndReimport();
            changed++;
        }

        Debug.Log("Bomb Game: sprawdzono " + checkedCount + " sprite'ów, zmieniono import w " + changed + ".");
    }
}
