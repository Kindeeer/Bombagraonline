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

        Vector2Int cell = new Vector2Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.y));

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
