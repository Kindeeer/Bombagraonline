using System;
using System.Collections.Generic;
using UnityEngine;

// Wzór misji: który licznik rośnie, do ilu trzeba dobić i co jest nagrodą.
public class MissionTemplate
{
    public string id;
    public PlayerStat stat;
    public int target;
    public int coins;
    public int xp;
    // {0} w tekście to liczba do zrobienia
    public string polish;
    public string english;
    // true = da się zrobić tylko w grze przez internet
    public bool online;

    public string Text => string.Format(Loc.P(polish, english), target);
}

// Jedna z dzisiejszych misji.
public class DailyMission
{
    public MissionTemplate template;
    // wartość licznika w chwili wylosowania misji (postęp liczymy od niej)
    public int baseline;
    public bool claimed;

    public int Progress => Mathf.Clamp(PlayerProgress.Get(template.stat) - baseline, 0, template.target);
    public bool Done => Progress >= template.target;
}

// Misje dzienne: codziennie trzy zadania wylosowane z puli. Postęp bierze się z liczników PlayerProgress (te same, co w profilu),
// więc misje nie potrzebują własnych zaczepów w rozgrywce. Za zrobioną misję odbiera się nagrodę przyciskiem na ekranie MISJE.
// Dzień zmienia się o północy czasu telefonu. Wszystko zapisane na urządzeniu (PlayerPrefs).
public static class DailyMissions
{
    public const int Count = 3;

    const string DayKey = "missions_day";
    const string IdKey = "mission_id_";
    const string BaseKey = "mission_base_";
    const string ClaimedKey = "mission_claimed_";

    // Pula misji. Żeby dodać nową, dopisz wiersz: licznik, cel, monety, XP i dwa teksty.
    static readonly MissionTemplate[] pool =
    {
        New("play3", PlayerStat.RoundsPlayed, 3, 15, 20, "Rozegraj {0} rundy", "Play {0} rounds"),
        New("play6", PlayerStat.RoundsPlayed, 6, 30, 40, "Rozegraj {0} rund", "Play {0} rounds"),
        New("win1", PlayerStat.RoundsWon, 1, 15, 25, "Wygraj {0} rundę", "Win {0} round"),
        New("win3", PlayerStat.RoundsWon, 3, 40, 60, "Wygraj {0} rundy", "Win {0} rounds"),
        New("bots4", PlayerStat.BotKills, 4, 20, 25, "Pokonaj {0} boty", "Defeat {0} bots"),
        New("bots10", PlayerStat.BotKills, 10, 45, 50, "Pokonaj {0} botów", "Defeat {0} bots"),
        New("bombs25", PlayerStat.BombsPlaced, 25, 15, 20, "Postaw {0} bomb", "Place {0} bombs"),
        New("bombs60", PlayerStat.BombsPlaced, 60, 30, 40, "Postaw {0} bomb", "Place {0} bombs"),
        New("items8", PlayerStat.PickupsCollected, 8, 15, 20, "Zbierz {0} przedmiotów", "Collect {0} items"),
        New("items20", PlayerStat.PickupsCollected, 20, 35, 45, "Zbierz {0} przedmiotów", "Collect {0} items"),
        New("coins25", PlayerStat.CoinsEarned, 25, 15, 20, "Zdobądź {0} monet", "Earn {0} coins"),
        New("kills2", PlayerStat.PlayerKills, 2, 30, 40, "Zabij {0} graczy online", "Kill {0} players online", true),
        New("kills5", PlayerStat.PlayerKills, 5, 60, 80, "Zabij {0} graczy online", "Kill {0} players online", true),
        New("online3", PlayerStat.OnlineRounds, 3, 30, 40, "Rozegraj {0} rundy online", "Play {0} online rounds", true)
    };

    static DailyMission[] today;
    static int loadedDay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        today = null;
        loadedDay = 0;
    }

    static MissionTemplate New(string id, PlayerStat stat, int target, int coins, int xp, string polish, string english, bool online = false)
    {
        return new MissionTemplate { id = id, stat = stat, target = target, coins = coins, xp = xp, polish = polish, english = english, online = online };
    }

    // Dzisiejszy dzień jako liczba RRRRMMDD (czas telefonu).
    static int DayNumber
    {
        get
        {
            DateTime now = DateTime.Now;
            return now.Year * 10000 + now.Month * 100 + now.Day;
        }
    }

    // Ile zostało do północy, czyli do nowych misji.
    public static TimeSpan TimeToReset => DateTime.Today.AddDays(1) - DateTime.Now;

    public static DailyMission[] Today
    {
        get
        {
            int day = DayNumber;

            if (today == null || loadedDay != day)
            {
                today = PlayerPrefs.GetInt(DayKey, 0) == day ? Load() : null;

                if (today == null)
                {
                    today = Roll(day);
                    Save(day);
                }

                loadedDay = day;
            }

            return today;
        }
    }

    // Ile misji czeka na odebranie nagrody.
    public static int ClaimableCount
    {
        get
        {
            int count = 0;

            foreach (DailyMission mission in Today)
            {
                if (mission.Done && !mission.claimed)
                {
                    count++;
                }
            }

            return count;
        }
    }

    // Ile misji jest już zrobionych (odebranych albo nie).
    public static int DoneCount
    {
        get
        {
            int count = 0;

            foreach (DailyMission mission in Today)
            {
                if (mission.Done)
                {
                    count++;
                }
            }

            return count;
        }
    }

    // Odbiera nagrodę za zrobioną misję. Zwraca false, gdy misja nie jest gotowa albo była już odebrana.
    public static bool Claim(int index)
    {
        DailyMission[] missions = Today;

        if (index < 0 || index >= missions.Length || !missions[index].Done || missions[index].claimed)
        {
            return false;
        }

        missions[index].claimed = true;
        PlayerPrefs.SetInt(ClaimedKey + index, 1);

        PlayerWallet.AddCoins(missions[index].template.coins);
        PlayerProgress.AddXp(missions[index].template.xp);
        PlayerPrefs.Save();
        return true;
    }

    // Losuje trzy różne misje na dany dzień: każda z innego licznika i najwyżej jedna wymagająca gry online.
    // Losowanie zależy tylko od daty, więc tego samego dnia wszyscy gracze mają te same misje (można je robić razem).
    static DailyMission[] Roll(int day)
    {
        System.Random random = new System.Random(day);
        List<MissionTemplate> shuffled = new List<MissionTemplate>(pool);

        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        List<DailyMission> picked = new List<DailyMission>();
        bool hasOnline = false;

        foreach (MissionTemplate template in shuffled)
        {
            if (picked.Count >= Count)
            {
                break;
            }

            if ((template.online && hasOnline) || picked.Exists(m => m.template.stat == template.stat))
            {
                continue;
            }

            hasOnline |= template.online;
            picked.Add(new DailyMission { template = template, baseline = PlayerProgress.Get(template.stat) });
        }

        return picked.ToArray();
    }

    static void Save(int day)
    {
        PlayerPrefs.SetInt(DayKey, day);

        for (int i = 0; i < today.Length; i++)
        {
            PlayerPrefs.SetString(IdKey + i, today[i].template.id);
            PlayerPrefs.SetInt(BaseKey + i, today[i].baseline);
            PlayerPrefs.SetInt(ClaimedKey + i, today[i].claimed ? 1 : 0);
        }

        PlayerPrefs.Save();
    }

    // Misje zapisane wcześniej tego samego dnia. null, gdy zapis jest niepełny (np. po zmianie puli misji).
    static DailyMission[] Load()
    {
        DailyMission[] loaded = new DailyMission[Count];

        for (int i = 0; i < Count; i++)
        {
            string id = PlayerPrefs.GetString(IdKey + i, "");
            MissionTemplate template = Array.Find(pool, t => t.id == id);

            if (template == null)
            {
                return null;
            }

            loaded[i] = new DailyMission
            {
                template = template,
                baseline = PlayerPrefs.GetInt(BaseKey + i, 0),
                claimed = PlayerPrefs.GetInt(ClaimedKey + i, 0) == 1
            };
        }

        return loaded;
    }
}
