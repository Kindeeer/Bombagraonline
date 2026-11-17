using UnityEngine;

// Kamera arena rysuje tylko swój prostokąt na ekranie (okno z planszą). Reszta ekranu, której nie przykrywa żaden element
// interfejsu (wąskie paski między ramką a panelami), nie była czyszczona, więc zostawały tam resztki poprzednich klatek:
// napis z menu głównego (litera R) i panel zasad z poczekalni. Ta dodatkowa kamera nic nie rysuje, tylko co klatkę
// zamalowuje cały ekran jednym kolorem pod resztą.
public class BackgroundClear : MonoBehaviour
{
    static readonly Color Fill = new Color(0.40f, 0.20f, 0.13f, 1f);

    Camera cam;
    Camera arenaCamera;

    // Kolor tła to kolor ramki z GameArt, więc szparki między ramką a panelami wyglądają jak część ramki.
    void Update()
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }

        GameArt art = GameArt.Instance;

        if (cam != null && art != null)
        {
            cam.backgroundColor = art.hudFrameColor;

            // kamera areny też czyści tło kolorem ramki (miała czarne), żeby brzeg okna z planszą nie był ciemny
            if (arenaCamera == null)
            {
                ArenaCamera arena = FindAnyObjectByType<ArenaCamera>();
                arenaCamera = arena != null ? arena.GetComponent<Camera>() : null;
            }

            if (arenaCamera != null)
            {
                arenaCamera.backgroundColor = art.hudFrameColor;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindAnyObjectByType<BackgroundClear>() != null)
        {
            return;
        }

        GameObject go = new GameObject("BackgroundClear");
        Camera camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Fill;
        camera.cullingMask = 0;
        camera.orthographic = true;
        camera.depth = -100f;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        go.AddComponent<BackgroundClear>();

        // ma przetrwać przeładowanie sceny (start rundy i kolejne rundy wczytują scenę od nowa)
        DontDestroyOnLoad(go);

        // bez drugiego nasłuchiwacza dźwięku (kamera nie jest widokiem gry)
        AudioListener listener = go.GetComponent<AudioListener>();

        if (listener != null)
        {
            Destroy(listener);
        }
    }
}
