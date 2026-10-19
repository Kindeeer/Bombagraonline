using System.Collections.Generic;
using UnityEngine;

// Zdarzenia dźwiękowe gry. Kod gry woła tylko GameAudio.Play(AudioEvent.X), a to, jaki dźwięk zagra,
// ustala dźwiękowiec w FMOD Studio (event o ścieżce z tabeli Paths poniżej).
public enum AudioEvent
{
    UiClick,
    UiBack,
    BombPlace,
    BombExplode,
    BombKick,
    BlockBreak,
    PickupCollect,
    ShieldBreak,
    Teleport,
    PlayerDeath,
    RoundReady,
    RoundGo,
    RoundWin,
    RoundDraw,
    Emote,
    NukeFall,
    NukeExplode,
    ChaosStart
}

public enum MusicTrack
{
    None,
    Menu,
    Game
}

// Warstwa audio pod FMOD.
//
// JAK TO DZIAŁA
// - Bez FMOD w projekcie nic nie gra, a zdarzenia można podejrzeć w Console (DebugMenu > Cheaty > Loguj zdarzenia audio).
// - Po zaimportowaniu FMOD for Unity edytor sam dopisze symbol FMOD_PRESENT (skrypt FmodDetector) i ten sam kod zacznie
//   grać zdarzenia z FMOD Studio. Nic nie trzeba przepisywać w kodzie gry.
//
// UMOWA Z DŹWIĘKOWCEM (nazwy w FMOD Studio)
// - Magistrale: bus:/SFX (efekty) i bus:/Music (muzyka). Przyciski Mute Sound i Mute Music wyciszają właśnie je.
// - Zdarzenia efektów: event:/SFX/<nazwa z enum AudioEvent>, np. event:/SFX/BombExplode (pełna lista w tabeli Paths).
// - Muzyka: event:/Music/Menu i event:/Music/Game (jedna instancja naraz, zmiana utworu zatrzymuje poprzedni).
// - Parametry globalne ustawia SetParameter(nazwa, wartość).
// Ścieżki zmienisz w jednym miejscu: tabela Paths i MusicPaths poniżej.
public static class GameAudio
{
    public const string SfxBus = "bus:/SFX";
    public const string MusicBus = "bus:/Music";

    static readonly Dictionary<AudioEvent, string> Paths = new Dictionary<AudioEvent, string>
    {
        { AudioEvent.UiClick, "event:/SFX/UiClick" },
        { AudioEvent.UiBack, "event:/SFX/UiBack" },
        { AudioEvent.BombPlace, "event:/SFX/BombPlace" },
        { AudioEvent.BombExplode, "event:/SFX/BombExplode" },
        { AudioEvent.BombKick, "event:/SFX/BombKick" },
        { AudioEvent.BlockBreak, "event:/SFX/BlockBreak" },
        { AudioEvent.PickupCollect, "event:/SFX/PickupCollect" },
        { AudioEvent.ShieldBreak, "event:/SFX/ShieldBreak" },
        { AudioEvent.Teleport, "event:/SFX/Teleport" },
        { AudioEvent.PlayerDeath, "event:/SFX/PlayerDeath" },
        { AudioEvent.RoundReady, "event:/SFX/RoundReady" },
        { AudioEvent.RoundGo, "event:/SFX/RoundGo" },
        { AudioEvent.RoundWin, "event:/SFX/RoundWin" },
        { AudioEvent.RoundDraw, "event:/SFX/RoundDraw" },
        { AudioEvent.Emote, "event:/SFX/Emote" },
        { AudioEvent.NukeFall, "event:/SFX/NukeFall" },
        { AudioEvent.NukeExplode, "event:/SFX/NukeExplode" },
        { AudioEvent.ChaosStart, "event:/SFX/ChaosStart" }
    };

    static readonly Dictionary<MusicTrack, string> MusicPaths = new Dictionary<MusicTrack, string>
    {
        { MusicTrack.Menu, "event:/Music/Menu" },
        { MusicTrack.Game, "event:/Music/Game" }
    };

    const string MuteSoundKey = "mute_sound";
    const string MuteMusicKey = "mute_music";

    static int muteSound = -1;
    static int muteMusic = -1;
#pragma warning disable 0414 // bez FMOD pole jest tylko zapisywane
    static bool mutesApplied;
#pragma warning restore 0414
    static MusicTrack currentMusic = MusicTrack.None;

    // Wypisuje każde zdarzenie w Console (do podpinania eventów w FMOD). Domyślnie wyłączone, żeby nie śmiecić.
    public static bool LogEvents { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        muteSound = -1;
        muteMusic = -1;
        mutesApplied = false;
        currentMusic = MusicTrack.None;
        LogEvents = false;
#if FMOD_PRESENT
        musicInstanceValid = false;
#endif
    }

    // ---------- Ustawienia (zapamiętywane na telefonie) ----------

    public static bool MuteSound
    {
        get
        {
            if (muteSound < 0)
            {
                muteSound = PlayerPrefs.GetInt(MuteSoundKey, 0);
            }

            return muteSound == 1;
        }
        set
        {
            muteSound = value ? 1 : 0;
            PlayerPrefs.SetInt(MuteSoundKey, muteSound);
            mutesApplied = false;
            ApplyMutes();
        }
    }

    public static bool MuteMusic
    {
        get
        {
            if (muteMusic < 0)
            {
                muteMusic = PlayerPrefs.GetInt(MuteMusicKey, 0);
            }

            return muteMusic == 1;
        }
        set
        {
            muteMusic = value ? 1 : 0;
            PlayerPrefs.SetInt(MuteMusicKey, muteMusic);
            mutesApplied = false;
            ApplyMutes();
        }
    }

    // Ścieżka zdarzenia FMOD dla danego zdarzenia gry (do podglądu i testów).
    public static string PathOf(AudioEvent audioEvent)
    {
        return Paths.TryGetValue(audioEvent, out string path) ? path : "";
    }

    // ---------- Odtwarzanie ----------

    public static void Play(AudioEvent audioEvent)
    {
        Play(audioEvent, Vector3.zero);
    }

    // position to miejsce w świecie gry (dla dźwięku przestrzennego w FMOD).
    public static void Play(AudioEvent audioEvent, Vector3 position)
    {
        string path = PathOf(audioEvent);

        if (LogEvents)
        {
            Debug.Log("[Audio] " + audioEvent + "  ->  " + path);
        }

        if (MuteSound || path.Length == 0)
        {
            return;
        }

        PlayOneShot(path, position);
    }

    public static void PlayMusic(MusicTrack track)
    {
        if (track == currentMusic)
        {
            return;
        }

        currentMusic = track;

        if (LogEvents)
        {
            Debug.Log("[Audio] Muzyka: " + track);
        }

        StartMusic(track);
    }

    public static void SetParameter(string name, float value)
    {
        if (LogEvents)
        {
            Debug.Log("[Audio] Parametr " + name + " = " + value);
        }

#if FMOD_PRESENT
        try
        {
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName(name, value);
        }
        catch (System.Exception exception)
        {
            WarnOnce("parametr " + name, exception);
        }
#endif
    }

    // ---------- FMOD ----------

#if FMOD_PRESENT
    static FMOD.Studio.EventInstance musicInstance;
    static bool musicInstanceValid;
    static readonly HashSet<string> warned = new HashSet<string>();

    static void PlayOneShot(string path, Vector3 position)
    {
        EnsureMutes();

        try
        {
            FMODUnity.RuntimeManager.PlayOneShot(path, position);
        }
        catch (System.Exception exception)
        {
            WarnOnce(path, exception);
        }
    }

    static void StartMusic(MusicTrack track)
    {
        try
        {
            if (musicInstanceValid)
            {
                musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                musicInstance.release();
                musicInstanceValid = false;
            }

            if (track == MusicTrack.None || !MusicPaths.TryGetValue(track, out string path))
            {
                return;
            }

            EnsureMutes();
            musicInstance = FMODUnity.RuntimeManager.CreateInstance(path);
            musicInstance.start();
            musicInstanceValid = true;
        }
        catch (System.Exception exception)
        {
            WarnOnce("muzyka " + track, exception);
        }
    }

    static void ApplyMutes()
    {
        try
        {
            if (!FMODUnity.RuntimeManager.HaveAllBanksLoaded)
            {
                return;
            }

            FMODUnity.RuntimeManager.GetBus(SfxBus).setMute(MuteSound);
            FMODUnity.RuntimeManager.GetBus(MusicBus).setMute(MuteMusic);
            mutesApplied = true;
        }
        catch (System.Exception exception)
        {
            WarnOnce("magistrale " + SfxBus + " / " + MusicBus, exception);
        }
    }

    static void EnsureMutes()
    {
        if (!mutesApplied)
        {
            ApplyMutes();
        }
    }

    static void WarnOnce(string what, System.Exception exception)
    {
        if (warned.Add(what))
        {
            Debug.LogWarning("[Audio] FMOD: nie udało się (" + what + "): " + exception.Message);
        }
    }
#else
    static void PlayOneShot(string path, Vector3 position)
    {
        // bez FMOD nic nie gra
    }

    static void StartMusic(MusicTrack track)
    {
        // bez FMOD nic nie gra
    }

    static void ApplyMutes()
    {
        // bez FMOD są tylko zapamiętane ustawienia
    }
#endif
}
