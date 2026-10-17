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

    [Header("Szybkość (power-up)")]
    public float speedStep = 0.4f;
    public int speedUpgradeLimit = 5;
    [Tooltip("Ile stopni można stracić przez przedmioty negatywne.")]
    public int speedDowngradeLimit = 2;

    int speedUpgrades;

    void Awake()
    {
        // moduł przedmiotów dokładamy sam, żeby nie trzeba było nic dodawać w inspektorze
        if (GetComponent<PlayerPickups>() == null)
        {
            gameObject.AddComponent<PlayerPickups>();
        }
    }

    // Wartości ujemne zmniejszają (przedmioty negatywne). Bomby i zasięg nie spadną poniżej 1.
    // Każda metoda zwraca faktyczną zmianę po limitach, żeby efekt czasowy dało się dokładnie cofnąć.
    public int AddSpeed(int steps = 1)
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();

        if (movement == null)
        {
            return 0;
        }

        int target = Mathf.Clamp(speedUpgrades + steps, -speedDowngradeLimit, speedUpgradeLimit);
        int applied = target - speedUpgrades;

        movement.baseSpeed += applied * speedStep;
        speedUpgrades = target;
        return applied;
    }

    public int AddBomb(int amount = 1)
    {
        int before = maxBombs;
        maxBombs = Mathf.Clamp(maxBombs + amount, 1, maxBombsLimit);
        return maxBombs - before;
    }

    public int AddRange(int amount = 1)
    {
        int before = blastRange;
        blastRange = Mathf.Clamp(blastRange + amount, 1, blastRangeLimit);
        return blastRange - before;
    }
}
