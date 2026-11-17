using UnityEngine;

// Tryb drużynowy 2 na 2. Drużyny są dwie: 0 = niebiescy, 1 = czerwoni (-1 = gra bez drużyn).
// Ten plik odpowiada tylko na pytania "czy gramy drużynowo?", "kto jest z kim?" i "jak rozdzielić graczy".
// Reszta gry pyta o to tutaj, więc zasady drużyn są w jednym miejscu.
public static class Teams
{
    public const int None = -1;
    public const int Blue = 0;
    public const int Red = 1;

    public static readonly Color[] Colors =
    {
        new Color(0.27f, 0.58f, 1f),
        new Color(1f, 0.33f, 0.33f)
    };

    // true, gdy obecna runda jest drużynowa (ustawienie poczekalni albo przycisk "2 NA 2" w grze solo).
    public static bool Active => GameRules.Teams;

    public static Color ColorOf(int team)
    {
        return team >= 0 && team < Colors.Length ? Colors[team] : Color.white;
    }

    public static string NameOf(int team)
    {
        return team == Red ? Loc.P("CZERWONI", "RED TEAM") : Loc.P("NIEBIESCY", "BLUE TEAM");
    }

    // Czy dwaj uczestnicy grają w tej samej drużynie (ta sama postać też liczy się jako "swoja").
    public static bool Allies(Combatant a, Combatant b)
    {
        return Active && a != null && b != null && a.team >= 0 && a.team == b.team;
    }

    public static bool AlliedSlots(int slotA, int slotB)
    {
        MatchManager match = MatchManager.Instance;

        if (match == null || slotA < 0 || slotB < 0 || slotA >= match.Slots.Length || slotB >= match.Slots.Length)
        {
            return false;
        }

        return Allies(match.Slots[slotA], match.Slots[slotB]);
    }

    // Czy ogień z bomby gracza ownerSlot rani gracza victimSlot. Własna bomba rani zawsze, cudza z mojej drużyny tylko
    // wtedy, gdy host włączył "ogień w swoich". Ogień bez właściciela (rakieta, atomówka) rani każdego.
    public static bool FireHurts(int ownerSlot, int victimSlot)
    {
        if (!Active || GameRules.FriendlyFire || ownerSlot < 0 || ownerSlot == victimSlot)
        {
            return true;
        }

        return !AlliedSlots(ownerSlot, victimSlot);
    }

    // Rozdziela graczy na dwie drużyny. wishes[i] to życzenie gracza z poczekalni: 0 = obojętnie, 1 = niebiescy, 2 = czerwoni.
    // Życzenia są spełniane po kolei, dopóki w drużynie jest miejsce (połowa graczy, zaokrąglona w górę),
    // a pozostali trafiają do drużyny, w której jest mniej osób. Dzięki temu nigdy nie ma wszystkich w jednej drużynie.
    public static int[] Assign(int[] wishes)
    {
        int players = wishes.Length;
        int room = (players + 1) / 2;
        int[] result = new int[players];
        int[] count = new int[2];

        for (int i = 0; i < players; i++)
        {
            int wish = wishes[i] - 1;
            result[i] = None;

            if ((wish == Blue || wish == Red) && count[wish] < room)
            {
                result[i] = wish;
                count[wish]++;
            }
        }

        for (int i = 0; i < players; i++)
        {
            if (result[i] == None)
            {
                int team = count[Blue] <= count[Red] ? Blue : Red;
                result[i] = team;
                count[team]++;
            }
        }

        return result;
    }
}
