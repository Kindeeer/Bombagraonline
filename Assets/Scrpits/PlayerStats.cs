using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Bomby")]
    public int maxBombs = 1;
    public int blastRange = 2;
    public float fuseTime = 2.5f;

    [Header("Limity (dla power-upów)")]
    public int maxBombsLimit = 6;
    public int blastRangeLimit = 8;

    public void AddBomb()
    {
        maxBombs = Mathf.Min(maxBombs + 1, maxBombsLimit);
    }

    public void AddRange()
    {
        blastRange = Mathf.Min(blastRange + 1, blastRangeLimit);
    }
}
