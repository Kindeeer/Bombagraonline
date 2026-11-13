using System;
using UnityEngine;

// Cztery emotki dostępne w grze.
public enum Emote
{
    Angry,
    Laugh,
    Cry,
    Like
}

// Wysyłanie emotek. Send(slot, emote) pokazuje dymek przy karcie gracza o danym slocie (patrz GameHud).
// Send wysyła moją emotkę także przez sieć, a Show tylko ją pokazuje (woła go odbiór emotki od innego gracza).
public static class Emotes
{
    public const int Count = 4;

    public static event Action<int, Emote> Sent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Sent = null;
    }

    // Emotka wysłana przeze mnie (slot 0): pokazuje się u mnie i leci do pozostałych graczy w rundzie online.
    public static void Send(int slot, Emote emote)
    {
        Show(slot, emote);

        // runda online albo poczekalnia przez internet
        if (slot == 0 && (OnlineMatch.Active || (LobbyState.InLobby && OnlineSession.Connected)))
        {
            OnlineMatch.SendEmote((int)emote);
        }
    }

    // Emotka do pokazania (moja albo odebrana z sieci): dymek przy karcie gracza i nad jego głową.
    public static void Show(int slot, Emote emote)
    {
        GameAudio.Play(AudioEvent.Emote);
        Sent?.Invoke(slot, emote);
    }

    public static string Label(Emote emote)
    {
        switch (emote)
        {
            case Emote.Angry: return Loc.T("emote.angry");
            case Emote.Laugh: return Loc.T("emote.laugh");
            case Emote.Cry: return Loc.T("emote.cry");
            default: return Loc.T("emote.like");
        }
    }

    // Grafika emotki z obiektu GameArt, a gdy jej nie ma, rysunek zastępczy.
    public static Sprite SpriteFor(Emote emote)
    {
        return PickupArt.EmoteIcon(emote);
    }
}
