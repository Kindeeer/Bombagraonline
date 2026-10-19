using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Usuwa z menu głównego w scenie komponenty z brakującym skryptem (zostały po przeniesieniu klasy ButtonFx do osobnego pliku).
// Skrypt MainMenu sam dokłada potem nowe efekty przycisków. Działa przy otwarciu sceny i przed wciśnięciem Play,
// ale tylko gdy projekt się skompilował (żeby nie kasować komponentów z powodu błędu kompilacji).
// Ten plik działa tylko w edytorze i nie trafia do gry.
[InitializeOnLoad]
public static class MenuMissingScriptFixer
{
    static MenuMissingScriptFixer()
    {
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += Fix;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                Fix();
            }
        };
        EditorApplication.delayCall += Fix;
    }

    static void Fix()
    {
        if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed || Application.isPlaying)
        {
            return;
        }

        MainMenu menu = Object.FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include);

        if (menu == null)
        {
            return;
        }

        int removed = 0;

        foreach (Transform child in menu.GetComponentsInChildren<Transform>(true))
        {
            // sam obiekt MainMenuCanvas zostaje nietknięty
            if (child != menu.transform)
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            }
        }

        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            Debug.Log("Bomb Game: usunięto " + removed + " uszkodzonych komponentów z menu głównego. Zapisz scenę (Ctrl+S).");
        }
    }
}
