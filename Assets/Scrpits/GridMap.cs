using UnityEngine;

public class GridMap : MonoBehaviour
{
    public Sprite squareSprite;
    public Color floorColor = Color.white;
    public Color wallColor = Color.gray;

    public const int Size = 15;
    public static GridMap Instance;

    bool[,] walls = new bool[Size, Size];
    bool[,] bombs = new bool[Size, Size];
    int[,] fire = new int[Size, Size];

    void Awake()
    {
        Instance = this;
        BuildMap();
    }

    void BuildMap()
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                bool isBorder = x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
                bool isPillar = x % 2 == 0 && y % 2 == 0;
                walls[x, y] = isBorder || isPillar;

                GameObject tile = new GameObject("Tile " + x + " " + y);
                tile.transform.parent = transform;
                tile.transform.position = new Vector3(x, y, 0);

                SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite = squareSprite;
                sr.color = walls[x, y] ? wallColor : floorColor;
            }
        }
    }

    bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < Size && cell.y < Size;
    }

    public bool IsWall(Vector2Int cell)
    {
        if (!InBounds(cell))
        {
            return true;
        }
        return walls[cell.x, cell.y];
    }

    public bool CanEnter(Vector2Int cell)
    {
        return !IsWall(cell) && !bombs[cell.x, cell.y];
    }

    public void SetBomb(Vector2Int cell, bool hasBomb)
    {
        bombs[cell.x, cell.y] = hasBomb;
    }

    public void AddFire(Vector2Int cell)
    {
        fire[cell.x, cell.y]++;
    }

    public void RemoveFire(Vector2Int cell)
    {
        fire[cell.x, cell.y]--;
    }

    public bool IsOnFire(Vector2Int cell)
    {
        if (!InBounds(cell))
        {
            return false;
        }
        return fire[cell.x, cell.y] > 0;
    }
}
