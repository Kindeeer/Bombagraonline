using UnityEngine;

// Pomoc przy rysowaniu układu planszy. Dodana do korzenia prefabu układu pokazuje w widoku Scene
// obszar areny, ramkę, pola startowe, środek (fontanna) i osie symetrii. W grze służy tylko do tego,
// żeby GridMap wiedział, dla jakiego rozmiaru siatki układ jest narysowany.
// Rysuj tylko w żółtej ćwiartce (lewy górny róg), resztę uzupełni przycisk "Odbij układ" w obiekcie GameArt.
public class MapLayoutGuide : MonoBehaviour
{
    // Starsze prefaby (sprzed tych pól) były rysowane dla siatki 15 x 11.
    public const int LegacyWidth = 15;
    public const int LegacyHeight = 11;

    [Tooltip("Szerokość siatki, dla której narysowano układ (z ramką). 0 = starszy prefab dla 15 x 11.")]
    public int gridWidth;
    [Tooltip("Wysokość siatki, dla której narysowano układ (z ramką).")]
    public int gridHeight;

    // Czy układ pasuje do podanego rozmiaru siatki.
    public bool FitsGrid(int width, int height)
    {
        int w = gridWidth > 0 ? gridWidth : LegacyWidth;
        int h = gridHeight > 0 ? gridHeight : LegacyHeight;
        return w == width && h == height;
    }

    void OnDrawGizmos()
    {
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        int w = GridMap.Width;
        int h = GridMap.Height;

        // żółta ćwiartka: tu rysujesz (x 0..środek, y od środka do góry)
        Area(0, h / 2, w / 2 + 1, h - h / 2, new Color(1f, 0.85f, 0.2f, 0.20f));

        // ramka areny (rysowana przez grę, nie trzeba jej malować)
        Color border = new Color(0.9f, 0.25f, 0.25f, 0.30f);
        Area(0, 0, w, 1, border);
        Area(0, h - 1, w, 1, border);
        Area(0, 1, 1, h - 2, border);
        Area(w - 1, 1, 1, h - 2, border);

        // pola startowe graczy: gra sama zostawia tu wolne miejsce
        foreach (Vector2Int spawn in GridMap.DefaultSpawnPositions())
        {
            Area(spawn.x - 1, spawn.y - 1, 3, 3, new Color(0.2f, 0.85f, 0.35f, 0.30f));
        }

        // wolny pierścień wokół fontanny (bez filarów i bez losowych bloków), a w środku sama fontanna
        Area(w / 2 - 2, h / 2 - 2, 5, 5, new Color(0.9f, 0.3f, 0.7f, 0.12f));
        Area(w / 2 - 1, h / 2 - 1, 3, 3, new Color(0.9f, 0.3f, 0.7f, 0.30f));

        // siatka
        Gizmos.color = new Color(1f, 1f, 1f, 0.16f);
        for (int x = 0; x <= w; x++) Gizmos.DrawLine(new Vector3(x, 0f, 0f), new Vector3(x, h, 0f));
        for (int y = 0; y <= h; y++) Gizmos.DrawLine(new Vector3(0f, y, 0f), new Vector3(w, y, 0f));

        // osie symetrii
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawLine(new Vector3(w * 0.5f, 0f, 0f), new Vector3(w * 0.5f, h, 0f));
        Gizmos.DrawLine(new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h * 0.5f, 0f));

        Gizmos.matrix = previous;
    }

    static void Area(int x, int y, int columns, int rows, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawCube(new Vector3(x + columns * 0.5f, y + rows * 0.5f, 0f), new Vector3(columns, rows, 0.01f));
    }
}
