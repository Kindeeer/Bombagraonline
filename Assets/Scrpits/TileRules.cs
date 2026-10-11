public static class TileRules
{
    public static bool IsIndestructible(TileType type)
    {
        return type == TileType.SolidWall || type == TileType.Pillar;
    }

    public static bool IsDestructible(TileType type)
    {
        return type == TileType.Cookie
            || type == TileType.HeavyCookie
            || type == TileType.HoneyBlock
            || type == TileType.SodaBlock;
    }
}
