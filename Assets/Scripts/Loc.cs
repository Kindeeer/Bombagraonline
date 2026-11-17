using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public enum Language
{
    Polish,
    English
}

// Tłumaczenia interfejsu. Wszystkie napisy gry są w jednej tabeli poniżej (klucz, polski, angielski), więc dopisanie
// tłumaczenia albo nowego języka to zmiana w jednym miejscu. Kod pobiera napis przez Loc.T("klucz").
//
// Napisy zrobione w Hierarchii (menu, HUD) rozpoznaje Loc.BindTexts po ich treści i dokłada im LocalizedText,
// więc nie trzeba ich ręcznie podpinać. Wybrany język jest zapamiętany na telefonie.
// Aby dodać język: dopisz go do enum Language, do tablicy LanguageCodes oraz kolejną kolumnę w tabeli Table.
public static class Loc
{
    const string Key = "language";

    static readonly string[] LanguageCodes = { "PL", "EN" };

    static int language = -1;

    // Zmienia się przy każdej zmianie języka (HUD używa tego do odświeżenia napisów).
    public static int Version { get; private set; }

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        language = -1;
        Version = 0;
        Changed = null;
        legacy = null;
    }

    public static string CodeOf(Language value)
    {
        return LanguageCodes[(int)value];
    }

    public static Language Current
    {
        get
        {
            if (language < 0)
            {
                // pierwsze uruchomienie: polski telefon dostaje polski, reszta angielski
                int fallback = Application.systemLanguage == SystemLanguage.Polish ? (int)Language.Polish : (int)Language.English;
                language = Mathf.Clamp(PlayerPrefs.GetInt(Key, fallback), 0, LanguageCodes.Length - 1);
            }

            return (Language)language;
        }
        set
        {
            if ((int)value == language)
            {
                return;
            }

            language = (int)value;
            PlayerPrefs.SetInt(Key, language);
            PlayerPrefs.Save();
            Version++;
            Changed?.Invoke();
        }
    }

    // ---------- Tabela: klucz, polski, angielski ----------

    static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
    {
        // menu główne
        { "menu.play", new[] { "GRAJ", "PLAY" } },
        { "menu.skins", new[] { "SKINY", "SKINS" } },
        { "menu.chests", new[] { "SKRZYNKI", "CHESTS" } },
        { "menu.settings", new[] { "USTAWIENIA", "SETTINGS" } },
        { "menu.mode", new[] { "TRYB GRY", "GAME MODE" } },
        { "menu.solo", new[] { "SOLO", "SOLO" } },
        { "menu.multi", new[] { "MULTI", "MULTI" } },
        { "menu.bots", new[] { "ILU GRACZY?", "HOW MANY PLAYERS?" } },
        { "menu.bots1", new[] { "2 GRACZE   (1 bot)", "2 PLAYERS   (1 bot)" } },
        { "menu.bots2", new[] { "3 GRACZE   (2 boty)", "3 PLAYERS   (2 bots)" } },
        { "menu.bots3", new[] { "4 GRACZE   (3 boty)", "4 PLAYERS   (3 bots)" } },
        { "menu.back", new[] { "WSTECZ", "BACK" } },
        { "menu.sound", new[] { "Dźwięk", "Sound" } },
        { "menu.sound.sub", new[] { "Efekty dźwiękowe (OFF = wyciszone)", "Sound effects (OFF = muted)" } },
        { "menu.music", new[] { "Muzyka", "Music" } },
        { "menu.music.sub", new[] { "Muzyka w tle (OFF = wyciszona)", "Background music (OFF = muted)" } },
        { "menu.pause", new[] { "PAUZA", "PAUSE" } },
        { "menu.resume", new[] { "WZNÓW", "RESUME" } },
        { "menu.quit", new[] { "WYJDŹ", "QUIT" } },
        { "menu.edit", new[] { "EDYTUJ", "EDIT" } },
        { "menu.noskins", new[] { "Brak skinów. Dodaj je w obiekcie GameArt (lista Skiny postaci).", "No skins yet. Add them in the GameArt object (Players list)." } },
        { "skin.selected", new[] { "WYBRANY", "SELECTED" } },

        // edycja skina i skrzynki
        { "edit.title", new[] { "EDYCJA", "EDIT SKIN" } },
        { "edit.titleOf", new[] { "EDYCJA: {0}", "EDIT: {0}" } },
        { "edit.default", new[] { "Zwykły", "Default" } },
        { "edit.locked", new[] { "ZABLOKOWANY", "LOCKED" } },
        { "shop.box", new[] { "SKRZYNKA Z CHROMAMI", "CHROMA CHEST" } },
        { "shop.rolls", new[] { "Losuje chromę, której jeszcze nie masz.", "Gives a chroma you do not have yet." } },
        { "shop.collected", new[] { "Zebrane chromy: {0} / {1}", "Chromas collected: {0} / {1}" } },
        { "shop.open", new[] { "OTWÓRZ  {0}", "OPEN  {0}" } },
        { "shop.all", new[] { "MASZ WSZYSTKO", "YOU HAVE THEM ALL" } },
        { "shop.hint", new[] { "Za każdą wygraną rundę dostajesz {0} monet.", "You earn {0} coins for every round you win." } },
        { "chest.equip", new[] { "ZAŁÓŻ", "EQUIP" } },
        { "chest.new", new[] { "NOWA!", "NEW!" } },
        { "rarity.Common", new[] { "ZWYKŁY", "COMMON" } },
        { "rarity.Rare", new[] { "RZADKI", "RARE" } },
        { "rarity.Epic", new[] { "EPICKI", "EPIC" } },
        { "rarity.Legendary", new[] { "LEGENDARNY", "LEGENDARY" } },

        // nazwy skinów i chrom (klucz = etykieta z GameArt)
        { "skin.Bóbr", new[] { "Bóbr", "Beaver" } },
        { "skin.Królik", new[] { "Królik", "Rabbit" } },
        { "skin.Miś", new[] { "Miś", "Bear" } },
        { "skin.Panda", new[] { "Panda", "Panda" } },
        { "skin.Żaba", new[] { "Żaba", "Frog" } },
        { "chroma.Fioletowy", new[] { "Fioletowy", "Purple" } },
        { "chroma.Lodowy", new[] { "Lodowy", "Icy" } },
        { "chroma.Złoty", new[] { "Złoty", "Golden" } },
        { "chroma.Tęczowy", new[] { "Tęczowy", "Rainbow" } },

        // runda i HUD
        { "round.ready", new[] { "GOTOWI?", "READY" } },
        { "round.go", new[] { "START!", "GO!" } },
        { "round.chaos", new[] { "PODWÓJNE BUFFY,\nCHAOS!", "DOUBLE BUFFS,\nCHAOS!" } },
        { "round.draw", new[] { "REMIS", "DRAW" } },
        { "round.wins", new[] { "{0} WYGRYWA RUNDĘ", "{0} WINS THE ROUND" } },
        { "round.coins", new[] { "+{0} MONET", "+{0} COINS" } },
        { "board.title", new[] { "WYNIKI RUNDY", "ROUND RESULTS" } },
        { "board.kills", new[] { "ZABICIA", "KILLS" } },
        { "board.wins", new[] { "WYGRANE", "WINS" } },
        { "board.coins", new[] { "MONETY", "COINS" } },
        { "board.total", new[] { "Masz razem: {0} monet", "You have {0} coins" } },
        { "hud.you", new[] { "TY", "YOU" } },
        { "hud.bot", new[] { "BOT {0}", "BOT {0}" } },
        { "hud.dead", new[] { "MARTWY", "DEAD" } },
        { "hud.buff", new[] { "BUFF", "BUFF" } },
        { "hud.cam", new[] { "CAM", "CAM" } },
        { "hud.emotes", new[] { "EMOTKI", "EMOTES" } },
        { "emote.angry", new[] { "Zły", "Angry" } },
        { "emote.laugh", new[] { "Śmiech", "Laugh" } },
        { "emote.cry", new[] { "Płacz", "Cry" } },
        { "emote.like", new[] { "Super", "Nice" } },
        { "nuke.hint", new[] { "WSKAŻ MIEJSCE NA MAPIE I NACIŚNIJ ATOM!", "PICK A SPOT ON THE MAP AND PRESS ATOM!" } },

        // przedmioty (klucz = nazwa pliku przedmiotu w Assets/Data/Pickups)
        { "pickup.BiggerBlast", new[] { "Większy wybuch", "Bigger Blast" } },
        { "pickup.BombStorm", new[] { "Burza bomb", "Bomb Storm" } },
        { "pickup.CurseAutoBombs", new[] { "Biegunka bomb", "Bomb Trail" } },
        { "pickup.CurseDizzy", new[] { "Zawroty głowy", "Dizzy" } },
        { "pickup.CurseSnail", new[] { "Ślimak", "Snail" } },
        { "pickup.CurseFreeze", new[] { "Zamrożenie", "Freeze" } },
        { "pickup.PierceBomb", new[] { "Bomba przebijająca", "Piercing Bomb" } },
        { "pickup.Mine", new[] { "Mina", "Mine" } },
        { "pickup.Example_Slowdown", new[] { "Spowolnienie", "Slowdown" } },
        { "pickup.ExtraBomb", new[] { "Dodatkowa bomba", "Extra Bomb" } },
        { "pickup.Hammer", new[] { "Młotek", "Hammer" } },
        { "pickup.Kick", new[] { "Kopanie bomb", "Bomb Kick" } },
        { "pickup.MegaBlast", new[] { "Mega wybuch", "Mega Blast" } },
        { "pickup.Nuke", new[] { "Atomówka", "Nuke" } },
        { "pickup.Rocket", new[] { "Rakieta", "Rocket" } },
        { "pickup.Shield", new[] { "Tarcza", "Shield" } },
        { "pickup.ShortFuse", new[] { "Krótki lont", "Short Fuse" } },
        { "pickup.SpeedBoost", new[] { "Buty", "Boots" } },
        { "pickup.Star", new[] { "Gwiazda", "Star" } },
        { "pickup.Teleport", new[] { "Zamiana miejsc", "Swap Places" } },
        { "pickup.Turbo", new[] { "Turbo", "Turbo" } }
    };

    // Starsze teksty z menu zbudowanego w Hierarchii, które nie są w tabeli dosłownie.
    static readonly Dictionary<string, string> ExtraLegacy = new Dictionary<string, string>
    {
        { "Efekty dźwiękowe (OFF = mute sound)", "menu.sound.sub" },
        { "Muzyka w tle (OFF = mute music)", "menu.music.sub" },
        { "SHOP", "menu.chests" },
        { "MULTI (wkrótce)", "menu.multi" }
    };

    // ---------- Pobieranie napisów ----------

    public static string T(string key)
    {
        if (Table.TryGetValue(key, out string[] values))
        {
            int index = (int)Current;
            return index < values.Length && !string.IsNullOrEmpty(values[index]) ? values[index] : values[0];
        }

        return key;
    }

    // Napis wpisany od razu w dwóch językach (dla nowych ekranów, których nie ma jeszcze w tabeli).
    public static string P(string polish, string english)
    {
        return Current == Language.Polish ? polish : english;
    }

    public static string F(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }

    // Tłumaczenie, gdy klucz istnieje, a w przeciwnym razie oryginalny napis (np. własny skin dodany w GameArt).
    public static string TOr(string key, string fallback)
    {
        return Table.ContainsKey(key) ? T(key) : fallback;
    }

    public static string SkinName(string label)
    {
        return TOr("skin." + label, label);
    }

    public static string ChromaName(string label)
    {
        return TOr("chroma." + label, label);
    }

    // Nazwa chromy w wybranym języku (angielska z pola english, a gdy jej brak, polska).
    public static string ChromaName(SkinChroma chroma)
    {
        return Current == Language.English && !string.IsNullOrEmpty(chroma.english) ? chroma.english : chroma.label;
    }

    public static string Pickup(PickupDefinition definition)
    {
        return definition == null ? "" : TOr("pickup." + definition.name, definition.displayName);
    }

    // ---------- Podpinanie napisów ze sceny ----------

    static Dictionary<string, string> legacy;

    static string Normalize(string text)
    {
        return Regex.Replace(text.Trim(), "\\s+", " ");
    }

    // Klucz napisu, jeśli jego treść (w którymkolwiek języku) jest w tabeli.
    public static string KeyForText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (legacy == null)
        {
            legacy = new Dictionary<string, string>();

            foreach (KeyValuePair<string, string[]> entry in Table)
            {
                // tylko napisy z menu i HUD są wyszukiwane po treści (reszta jest ustawiana z kodu)
                if (entry.Key.StartsWith("menu.") || entry.Key.StartsWith("hud.") || entry.Key.StartsWith("emote.")
                    || entry.Key == "edit.title" || entry.Key == "shop.box" || entry.Key == "chest.equip")
                {
                    foreach (string value in entry.Value)
                    {
                        legacy[Normalize(value)] = entry.Key;
                    }
                }
            }

            foreach (KeyValuePair<string, string> entry in ExtraLegacy)
            {
                legacy[Normalize(entry.Key)] = entry.Value;
            }
        }

        return legacy.TryGetValue(Normalize(text), out string key) ? key : null;
    }

    // Dokłada LocalizedText wszystkim napisom pod root, których treść jest w tabeli. Napisy z listy wyjątków (ustawiane
    // z kodu) zostają nietknięte.
    public static void BindTexts(Transform root, params TMP_Text[] except)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponent<LocalizedText>() != null || Array.IndexOf(except, text) >= 0)
            {
                continue;
            }

            string key = KeyForText(text.text);

            if (key != null)
            {
                text.gameObject.AddComponent<LocalizedText>().SetKey(key);
            }
        }
    }
}
