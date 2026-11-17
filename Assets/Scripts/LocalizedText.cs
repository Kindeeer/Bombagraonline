using TMPro;
using UnityEngine;

// Napis, który sam zmienia treść po zmianie języka.
public class LocalizedText : MonoBehaviour
{
    [SerializeField] string key;

    TMP_Text text;

    public void SetKey(string newKey)
    {
        key = newKey;
        Refresh();
    }

    void OnEnable()
    {
        Loc.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        Loc.Changed -= Refresh;
    }

    void Refresh()
    {
        if (text == null)
        {
            text = GetComponent<TMP_Text>();
        }

        if (text != null && !string.IsNullOrEmpty(key))
        {
            text.text = Loc.T(key);
        }
    }
}
