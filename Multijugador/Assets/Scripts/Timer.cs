using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

public class Timer : NetworkBehaviour
{
    public TMP_Text Ganaste;
    public TMP_Text Perdiste;
    public TMP_Text textoTimer;
    public ControlDeInicioDePartida IniciadorCondicion;
    public NetworkVariable<float> tiempo = new NetworkVariable<float>(10);
    public NetworkVariable<bool> partidaTerminada = new NetworkVariable<bool>(false);
    public void Update()
    {
        if (IniciadorCondicion.PartidaIniciada.Value == true)
        {
            if (IsServer)
            {
                MostrarTimerClientRpc();

                if (tiempo.Value > 0)
                {

                    tiempo.Value -= Time.deltaTime;
                }
                if (tiempo.Value <= 0)
                {
                    tiempo.Value = 0;
                    partidaTerminada.Value = true;
                    foreach (var jugador in NetworkManager.Singleton.ConnectedClientsList)
                    {
                        MostrarResultadoClientRpc(jugador.ClientId);
                    }
                }
            }
            if (partidaTerminada.Value && Input.GetKeyDown(KeyCode.Return))
            {
                ReiniciarPartida();
            }

            textoTimer.text = Mathf.Ceil(tiempo.Value).ToString();
        }
    }
    private void ReiniciarPartida()
    {
        Time.timeScale = 1;

        tiempo.Value = 30;
        partidaTerminada.Value = false;

        OcultarResultadoClientRpc();
    }
    [ClientRpc]
    private void MostrarTimerClientRpc()
    {
        textoTimer.gameObject.SetActive(true);
    }

    [ClientRpc]
    private void MostrarResultadoClientRpc(ulong jugadorId)
    {
        if (NetworkManager.Singleton.LocalClientId != jugadorId)
            return;

        tocaraotrosplayers player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<tocaraotrosplayers>();

        if (player.LaTraes.Value == true)
        {
            Ganaste.gameObject.SetActive(false);
            Perdiste.gameObject.SetActive(true);
        }
        else
        {
            Ganaste.gameObject.SetActive(true);
            Perdiste.gameObject.SetActive(false);
        }
    }
    [ClientRpc]
    private void OcultarResultadoClientRpc()
    {
        Ganaste.gameObject.SetActive(false);
        Perdiste.gameObject.SetActive(false);
        textoTimer.gameObject.SetActive(false);
    }
}
