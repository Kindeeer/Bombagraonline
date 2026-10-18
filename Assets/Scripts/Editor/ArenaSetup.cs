using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Malowanie areny w tilemapach: ramka, filary i podłoga dla rozmiaru z GridMap (Width x Height).
// Wołane z przycisku w Inspectorze obiektu GameArt. Ten plik działa tylko w edytorze i nie trafia do gry.
public static class ArenaSetup
{
    const string GroundTilePath = "Assets/Tiles/Tile_Ground.asset";

    // Pyta o potwierdzenie i maluje arenę od nowa.
    internal static void GenerateArena()
    {
        string question = "Wyczyści i namaluje od nowa arenę " + GridMap.Width + " x " + GridMap.Height
            + " (ramka, filary co drugie pole, podłoga) w tilemapach Blocks, Ground i Effects. Zrobi to też z blokami, które namalowałeś. Można cofnąć (Ctrl+Z).";

        if (!EditorUtility.DisplayDialog("Generate Arena", question, "Generuj", "Anuluj"))
        {
            return;
        }

        GenerateArenaCore();
    }

    // Sama praca bez pytania.
    internal static void GenerateArenaCore()
    {
        GridMap map = Object.FindAnyObjectByType<GridMap>();

        if (map == null || map.blocksTilemap == null || map.borderTile == null || map.pillarTile == null)
        {
            Debug.LogError("Bomb Game: w scenie brakuje GridMap z przypisaną Blocks Tilemap oraz kafelkami Border i Pillar.");
            return;
        }

        Tilemap ground = FindTilemap("Ground");
        TileBase groundTile = AssetDatabase.LoadAssetAtPath<TileBase>(GroundTilePath);

        Clear(map.blocksTilemap);
        Clear(map.effectsTilemap);

        bool paintGround = ground != null && groundTile != null;

        if (paintGround)
        {
            Clear(ground);
        }
        else
        {
            Debug.LogWarning("Bomb Game: nie znalazłem tilemapy 'Ground' albo kafelka Tile_Ground, podłogi nie ruszam.");
        }

        for (int y = 0; y < GridMap.Height; y++)
        {
            for (int x = 0; x < GridMap.Width; x++)
            {
                Vector3Int position = new Vector3Int(x, y, 0);
                bool border = x == 0 || y == 0 || x == GridMap.Width - 1 || y == GridMap.Height - 1;
                bool pillar = !border && x % 2 == 0 && y % 2 == 0;

                if (border)
                {
                    map.blocksTilemap.SetTile(position, map.borderTile);
                }
                else if (pillar)
                {
                    map.blocksTilemap.SetTile(position, map.pillarTile);
                }

                if (paintGround)
                {
                    ground.SetTile(position, groundTile);
                }
            }
        }

        Undo.RecordObject(map, "Generate Arena");
        map.spawnPositions = GridMap.DefaultSpawnPositions();
        EditorUtility.SetDirty(map);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Bomb Game: arena " + GridMap.Width + " x " + GridMap.Height + " gotowa. Zapisz scenę (Ctrl+S).");
    }

    static Tilemap FindTilemap(string objectName)
    {
        foreach (Tilemap tilemap in Object.FindObjectsByType<Tilemap>())
        {
            if (tilemap.name == objectName)
            {
                return tilemap;
            }
        }

        return null;
    }

    static void Clear(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return;
        }

        Undo.RegisterCompleteObjectUndo(tilemap, "Generate Arena");
        tilemap.ClearAllTiles();
    }
}
