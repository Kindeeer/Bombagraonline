using UnityEngine;

// Wspólne wejście dotykowe: joystick i przycisk bomby wpisują tu stan, a lokalny gracz czyta go
// razem z klawiaturą. Dzięki temu PlayerMovement i BombController nie muszą wiedzieć, skąd przychodzi sterowanie.
public static class TouchInput
{
    // Jak długo trzymamy dotknięcie przycisku bomby, jeśli nikt go jeszcze nie odebrał (sekundy).
    const float BombBufferSeconds = 0.2f;

    // Kierunek z joysticka: jedna oś naraz (gra chodzi po osiach), zero = brak dotyku.
    public static Vector2Int Move { get; set; }

    static float bombQueuedAt = float.NegativeInfinity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Move = Vector2Int.zero;
        bombQueuedAt = float.NegativeInfinity;
    }

    public static void QueueBomb()
    {
        bombQueuedAt = Time.unscaledTime;
    }

    // Zwraca true raz na każde dotknięcie przycisku bomby.
    public static bool ConsumeBomb()
    {
        if (Time.unscaledTime - bombQueuedAt > BombBufferSeconds)
        {
            return false;
        }

        bombQueuedAt = float.NegativeInfinity;
        return true;
    }
}
