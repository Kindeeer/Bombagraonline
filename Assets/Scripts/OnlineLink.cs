using System.Collections.Generic;
using UnityEngine;

// Łącznik rundy online z siecią (dokłada go MatchManager). Co chwilę wysyła pozycję mojej postaci, pilnuje odbioru wiadomości
// i sprawdza, czy ktoś nie wyszedł z gry: kto zniknął z sesji, jest traktowany jako martwy, więc runda się nie zawiesza.
public class OnlineLink : MonoBehaviour
{
    const float SendInterval = 0.05f;

    // tyle sekund czekamy na powrót połączenia (np. przy zmianie hosta), zanim wrócimy do menu
    const float OfflineLimit = 30f;

    PlayerMovement mine;
    PlayerHealth myHealth;
    float sendTimer;
    float offlineFor;
    bool wasOffline;

    void Start()
    {
        MatchManager match = GetComponent<MatchManager>();

        if (match != null && match.Slots[0] != null && match.Slots[0].health != null)
        {
            mine = match.Slots[0].health.GetComponent<PlayerMovement>();
            myHealth = match.Slots[0].health;
        }
    }

    void Update()
    {
        if (!OnlineMatch.Active)
        {
            return;
        }

        OnlineMatch.EnsureHandlers();

        if (OnlineSession.Connected && LobbyNet.Ready)
        {
            LobbyNet.EnsureHandler(Unity.Netcode.NetworkManager.Singleton);
        }

        // host zamknął pokój albo zniknęło połączenie: wracamy do menu
        if (OnlineSession.TakeEnded())
        {
            OnlineMatch.End();
            MainMenu.ReturnToMenu();
            return;
        }

        if (OnlineSession.TakeRefresh())
        {
            CheckLeavers();
        }

        // Zmiana hosta: przez kilka sekund nie ma połączenia (nowy host zakłada łącze, reszta się do niego podłącza).
        // Wiadomości wysłane w tym czasie przepadają, więc po powrocie połączenia jeszcze raz mówię pozostałym, że zginąłem
        // (inaczej moja postać stałaby u nich jako żywa i runda by się nie skończyła).
        if (!LobbyNet.Ready)
        {
            wasOffline = true;
            offlineFor += Time.unscaledDeltaTime;

            // połączenie nie wróciło: koniec gry online, wracamy do menu
            if (offlineFor > OfflineLimit)
            {
                OnlineMatch.End();
                MainMenu.ReturnToMenu();
                return;
            }
        }
        else if (wasOffline)
        {
            wasOffline = false;
            offlineFor = 0f;

            if (myHealth != null && myHealth.IsDead)
            {
                OnlineMatch.SendDead(OnlineMatch.MyId);
            }
        }

        sendTimer -= Time.unscaledDeltaTime;

        if (mine != null && sendTimer <= 0f && LobbyNet.Ready)
        {
            sendTimer = SendInterval;
            LobbyNet.Send(mine.transform.position, mine.Facing, mine.IsMoving);
        }
    }

    // Gracze, których nie ma już w sesji, uznajemy za martwych (u każdego tak samo).
    void CheckLeavers()
    {
        MatchManager match = MatchManager.Instance;

        if (match == null)
        {
            return;
        }

        HashSet<string> present = new HashSet<string>();

        foreach (OnlinePlayer player in OnlineSession.Players())
        {
            present.Add(player.id);
        }

        if (present.Count == 0)
        {
            return;
        }

        foreach (OnlineSlot slot in new List<OnlineSlot>(OnlineMatch.Players))
        {
            if (!slot.isMe && !present.Contains(slot.id))
            {
                OnlineMatch.PlayerLeft(slot.id);
            }
        }
    }

    // Zamknięcie gry w trakcie rundy też liczy się jako wyjście.
    void OnApplicationQuit()
    {
        OnlineMatch.SendLeave();
    }
}
