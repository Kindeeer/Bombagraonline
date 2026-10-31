using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Inspektor obiektu GameArt: pola na wszystkie grafiki plus przyciski narzędzi (zamiast menu Tools).
// Ten plik działa tylko w edytorze i nie trafia do gry.
[CustomEditor(typeof(GameArt))]
public class GameArtEditor : Editor
{
    bool showTools;
    string newThemeName = "Ciasteczka";

    // Prawy klik w Hierarchii > Bomb Game > Game Art: tworzy obiekt z grafikami (tylko jeden na scenę).
    [MenuItem("GameObject/Bomb Game/Game Art (wszystkie grafiki)", false, 20)]
    static void CreateGameArt()
    {
        GameArt existing = Object.FindAnyObjectByType<GameArt>();

        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("Bomb Game: obiekt GameArt już jest w scenie.");
            return;
        }

        GameObject go = new GameObject("GameArt", typeof(GameArt));
        Undo.RegisterCreatedObjectUndo(go, "Create Game Art");
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Bomb Game: obiekt GameArt utworzony. Zapisz scenę (Ctrl+S).");
    }

    public override void OnInspectorGUI()
    {
        GameArt art = (GameArt)target;

        EditorGUILayout.HelpBox(
            "Jedno miejsce na wszystkie grafiki. Przeciągaj sprite'y na pola poniżej. Puste pole = gra używa grafiki zastępczej, " +
            "którą sama rysuje. Kafelki z sekcji Plansza są wpisywane też w pliki w Assets/Tiles.",
            MessageType.Info);

        EditorGUILayout.HelpBox(
            "Skiny postaci są na samej górze w liście Players (każdy element to jeden zwierzak: cztery kierunki i dwie buźki). " +
            "Nowy skin dodasz przyciskiem + pod listą Players i wpisaniem jego grafik.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        newThemeName = EditorGUILayout.TextField("Nazwa motywu", newThemeName);

        if (GUILayout.Button("Utwórz motyw z obecnych grafik", GUILayout.Width(210f)))
        {
            art.CreateThemeFromCurrent(string.IsNullOrWhiteSpace(newThemeName) ? "Motyw" : newThemeName.Trim());
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        DrawDefaultInspector();
        EditorGUILayout.Space();

        showTools = EditorGUILayout.Foldout(showTools, "Narzędzia (import grafik, HUD)", true);

        if (!showTools)
        {
            return;
        }

        EditorGUILayout.LabelField("Grafiki", EditorStyles.boldLabel);

        if (GUILayout.Button("Wyostrz import sprite'ów (bez kompresji, mipmapy dla 2x)"))
        {
            SpriteImportTool.Sharpen();
        }

        EditorGUILayout.LabelField("HUD", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Tworzy w Hierarchii nakładkę HUD jak w Bomb It: tło paneli bocznych i ramkę wokół okna z planszą (zwykłe obiekty Canvas). " +
            "Kamera w grze rysuje wtedy tylko okno między panelami, więc plansza nie prześwituje pod przyciskami. " +
            "Zrób to raz i zapisz scenę (Ctrl+S). Wygląd ustawiasz w sekcji Ramka i panele HUD powyżej.",
            MessageType.None);

        if (GUILayout.Button("Zbuduj ramkę HUD w scenie (Canvas w Hierarchii)"))
        {
            HudFrameBuilder.BuildInScene();
        }


    }
}
