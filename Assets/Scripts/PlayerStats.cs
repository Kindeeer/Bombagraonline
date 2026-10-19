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
    [Tooltip("Najkrótszy i najdłuższy lont bomby (sekundy) po przedmiotach zmieniających lont.")]
    public float minFuseTime = 1f;
    public float maxFuseTime = 5f;

    [Header("Szybkość (buty)")]
    [Tooltip("Prędkość chodzenia bez żadnych butów (pola na sekundę). To jest wartość startowa każdej postaci.")]
    public float startSpeed = 1.7f;
    [Tooltip("O ile każdy but zwiększa prędkość (pola na sekundę). Buty się kumulują.")]
    public float bootBonus = 0.5f;
    [Tooltip("Najwolniejsza możliwa prędkość (po klątwach).")]
    public float minSpeed = 1f;
    [Tooltip("Ile butów można mieć naraz.")]
    public int speedUpgradeLimit = 5;
    [Tooltip("Ile stopni można stracić przez przedmioty negatywne.")]
    public int speedDowngradeLimit = 2;
    [Tooltip("Mnożnik prędkości (używają go boty, żeby chodziły szybciej albo wolniej od gracza).")]
    public float speedMultiplier = 1f;

    int speedUpgrades;
    PlayerMovement movement;

    // Ile butów ma postać teraz (z butami czasowymi, np. Turbo). Używa tego HUD.
    public int SpeedLevel => speedUpgrades;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();

        // moduł przedmiotów dokładamy sam, żeby nie trzeba było nic dodawać w inspektorze
        if (GetComponent<PlayerPickups>() == null)
        {
            gameObject.AddComponent<PlayerPickups>();
        }

        // prędkość startowa jest ustawiona tutaj, a nie w PlayerMovement, więc strojenie robi się w jednym miejscu
        RefreshSpeed();
    }

    // Przelicza prędkość chodzenia z liczby butów i mnożnika.
    public void RefreshSpeed()
    {
        if (movement == null)
        {
            movement = GetComponent<PlayerMovement>();
        }

        if (movement != null)
        {
            movement.baseSpeed = Mathf.Max(minSpeed, startSpeed + speedUpgrades * bootBonus) * speedMultiplier;
        }
    }

    // Wartości ujemne zmniejszają (przedmioty negatywne). Bomby i zasięg nie spadną poniżej 1.
    // Każda metoda zwraca faktyczną zmianę po limitach, żeby efekt czasowy dało się dokładnie cofnąć.
    public int AddSpeed(int steps = 1)
    {
        int target = Mathf.Clamp(speedUpgrades + steps, -speedDowngradeLimit, speedUpgradeLimit);
        int applied = target - speedUpgrades;

        speedUpgrades = target;
        RefreshSpeed();
        return applied;
    }

    // Zmienia lont bomby w sekundach w granicach limitów. Zwraca faktyczną zmianę, żeby efekt czasowy dało się cofnąć.
    public float AddFuse(float delta)
    {
        if (Mathf.Approximately(delta, 0f))
        {
            return 0f;
        }

        float before = fuseTime;
        fuseTime = Mathf.Clamp(fuseTime + delta, minFuseTime, maxFuseTime);
        return fuseTime - before;
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
