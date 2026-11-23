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

#if FMOD_PRESENT
    readonly Dictionary<AudioEvent, FMODUnity.EventReference> effectLookup = new Dictionary<AudioEvent, FMODUnity.EventReference>();
    readonly Dictionary<MusicTrack, FMODUnity.EventReference> musicLookup = new Dictionary<MusicTrack, FMODUnity.EventReference>();
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
    }
}
