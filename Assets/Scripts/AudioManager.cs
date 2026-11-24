using System;
using System.Collections.Generic;
using UnityEngine;

// Jeden wiersz listy efektów: zdarzenie z gry i event z FMOD-a, który ma przy nim zagrać.
[Serializable]
public class AudioEventEntry
{
    public AudioEvent id;
#if FMOD_PRESENT
    public FMODUnity.EventReference eventRef;
#endif
}

// Jeden wiersz listy muzyki.
[Serializable]
public class MusicEntry
{
    public MusicTrack track;
#if FMOD_PRESENT
    public FMODUnity.EventReference eventRef;
#endif
}

// Jeden wiersz listy przedmiotów: dźwięk zebrania i dźwięk użycia dla danego przedmiotu (z Assets/Data/Pickups).
[Serializable]
public class PickupSoundEntry
{
    public PickupDefinition item;
#if FMOD_PRESENT
    public FMODUnity.EventReference collect;
    public FMODUnity.EventReference use;
#endif
}

// Menedżer dźwięku. To jest miejsce dla dźwiękowca: w inspektorze widać listę wszystkich zdarzeń gry
// i przy każdym pole Event Reference, w którym wybiera się event z FMOD Studio (bez wpisywania nazw w kodzie).
//
// Gdzie to jest: prefab Assets/Resources/AudioManager.prefab. Gra sama go wczytuje przy starcie i trzyma przez cały czas,
// więc nie trzeba go dokładać do sceny (a scena zostaje do pracy dla reszty zespołu bez konfliktów).
// Pusty wiersz = gra użyje starej nazwy z tabeli Paths w GameAudio.cs, więc dźwięki można podpinać po kolei.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Tooltip("Efekty dźwiękowe: przy każdym zdarzeniu wybierz event z FMOD-a. Lista uzupełnia się sama.")]
    public List<AudioEventEntry> effects = new List<AudioEventEntry>();

    [Tooltip("Muzyka: menu i gra. Event muzyki ma być zapętlony i mieć fade-out.")]
    public List<MusicEntry> music = new List<MusicEntry>();

    [Tooltip("Dźwięki przedmiotów: osobny dźwięk zebrania (każdy przedmiot z bąbelka) i opcjonalnie użycia (młotek, rakieta, mina...). " +
        "Pusty = zagra wspólny PickupCollect.")]
    public List<PickupSoundEntry> pickups = new List<PickupSoundEntry>();

#if FMOD_PRESENT
    readonly Dictionary<AudioEvent, FMODUnity.EventReference> effectLookup = new Dictionary<AudioEvent, FMODUnity.EventReference>();
    readonly Dictionary<MusicTrack, FMODUnity.EventReference> musicLookup = new Dictionary<MusicTrack, FMODUnity.EventReference>();
    readonly Dictionary<PickupDefinition, FMODUnity.EventReference> collectLookup = new Dictionary<PickupDefinition, FMODUnity.EventReference>();
    readonly Dictionary<PickupDefinition, FMODUnity.EventReference> useLookup = new Dictionary<PickupDefinition, FMODUnity.EventReference>();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }

    // Wczytuje prefab z Resources przed pierwszą sceną. Bez prefabu gra korzysta ze starych ścieżek.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>("AudioManager");

        if (prefab != null)
        {
            DontDestroyOnLoad(Instantiate(prefab));
        }
    }

    // FMOD liczy głośność i stereo dźwięków z pozycji na planszy względem "słuchacza" (FMOD Studio Listener). Dokładamy go z kodu
    // do kamery areny, żeby nic nie trzeba było ustawiać w scenie. Słuchacz leży na płaszczyźnie planszy (z = 0), a nie za nią
    // (kamera 2D stoi 10 jednostek dalej), bo inaczej każdy dźwięk byłby z góry "oddalony" o te 10 jednostek.
    public static void AttachListener(Camera camera)
    {
#if FMOD_PRESENT
        if (!Application.isPlaying || camera == null || camera.GetComponentInChildren<FMODUnity.StudioListener>() != null)
        {
            return;
        }

        GameObject listener = new GameObject("FMOD Listener");
        listener.transform.SetParent(camera.transform, false);
        listener.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);
        listener.AddComponent<FMODUnity.StudioListener>();
#endif
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        name = "AudioManager";
        Rebuild();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Rebuild()
    {
#if FMOD_PRESENT
        effectLookup.Clear();
        musicLookup.Clear();
        collectLookup.Clear();
        useLookup.Clear();

        foreach (PickupSoundEntry entry in pickups)
        {
            if (entry.item == null)
            {
                continue;
            }

            if (!entry.collect.IsNull)
            {
                collectLookup[entry.item] = entry.collect;
            }

            if (!entry.use.IsNull)
            {
                useLookup[entry.item] = entry.use;
            }
        }

        foreach (AudioEventEntry entry in effects)
        {
            if (!entry.eventRef.IsNull)
            {
                effectLookup[entry.id] = entry.eventRef;
            }
        }

        foreach (MusicEntry entry in music)
        {
            if (!entry.eventRef.IsNull)
            {
                musicLookup[entry.track] = entry.eventRef;
            }
        }
#endif
    }

#if FMOD_PRESENT
    public static bool TryGet(AudioEvent audioEvent, out FMODUnity.EventReference reference)
    {
        reference = default;
        return Instance != null && Instance.effectLookup.TryGetValue(audioEvent, out reference);
    }

    public static bool TryGetPickup(PickupDefinition item, bool use, out FMODUnity.EventReference reference)
    {
        reference = default;
        return Instance != null && (use ? Instance.useLookup : Instance.collectLookup).TryGetValue(item, out reference);
    }

    public static bool TryGetMusic(MusicTrack track, out FMODUnity.EventReference reference)
    {
        reference = default;
        return Instance != null && Instance.musicLookup.TryGetValue(track, out reference);
    }
#endif

    // Uzupełnia listy o wszystkie zdarzenia z gry (nowe zdarzenia z kodu pojawiają się tu same, a wpisane eventy zostają).
    // Wołane przy każdej zmianie w inspektorze, więc dźwiękowiec nie musi niczego dodawać ręcznie.
    void OnValidate()
    {
        FillMissing();
    }

    [ContextMenu("Uzupełnij listę zdarzeń")]
    void FillMissing()
    {
        foreach (AudioEvent audioEvent in Enum.GetValues(typeof(AudioEvent)))
        {
            if (!effects.Exists(entry => entry.id == audioEvent))
            {
                effects.Add(new AudioEventEntry { id = audioEvent });
            }
        }

        foreach (MusicTrack track in Enum.GetValues(typeof(MusicTrack)))
        {
            if (track != MusicTrack.None && !music.Exists(entry => entry.track == track))
            {
                music.Add(new MusicEntry { track = track });
            }
        }

        // przedmioty biorą się z tabeli PickupTable, więc nowy przedmiot pojawia się tu sam
        PickupTable table = Resources.Load<PickupTable>(PickupTable.ResourcePath);

        if (table != null)
        {
            foreach (PickupDefinition item in table.items)
            {
                if (item != null && !pickups.Exists(entry => entry.item == item))
                {
                    pickups.Add(new PickupSoundEntry { item = item });
                }
            }
        }
    }
}
