public enum TileType
{
    // Explicit values preserve the old FloorTile serialization:
    // Green = 0, Red = 1, Completed = 2.
    Objective = 0,
    HazardUnjumpable = 1,
    Default = 2,
    HazardJumpable = 3,
    WarningUnjumpable = 4,
    WarningJumpable = 5,
    Wall = 6,
    Portal = 7,
    DisplayRed = 8,
    DisplayGreen = 9,
    DisplayStart = 10
}
