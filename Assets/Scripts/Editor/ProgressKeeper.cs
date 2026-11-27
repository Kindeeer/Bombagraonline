using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Zerowanie danych gracza przy wciśnięciu Play w edytorze. Gdy w menu Bomb Game > Zachowaj dane gracza... NIE ma ptaszka,
// tuż przed startem gry kasujemy postęp (monety, chromy i odłamki, poziom i XP, statystyki, ikonka profilu, misje dzienne)
// i ustawienia (język, głośność, nick, wybrany skin, zasady poczekalni). Robimy to jeszcze w trybie edycji, bo wtedy scena jest
// wczytana i znamy nazwy skinów z GameArt. Ten plik działa tylko w edytorze.
[InitializeOnLoad]
public static class ProgressKeeper
{
    const string ResetFlag = "BombaGra.ProgressWasReset";

    static ProgressKeeper()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
        {
            bool keep = EditorPrefs.GetBool(PlayerWallet.KeepProgressPref, false);
            SessionState.SetBool(ResetFlag, !keep);

            if (!keep)
            {
                ProgressReset.ResetAll(SkinLabels());
                ProgressReset.ResetSettings();
            }
        }
        else if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ResetFlag, false))
        {
            // komunikat dopiero po starcie gry, bo Console często czyści się przy wciśnięciu Play
            SessionState.SetBool(ResetFlag, false);
            Debug.Log("Bomb Game: dane gracza wyzerowane (monety, chromy i odłamki, poziom, statystyki, misje, język, głośność, nick, "
                + "skin, zasady poczekalni). Żeby je zachować, zaznacz w menu Bomb Game > Zachowaj dane gracza między uruchomieniami Play.");
        }
    }

    static IEnumerable<string> SkinLabels()
    {
        List<string> labels = new List<string>();
        GameArt art = Object.FindAnyObjectByType<GameArt>(FindObjectsInactive.Include);

        if (art != null && art.players != null)
        {
            foreach (PlayerSprites skin in art.players)
            {
                if (skin != null && !string.IsNullOrEmpty(skin.label))
                {
                    labels.Add(skin.label);
                }
            }
        }

        return labels;
    }
}
