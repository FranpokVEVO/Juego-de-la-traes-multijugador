using UnityEngine;
using Unity.Netcode;

public class tocaraotrosplayers : NetworkBehaviour
{
    public Transform hitbox_Tocar;

    public NetworkVariable<bool> LaTraes =
        new NetworkVariable<bool>(false);

    private void OnEnable()
    {
        LaTraes.OnValueChanged += CambiarColor;
    }

    private void OnDisable()
    {
        LaTraes.OnValueChanged -= CambiarColor;
    }

    private void CambiarColor(bool anterior, bool nuevo)
    {
        Renderer renderer = GetComponentInChildren<Renderer>();

        if (renderer != null)
        {
            renderer.material.color = nuevo ? Color.red : Color.white;
        }
    }

    public void Update()
    {
        if (!IsOwner) return;

        if (LaTraes.Value == true)
        {
            if (Input.GetMouseButtonDown(0))
            {
                hitbox_Tocar.gameObject.SetActive(true);
            }

            if (Input.GetMouseButtonUp(0))
            {
                hitbox_Tocar.gameObject.SetActive(false);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        if (other.CompareTag("Player"))
        {
            tocaraotrosplayers jugadorTocado =
                other.GetComponentInParent<tocaraotrosplayers>();

            if (jugadorTocado != null)
            {
                TocarJugadorServerRpc(
                    jugadorTocado.NetworkObjectId
                );
            }
        }
    }

    [ServerRpc]
    private void TocarJugadorServerRpc(ulong jugadorTocadoId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
            jugadorTocadoId,
            out NetworkObject objetoTocado))
        {
            return;
        }

        tocaraotrosplayers jugadorTocado =
            objetoTocado.GetComponent<tocaraotrosplayers>();

        if (jugadorTocado == null)
            return;

        if (!LaTraes.Value)
            return;

        jugadorTocado.LaTraes.Value = true;
        LaTraes.Value = false;

        DesactivarHitboxClientRpc();
    }

    [ClientRpc]
    private void DesactivarHitboxClientRpc()
    {
        if (hitbox_Tocar != null)
        {
            hitbox_Tocar.gameObject.SetActive(false);
        }
    }
}