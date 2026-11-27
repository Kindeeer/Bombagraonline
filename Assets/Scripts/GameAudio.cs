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
    ChaosStart,
    CoinGain,
    ShopBuy,
    ShopDenied,
    ChestShake,
    ChestOpen,
    ChestRevealCommon,
    ChestRevealRare,
    ChestRevealEpic,
    ChestRevealLegendary,
    ChromaEquip,
    // nowe zdarzenia: nie mają stałej nazwy w FMOD, grają tylko po podpięciu w AudioManagerze (a bez tego zapasowy dźwięk, jeśli jest)
    RoundLose,
    MatchWin,
    MinePlace,
    MineExplode,
    Freeze,
    CurseHit,
    ResultsShow,
    LevelUp,
    MissionClaim,
    EmoteAngry,
    EmoteLaugh,
    EmoteCry,
    EmoteLike,
    BlockCrack,
    HoneyBreak,
    SodaBreak,
    BuffSpawned,
    FountainSplash,
    CameraZoomIn,
    CameraZoomOut,
    KillCoin,
    PlayClick,
    // przewijanie listy palcem: początek i puszczenie (patrz ScrollSounds)
    ScrollStart,
    ScrollEnd,
    // dźwięk w tle w pętli (fontanna): startuje i kończy go kod przez GameAudio.StartLoop
    FountainIdle
}

// Uchwyt do dźwięku w pętli. Stop() wyłącza go z krótkim wyciszeniem (fade-out z eventu).
public sealed class AudioLoop
{
#if FMOD_PRESENT
    public FMOD.Studio.EventInstance instance;
#endif

    bool stopped;

    public void Stop()
    {
        if (stopped)
        {
            return;
        }

        stopped = true;

#if FMOD_PRESENT
        instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        instance.release();
#endif
    }
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
// - Podpinanie eventów: Assets/Resources/AudioManager.prefab (komponent AudioManager). Lista wszystkich zdarzeń gry z polem
//   Event Reference przy każdym. Puste pole = użyta zostaje nazwa z tabeli Paths poniżej.
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
        { AudioEvent.Emote, "event:/SFX/Emote" },
        { AudioEvent.NukeFall, "event:/SFX/NukeFall" },
        { AudioEvent.NukeExplode, "event:/SFX/NukeExplode" },
        { AudioEvent.ChaosStart, "event:/SFX/ChaosStart" },
        { AudioEvent.CoinGain, "event:/SFX/CoinGain" },
        { AudioEvent.ShopBuy, "event:/SFX/ShopBuy" },
        { AudioEvent.ShopDenied, "event:/SFX/ShopDenied" },
        { AudioEvent.ChestShake, "event:/SFX/ChestShake" },
        { AudioEvent.ChestOpen, "event:/SFX/ChestOpen" },
        { AudioEvent.ChestRevealCommon, "event:/SFX/ChestRevealCommon" },
        { AudioEvent.ChestRevealRare, "event:/SFX/ChestRevealRare" },
        { AudioEvent.ChestRevealEpic, "event:/SFX/ChestRevealEpic" },
        { AudioEvent.ChestRevealLegendary, "event:/SFX/ChestRevealLegendary" },
        { AudioEvent.ChromaEquip, "event:/SFX/ChromaEquip" }
    };

    // Zdarzenia bez własnego eventu w FMOD grają dźwięk zapasowy z tej tabeli, dopóki dźwiękowiec nie podepnie własnego
    // w AudioManagerze. Zdarzenia bez wpisu w obu miejscach są ciche.
    static readonly Dictionary<AudioEvent, AudioEvent> Fallbacks = new Dictionary<AudioEvent, AudioEvent>
    {
        // remis nie wymaga osobnego dźwięku: bez podpiętego RoundDraw gra ten sam sygnał co przy przegranej
        { AudioEvent.RoundDraw, AudioEvent.RoundLose },
        { AudioEvent.MatchWin, AudioEvent.RoundWin },
        { AudioEvent.MinePlace, AudioEvent.BombPlace },
        { AudioEvent.MineExplode, AudioEvent.BombExplode },
        { AudioEvent.MissionClaim, AudioEvent.CoinGain },
        // emotki bez własnego dźwięku grają wspólny Emote
        { AudioEvent.EmoteAngry, AudioEvent.Emote },
        { AudioEvent.EmoteLaugh, AudioEvent.Emote },
        { AudioEvent.EmoteCry, AudioEvent.Emote },
        { AudioEvent.EmoteLike, AudioEvent.Emote },
        // rozbicie grubej czekolady (pierwsze trafienie), miodu i sody grają dźwięk rozbicia ciastka, dopóki nie mają własnych
        { AudioEvent.BlockCrack, AudioEvent.BlockBreak },
        { AudioEvent.HoneyBreak, AudioEvent.BlockBreak },
        { AudioEvent.SodaBreak, AudioEvent.BlockBreak },
        { AudioEvent.KillCoin, AudioEvent.CoinGain },
        // przycisk PLAY w menu głównym gra zwykły klik, dopóki nie ma własnego dźwięku
        { AudioEvent.PlayClick, AudioEvent.UiClick },
        // przewijanie list gra zwykły klik, dopóki nie ma własnych dźwięków
        { AudioEvent.ScrollStart, AudioEvent.UiClick },
        { AudioEvent.ScrollEnd, AudioEvent.UiClick }
    };

    static readonly Dictionary<MusicTrack, string> MusicPaths = new Dictionary<MusicTrack, string>
    {
        { MusicTrack.Menu, "event:/Music/Menu" },
        { MusicTrack.Game, "event:/Music/Game" }
    };

    const string MuteSoundKey = "mute_sound";
    const string MuteMusicKey = "mute_music";
    const string SfxVolumeKey = "volume_sfx";
    const string MusicVolumeKey = "volume_music";

    static float sfxVolume = -1f;
    static float musicVolume = -1f;

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
        sfxVolume = -1f;
        musicVolume = -1f;
        mutesApplied = false;
        currentMusic = MusicTrack.None;
        LogEvents = false;
#if FMOD_PRESENT
        musicInstanceValid = false;
        missingPaths.Clear();
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

    // ---------- Głośność (suwaki w ustawieniach, 0-1) ----------

    public static float SfxVolume
    {
        get
        {
            if (sfxVolume < 0f)
            {
                sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
            }

            return sfxVolume;
        }
    }

    public static float MusicVolume
    {
        get
        {
            if (musicVolume < 0f)
            {
                musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
            }

            return musicVolume;
        }
    }

    // Co pokazuje suwak: wyciszenie (dawny przełącznik OFF) to 0.
    public static float SfxVolumeShown => MuteSound ? 0f : SfxVolume;
    public static float MusicVolumeShown => MuteMusic ? 0f : MusicVolume;

    // Suwak efektów. Zero wycisza efekty (gra wtedy w ogóle ich nie odpala), więcej je włącza z daną głośnością.
    public static void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        bool mute = sfxVolume <= 0.001f;

        if (mute != MuteSound)
        {
            MuteSound = mute;
        }
        else
        {
            mutesApplied = false;
            ApplyMutes();
        }
    }

    public static void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
        bool mute = musicVolume <= 0.001f;

        if (mute != MuteMusic)
        {
            MuteMusic = mute;
        }
        else
        {
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
            Debug.Log("[Audio] " + audioEvent + "  ->  " + (path.Length > 0 ? path : "(tylko z AudioManagera)"));
        }

        if (MuteSound)
        {
            return;
        }

        // najpierw event podpięty w AudioManagerze (inspektor), potem nazwa z tabeli Paths, na końcu dźwięk zapasowy
        if (PlayAssigned(audioEvent, position))
        {
            return;
        }

        if (path.Length > 0)
        {
            PlayOneShot(path, position);
        }
        else if (Fallbacks.TryGetValue(audioEvent, out AudioEvent fallback))
        {
            Play(fallback, position);
        }
    }

    // Zebranie przedmiotu: dźwięk podpięty przy tym przedmiocie w AudioManagerze, a gdy go nie ma, wspólny PickupCollect.
    public static void PlayPickupCollect(PickupDefinition item, Vector3 position)
    {
        if (item != null && PlayPickupAssigned(item, false, position))
        {
            return;
        }

        Play(AudioEvent.PickupCollect, position);
    }

    // Użycie przedmiotu specjalnego (młotek, rakieta, mina...). Gra tylko wtedy, gdy dźwiękowiec podpiął dźwięk użycia.
    public static void PlayPickupUse(PickupDefinition item, Vector3 position)
    {
        if (item != null)
        {
            PlayPickupAssigned(item, true, position);
        }
    }

    static bool PlayPickupAssigned(PickupDefinition item, bool use, Vector3 position)
    {
        if (LogEvents)
        {
            Debug.Log("[Audio] Przedmiot " + item.name + (use ? " (użycie)" : " (zebranie)"));
        }

        return !MuteSound && PlayPickupEvent(item, use, position);
    }

    // Dźwięk w tle w pętli (np. fontanna): event w FMOD ma być zapętlony. Zwraca uchwyt do zatrzymania (null, gdy nic nie gra).
    // Gra tylko po podpięciu eventu w AudioManagerze (albo gdy ma ścieżkę w tabeli Paths). Pozycja jest stała.
    public static AudioLoop StartLoop(AudioEvent audioEvent, Vector3 position)
    {
        if (LogEvents)
        {
            Debug.Log("[Audio] Pętla: " + audioEvent);
        }

        return StartLoopInternal(audioEvent, position);
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

    // Stare nazwy eventów (tabela Paths) są tylko zapasem. Gdy ich nie ma w bankach, po cichu je pomijamy, zamiast zasypywać
    // Console ostrzeżeniami przy każdym dźwięku. Prawdziwe problemy (zły event podpięty w AudioManagerze) dalej są zgłaszane.
    static readonly HashSet<string> missingPaths = new HashSet<string>();

    static bool PathExists(string path)
    {
        if (missingPaths.Contains(path))
        {
            return false;
        }

        // banki jeszcze się wczytują: nie wiemy, czy event istnieje, więc na razie nie gramy i niczego nie zapamiętujemy
        if (!FMODUnity.RuntimeManager.HaveAllBanksLoaded)
        {
            return false;
        }

        if (FMODUnity.RuntimeManager.StudioSystem.getEvent(path, out FMOD.Studio.EventDescription description) == FMOD.RESULT.OK)
        {
            return true;
        }

        missingPaths.Add(path);
        return false;
    }

    static void PlayOneShot(string path, Vector3 position)
    {
        EnsureMutes();

        if (!PathExists(path))
        {
            return;
        }

        try
        {
            FMODUnity.RuntimeManager.PlayOneShot(path, position);
        }
        catch (System.Exception exception)
        {
            WarnOnce(path, exception);
        }
    }

    // Gra event podpięty w AudioManagerze. Zwraca false, gdy dla tego zdarzenia nic nie podpięto.
    static AudioLoop StartLoopInternal(AudioEvent audioEvent, Vector3 position)
    {
        try
        {
            EnsureMutes();
            FMOD.Studio.EventInstance instance;

            if (AudioManager.TryGet(audioEvent, out FMODUnity.EventReference reference))
            {
                instance = FMODUnity.RuntimeManager.CreateInstance(reference);
            }
            else if (PathOf(audioEvent).Length > 0 && PathExists(PathOf(audioEvent)))
            {
                instance = FMODUnity.RuntimeManager.CreateInstance(PathOf(audioEvent));
            }
            else
            {
                return null;
            }

            instance.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(position));
            instance.start();
            return new AudioLoop { instance = instance };
        }
        catch (System.Exception exception)
        {
            WarnOnce("pętla " + audioEvent, exception);
            return null;
        }
    }

    static bool PlayPickupEvent(PickupDefinition item, bool use, Vector3 position)
    {
        if (!AudioManager.TryGetPickup(item, use, out FMODUnity.EventReference reference))
        {
            return false;
        }

        EnsureMutes();

        try
        {
            FMODUnity.RuntimeManager.PlayOneShot(reference, position);
        }
        catch (System.Exception exception)
        {
            WarnOnce("przedmiot " + item.name, exception);
        }

        return true;
    }

    static bool PlayAssigned(AudioEvent audioEvent, Vector3 position)
    {
        if (!AudioManager.TryGet(audioEvent, out FMODUnity.EventReference reference))
        {
            return false;
        }

        EnsureMutes();

        try
        {
            FMODUnity.RuntimeManager.PlayOneShot(reference, position);
        }
        catch (System.Exception exception)
        {
            WarnOnce("zdarzenie " + audioEvent, exception);
        }

        return true;
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

            if (track == MusicTrack.None)
            {
                return;
            }

            EnsureMutes();

            if (AudioManager.TryGetMusic(track, out FMODUnity.EventReference assigned))
            {
                musicInstance = FMODUnity.RuntimeManager.CreateInstance(assigned);
            }
            else if (MusicPaths.TryGetValue(track, out string path) && PathExists(path))
            {
                musicInstance = FMODUnity.RuntimeManager.CreateInstance(path);
            }
            else
            {
                return;
            }

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
        }
        catch (System.Exception exception)
        {
            WarnOnce("banki", exception);
            return;
        }

        mutesApplied = true;
        SetBusMute(SfxBus, MuteSound);
        SetBusMute(MusicBus, MuteMusic);
        SetVolume(AudioManager.SfxVcaPath, SfxBus, SfxVolume);
        SetVolume(AudioManager.MusicVcaPath, MusicBus, MusicVolume);
    }

    static void SetBusMute(string bus, bool mute)
    {
        try
        {
            FMODUnity.RuntimeManager.GetBus(bus).setMute(mute);
        }
        catch (System.Exception exception)
        {
            WarnOnce("magistrala " + bus, exception);
        }
    }

    // Głośność przez VCA dźwiękowca, a gdy takiego VCA nie ma w bankach, przez magistralę.
    static void SetVolume(string vca, string bus, float volume)
    {
        try
        {
            FMODUnity.RuntimeManager.GetVCA(vca).setVolume(volume);
            return;
        }
        catch (System.Exception)
        {
            // brak VCA: niżej próbujemy magistrali
        }

        try
        {
            FMODUnity.RuntimeManager.GetBus(bus).setVolume(volume);
        }
        catch (System.Exception exception)
        {
            WarnOnce("głośność " + vca + " / " + bus, exception);
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
    static AudioLoop StartLoopInternal(AudioEvent audioEvent, Vector3 position)
    {
        return null;
    }

    static bool PlayPickupEvent(PickupDefinition item, bool use, Vector3 position)
    {
        return false;
    }

    static bool PlayAssigned(AudioEvent audioEvent, Vector3 position)
    {
        return false;
    }

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
