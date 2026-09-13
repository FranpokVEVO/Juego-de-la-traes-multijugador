using MultiplayerLAN;
using UnityEngine;
using Unity.Netcode;

public class ControlDeInicioDePartida : NetworkBehaviour
{
    public MultiplayerNetworkMenu ControlMenu;
    public LANDiscoveryManager ManejoDePlayer;

    public bool suficiente = false;

    public NetworkVariable<bool> PartidaIniciada =
        new NetworkVariable<bool>(false);

    private void Update()
    {
        if (NetworkManager.Singleton == null)
            return;

        int jugadores = NetworkManager.Singleton.ConnectedClients.Count;

        suficiente = jugadores >= 2 && jugadores <= 4;
    }

    public void IniciarPartida()
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        int jugadores = NetworkManager.Singleton.ConnectedClients.Count;

        if (jugadores >= 2 && jugadores <= 4)
        {
            PartidaIniciada.Value = true;

            var jugadoresConectados = NetworkManager.Singleton.ConnectedClientsList;

            int elegido = Random.Range(0, jugadoresConectados.Count);

            foreach (var jugador in jugadoresConectados)
            {
                tocaraotrosplayers player =
                    jugador.PlayerObject.GetComponent<tocaraotrosplayers>();

                if (player != null)
                {
                    player.LaTraes.Value = false;
                }
            }

            tocaraotrosplayers jugadorElegido =
                jugadoresConectados[elegido].PlayerObject.GetComponent<tocaraotrosplayers>();

            if (jugadorElegido != null)
            {
                jugadorElegido.LaTraes.Value = true;
            }

            OcultarMenuClientRpc();
        }
    }

    [ClientRpc]
    private void OcultarMenuClientRpc()
    {
        if (ControlMenu != null)
        {
            ControlMenu.gameObject.SetActive(false);
        }
    }
}