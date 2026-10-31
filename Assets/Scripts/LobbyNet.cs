using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using UnityEngine;

// Pozycja innego gracza widziana przez sieć.
public class RemoteState
{
    public Vector2 position;
    public Facing facing;
    public bool moving;
}

// Szybkie łącze ruchu w poczekalni (przez Relay). Każdy telefon kilkanaście razy na sekundę wysyła pozycję, kierunek i to,
// czy idzie. Klienci wysyłają je do hosta, a host rozsyła dalej pozostałym (gwiazda: wszyscy łączą się z hostem).
// Wiadomości są "nieniezawodne z numeracją": zgubiona paczka nie szkodzi, bo zaraz przychodzi nowa.
// Samo łączenie (Relay) zakłada sesja z OnlineSession, a tu tylko wysyłamy i odbieramy własne wiadomości.
public static class LobbyNet
{
    const string Message = "bomba.state";

    public static readonly Dictionary<string, RemoteState> States = new Dictionary<string, RemoteState>();

    static CustomMessagingManager registeredOn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        States.Clear();
        registeredOn = null;
    }

    // Sesja potrzebuje w scenie NetworkManagera z transportem Unity. Robimy go w kodzie, żeby nie trzeba było nic dodawać w scenie.
    public static void EnsureManager()
    {
        if (NetworkManager.Singleton != null)
        {
            return;
        }

        GameObject go = new GameObject("NetworkManager");
        go.SetActive(false);

        NetworkManager manager = go.AddComponent<NetworkManager>();
        UnityTransport transport = go.AddComponent<UnityTransport>();

        // sceny wczytujemy sami (SceneManager), więc zarządzanie scenami przez Netcode jest wyłączone
        manager.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = false
        };

        go.SetActive(true);
    }

    public static bool Ready
    {
        get
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager != null && manager.IsListening && manager.CustomMessagingManager != null;
        }
    }

    // Wysyła swój stan. Wołać kilkanaście razy na sekundę.
    public static void Send(Vector2 position, Facing facing, bool moving)
    {
        if (!Ready || !AuthenticationService.Instance.IsSignedIn)
        {
            return;
        }

        NetworkManager manager = NetworkManager.Singleton;
        EnsureHandler(manager);

        using (FastBufferWriter writer = Pack(AuthenticationService.Instance.PlayerId, position, facing, moving))
        {
            if (manager.IsServer)
            {
                manager.CustomMessagingManager.SendNamedMessageToAll(Message, writer, NetworkDelivery.UnreliableSequenced);
            }
            else
            {
                manager.CustomMessagingManager.SendNamedMessage(Message, NetworkManager.ServerClientId, writer, NetworkDelivery.UnreliableSequenced);
            }
        }
    }

    // Odbiór zaczyna działać dopiero po tym, jak połączenie ruszy, więc handler podpinamy przy pierwszej okazji.
    public static void EnsureHandler(NetworkManager manager)
    {
        if (manager == null || manager.CustomMessagingManager == null || registeredOn == manager.CustomMessagingManager)
        {
            return;
        }

        registeredOn = manager.CustomMessagingManager;
        registeredOn.RegisterNamedMessageHandler(Message, OnState);
    }

    static FastBufferWriter Pack(string id, Vector2 position, Facing facing, bool moving)
    {
        FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(position.x);
        writer.WriteValueSafe(position.y);
        writer.WriteValueSafe((byte)facing);
        writer.WriteValueSafe(moving);
        return writer;
    }

    static void OnState(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out float x);
        reader.ReadValueSafe(out float y);
        reader.ReadValueSafe(out byte facing);
        reader.ReadValueSafe(out bool moving);

        if (!States.TryGetValue(id, out RemoteState state))
        {
            state = new RemoteState();
            States[id] = state;
        }

        state.position = new Vector2(x, y);
        state.facing = (Facing)facing;
        state.moving = moving;

        // host przekazuje stan od jednego klienta pozostałym
        NetworkManager manager = NetworkManager.Singleton;

        if (manager != null && manager.IsServer)
        {
            using (FastBufferWriter writer = Pack(id, state.position, state.facing, moving))
            {
                foreach (ulong client in manager.ConnectedClientsIds)
                {
                    if (client != senderClientId && client != NetworkManager.ServerClientId)
                    {
                        manager.CustomMessagingManager.SendNamedMessage(Message, client, writer, NetworkDelivery.UnreliableSequenced);
                    }
                }
            }
        }
    }
}
