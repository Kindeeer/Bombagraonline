using System.Collections.Generic;
using UnityEngine;

// Kasuje dane gracza zapisane na urządzeniu. ResetAll: postęp (monety, chromy także założone i odłamki, znaczki "nowe",
// poziom konta i XP, statystyki, ikonka profilu, misje dzienne). ResetSettings: ustawienia (język, głośność, nick, wybrany skin,
// zasady i zapisy poczekalni, liczba botów, wybrana skrzynka).
// Używa tego przełącznik w edytorze: Bomb Game > Zachowaj dane gracza między uruchomieniami Play (odznaczony = każdy Play od zera).
public static class ProgressReset
{
    // skinLabels: nazwy wszystkich skinów (z GameArt), bo część zapisów ma nazwę skina w kluczu.
    public static void ResetAll(IEnumerable<string> skinLabels)
    {
        List<string> labels = skinLabels != null ? new List<string>(skinLabels) : new List<string>();

        PlayerWallet.ResetAll(labels);
        ChromaShards.ResetAll();
        NewChromas.ResetAll();
        PlayerProgress.ResetAll(labels);
        PlayerAvatar.ResetAll();
        DailyMissions.ResetAll();
        PlayerPrefs.Save();
    }

    // Klucze ustawień (nazwy jak w klasach, które je zapisują). Po dodaniu nowego ustawienia dopisz tu jego klucz.
    static readonly string[] SettingKeys =
    {
        "language",                                               // Loc
        "mute_sound", "mute_music", "volume_sfx", "volume_music", // GameAudio
        "nick",                                                   // PlayerProfile
        "selected_skin",                                          // SkinSelection
        "lobby_last", "lobby_presets",                            // LobbyState, LobbyPresets
        "bot_count",                                              // MainMenu
        "chest_selected"                                          // Chests
    };

    // Usuwa ustawienia. Zapamiętane wartości w pamięci gry czyszczą się same przy starcie Play (ResetStatics w każdej klasie).
    public static void ResetSettings()
    {
        foreach (string key in SettingKeys)
        {
            PlayerPrefs.DeleteKey(key);
        }

        PlayerPrefs.Save();
    }
}
