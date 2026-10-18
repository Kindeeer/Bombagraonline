using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    public float restartDelay = 2f;
    public SpriteRenderer body;

    [Header("Śmierć: rozsypka w pył")]
    [Tooltip("Na ile kawałków (kolumny x wiersze) rozpada się postać.")]
    [Min(1)] public int dustColumns = 8;
    [Min(1)] public int dustRows = 8;
    [Tooltip("Jak długo postać rozsypuje się od głowy do stóp (sekundy).")]
    public float dustDuration = 0.8f;
    [Tooltip("Jak długo kupka leży po upadku wszystkich kawałków (sekundy).")]
    public float dustHold = 0.6f;
    [Tooltip("Jak długo kupka i cień bledną, zanim znikną (sekundy).")]
    public float dustFade = 0.7f;

    // Tryb debug (menu pod TAB): wybuchy nie szkodzą.
    [HideInInspector] public bool godMode;

    bool isDead = false;
    PlayerPickups pickups;

    public bool IsDead => isDead;

    void Awake()
    {
        if (body == null)
        {
            body = GetComponentInChildren<SpriteRenderer>();
        }
    }

    void Update()
    {
        if (isDead)
        {
            return;
        }

        // Prawidłowe odczytanie kratki ze środka pozycji gracza (np. z 1.5, 1.5 robimy kratkę 1, 1)
        Vector2Int cell = GridMap.Instance.WorldToGrid(transform.position);

        if (GridMap.Instance.IsOnFire(cell))
        {
            if (godMode)
            {
                return;
            }

            // tarcza albo gwiazda chroni przed wybuchem
            if (pickups == null)
            {
                pickups = GetComponent<PlayerPickups>();
            }

            if (pickups != null && pickups.AbsorbHit())
            {
                return;
            }

            Die();
        }
    }

    // Zabija postać od razu (menu debug).
    public void Kill()
    {
        if (!isDead)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        GetComponent<PlayerMovement>().enabled = false;
        GetComponent<BombController>().enabled = false;

        // cień znika razem z kupką pyłu, a nie zostaje po śmierci
        List<SpriteRenderer> shadows = new List<SpriteRenderer>();

        foreach (SpriteRenderer other in GetComponentsInChildren<SpriteRenderer>())
        {
            if (other != body && other.name == "Shadow")
            {
                shadows.Add(other);
            }
        }

        // postać rozsypuje się od góry w kupkę pyłu, a gdy nie ma sprite'a, zostaje stary sygnał (czerwony kolor)
        if (DeathDust.Play(body, dustColumns, dustRows, dustDuration, shadows.ToArray(), dustHold, dustFade) != null)
        {
            body.enabled = false;
        }
        else
        {
            body.color = Color.red;
        }

        // gdy w scenie jest MatchManager, to on liczy punkty i restartuje rundę
        if (MatchManager.Instance == null)
        {
            Invoke(nameof(Restart), restartDelay);
        }
    }

    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}