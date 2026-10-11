using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    public float restartDelay = 2f;

    bool isDead = false;

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
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        GetComponent<PlayerMovement>().enabled = false;
        GetComponent<PlayerBombs>().enabled = false;
        GetComponent<SpriteRenderer>().color = Color.red;
        Invoke("Restart", restartDelay);
    }

    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}