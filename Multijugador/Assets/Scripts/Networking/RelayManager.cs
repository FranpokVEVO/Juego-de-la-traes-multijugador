using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace MultiplayerLAN
{
    public class RelayManager : MonoBehaviour
    {
        public static RelayManager Instance { get; private set; }

        public bool IsInitialized { get; private set; } = false;
        public string CurrentJoinCode { get; private set; } = "";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async Task<bool> InitializeAndAuthenticateAsync()
        {
            if (IsInitialized && AuthenticationService.Instance.IsSignedIn)
            {
                return true;
            }

            try
            {
                Debug.Log("[RelayManager] Inicializando Unity Gaming Services...");
                await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("[RelayManager] Autenticando jugador de forma anónima...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"[RelayManager] Autenticado con éxito. Player ID: {AuthenticationService.Instance.PlayerId}");
                }

                IsInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RelayManager] Error al inicializar o autenticar servicios UGS: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Crea una sala Relay para Host (en la nube de Unity) y devuelve el Join Code de 6 caracteres.
        /// </summary>
        public async Task<string> CreateRelayHostAsync(int maxConnections = 4)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[RelayManager] Error: NetworkManager no existe en la escena.");
                return null;
            }

            bool authOk = await InitializeAndAuthenticateAsync();
            if (!authOk) return null;

            try
            {
                Debug.Log("[RelayManager] Solicitando asignación de servidor Relay (Host)...");
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections - 1);

                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                CurrentJoinCode = joinCode;

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    transport.SetHostRelayData(
                        allocation.RelayServer.IpV4,
                        (ushort)allocation.RelayServer.Port,
                        allocation.AllocationIdBytes,
                        allocation.Key,
                        allocation.ConnectionData
                    );

                    Debug.Log($"[RelayManager] Transport configurado con Relay IP: {allocation.RelayServer.IpV4}:{allocation.RelayServer.Port}");
                }
                else
                {
                    Debug.LogError("[RelayManager] UnityTransport no encontrado en NetworkManager.");
                    return null;
                }

                if (NetworkManager.Singleton.StartHost())
                {
                    Debug.Log($"[RelayManager] ¡Host Online iniciado con éxito! CÓDIGO RELAY: {joinCode}");
                    return joinCode;
                }
                else
                {
                    Debug.LogError("[RelayManager] NetworkManager.StartHost() falló.");
                    return null;
                }
            }
            catch (RelayServiceException ex)
            {
                Debug.LogError($"[RelayManager] Error en Relay Service: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RelayManager] Error al crear sala Relay: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Se conecta a una sala Relay existente mediante un Join Code de 6 caracteres.
        /// </summary>
        public async Task<bool> JoinRelayClientAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                Debug.LogError("[RelayManager] El Código de Sala Relay no puede estar vacío.");
                return false;
            }

            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[RelayManager] Error: NetworkManager no existe en la escena.");
                return false;
            }

            bool authOk = await InitializeAndAuthenticateAsync();
            if (!authOk) return false;

            string cleanCode = joinCode.Trim().ToUpper();

            try
            {
                Debug.Log($"[RelayManager] Uniéndose a sala Relay con código: {cleanCode}...");
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(cleanCode);

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    transport.SetClientRelayData(
                        joinAllocation.RelayServer.IpV4,
                        (ushort)joinAllocation.RelayServer.Port,
                        joinAllocation.AllocationIdBytes,
                        joinAllocation.Key,
                        joinAllocation.ConnectionData,
                        joinAllocation.HostConnectionData
                    );

                    Debug.Log($"[RelayManager] Transport cliente configurado a Relay IP: {joinAllocation.RelayServer.IpV4}:{joinAllocation.RelayServer.Port}");
                }
                else
                {
                    Debug.LogError("[RelayManager] UnityTransport no encontrado en NetworkManager.");
                    return false;
                }

                if (NetworkManager.Singleton.StartClient())
                {
                    CurrentJoinCode = cleanCode;
                    Debug.Log($"[RelayManager] ¡Conexión cliente iniciada a sala Relay {cleanCode}!");
                    return true;
                }
                else
                {
                    Debug.LogError("[RelayManager] NetworkManager.StartClient() falló.");
                    return false;
                }
            }
            catch (RelayServiceException ex)
            {
                Debug.LogError($"[RelayManager] Error al unirse a Relay con código '{cleanCode}': {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RelayManager] Error inesperado al unirse a Relay: {ex.Message}");
                return false;
            }
        }
    }
}
