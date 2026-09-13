using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace MultiplayerLAN
{
    public enum NetworkMode
    {
        OnlineRelay,
        LocalLAN
    }

    public class MultiplayerNetworkMenu : MonoBehaviour
    {
        [Header("Configuración General")]
        [SerializeField] private NetworkMode currentMode = NetworkMode.OnlineRelay;
        [SerializeField] private string roomName = "Partida de Amigos";
        [SerializeField] private ushort port = 7777;

        [Header("Control de partida")]
        public ControlDeInicioDePartida ControlInicio;

        [Header("Modo Online (Relay)")]
        [SerializeField] private string onlineInputCode = "";

        [Header("Modo LAN (Local)")]
        [SerializeField] private string lanInputCode = "";

        private List<DiscoveredServer> availableServers = new List<DiscoveredServer>();
        private Vector2 scrollPosition;
        private string activeRoomCode = "";
        private string statusMessage = "";
        private bool isConnecting = false;

        private void Awake()
        {
            var oldMenus = FindObjectsOfType<LANNetworkMenu>();

            foreach (var old in oldMenus)
            {
                Destroy(old);
            }
        }

        private void Start()
        {
            if (LANDiscoveryManager.Instance != null)
            {
                LANDiscoveryManager.Instance.OnServerListUpdated += OnServerListUpdated;
                LANDiscoveryManager.Instance.StartListening();
            }

            if (RelayManager.Instance == null)
            {
                var go = new GameObject("RelayManager");
                go.AddComponent<RelayManager>();
            }
        }

        private void OnDestroy()
        {
            if (LANDiscoveryManager.Instance != null)
            {
                LANDiscoveryManager.Instance.OnServerListUpdated -= OnServerListUpdated;
            }
        }

        private void OnServerListUpdated(List<DiscoveredServer> servers)
        {
            availableServers = servers;
        }

        #region ONLINE RELAY METHODS

        public async void StartOnlineHost()
        {
            if (isConnecting) return;

            isConnecting = true;
            statusMessage = "Conectando con Unity Relay Servers...";

            if (RelayManager.Instance != null)
            {
                string joinCode = await RelayManager.Instance.CreateRelayHostAsync(4);

                if (!string.IsNullOrEmpty(joinCode))
                {
                    activeRoomCode = joinCode;
                    statusMessage = "¡Sala Online creada con éxito!";
                }
                else
                {
                    statusMessage = "Error al crear sala Online Relay.";
                }
            }
            else
            {
                statusMessage = "Error: RelayManager no disponible.";
            }

            isConnecting = false;
        }

        public async void JoinOnlineByCode()
        {
            if (isConnecting) return;

            if (string.IsNullOrWhiteSpace(onlineInputCode))
            {
                statusMessage = "Ingresa un Código Relay válido de 6 caracteres.";
                return;
            }

            isConnecting = true;
            statusMessage = $"Conectando a Sala Relay '{onlineInputCode.Trim().ToUpper()}'...";

            if (RelayManager.Instance != null)
            {
                bool success = await RelayManager.Instance.JoinRelayClientAsync(onlineInputCode);

                if (success)
                {
                    activeRoomCode = onlineInputCode.Trim().ToUpper();
                    statusMessage = "¡Conectado exitosamente a la Sala Online!";
                }
                else
                {
                    statusMessage = "Error al unirse a la sala Relay. Revisa el código.";
                }
            }
            else
            {
                statusMessage = "Error: RelayManager no disponible.";
            }

            isConnecting = false;
        }

        #endregion

        #region LAN LOCAL METHODS

        public void StartLANHost()
        {
            if (NetworkManager.Singleton == null) return;

            string localIP = GetLocalIPAddress();

            ConfigureTransport("0.0.0.0", port);

            if (NetworkManager.Singleton.StartHost())
            {
                activeRoomCode = IPToRoomCode(localIP);
                statusMessage = "¡Sala LAN creada con éxito!";

                Debug.Log($"[MultiplayerMenu] Host LAN iniciado. IP: {localIP}, Código: {activeRoomCode}");

                if (LANDiscoveryManager.Instance != null)
                {
                    LANDiscoveryManager.Instance.StartBroadcasting(roomName, port);
                }
            }
            else
            {
                statusMessage = "Error al iniciar Host LAN.";
            }
        }

        public void JoinLANByCode()
        {
            if (string.IsNullOrEmpty(lanInputCode))
            {
                statusMessage = "Ingresa un Código de Sala o IP válido.";
                return;
            }

            string destinationIP = RoomCodeToIP(lanInputCode);

            JoinLANGame(destinationIP, port);
        }

        public void JoinLANGame(string ipAddress, ushort gamePort)
        {
            if (NetworkManager.Singleton == null) return;

            ConfigureTransport(ipAddress, gamePort);

            if (NetworkManager.Singleton.StartClient())
            {
                statusMessage = $"Conectando a {ipAddress} (LAN)...";

                if (LANDiscoveryManager.Instance != null)
                {
                    LANDiscoveryManager.Instance.StopListening();
                }
            }
            else
            {
                statusMessage = "Error al intentar conectar por LAN.";
            }
        }

        #endregion

        public void Disconnect()
        {
            if (NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.Shutdown();

            activeRoomCode = "";
            statusMessage = "Desconectado.";

            if (LANDiscoveryManager.Instance != null)
            {
                LANDiscoveryManager.Instance.StartListening();
            }
        }

        private void ConfigureTransport(string ip, ushort transportPort)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            if (transport != null)
            {
                transport.SetConnectionData(ip, transportPort);
            }
        }

        private string GetLocalIPAddress()
        {
            if (LANDiscoveryManager.Instance != null)
            {
                return LANDiscoveryManager.Instance.GetLocalIPAddress();
            }

            return "127.0.0.1";
        }

        public static string IPToRoomCode(string ipStr)
        {
            try
            {
                if (IPAddress.TryParse(ipStr, out var ip))
                {
                    byte[] bytes = ip.GetAddressBytes();

                    if (bytes.Length == 4)
                    {
                        uint ipNum =
                            ((uint)bytes[0] << 24) |
                            ((uint)bytes[1] << 16) |
                            ((uint)bytes[2] << 8) |
                            bytes[3];

                        return ipNum.ToString("X8");
                    }
                }
            }
            catch { }

            return ipStr;
        }

        public static string RoomCodeToIP(string code)
        {
            if (string.IsNullOrEmpty(code))
                return "127.0.0.1";

            string clean = code.Trim().ToUpper();

            if (clean.Length == 8 &&
                uint.TryParse(
                    clean,
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out uint ipNum))
            {
                byte b1 = (byte)((ipNum >> 24) & 0xFF);
                byte b2 = (byte)((ipNum >> 16) & 0xFF);
                byte b3 = (byte)((ipNum >> 8) & 0xFF);
                byte b4 = (byte)(ipNum & 0xFF);

                return $"{b1}.{b2}.{b3}.{b4}";
            }

            return code.Trim();
        }

        private void OnGUI()
        {
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.cyan }
            };

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(15, 15, 15, 15)
            };

            GUIStyle bigButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };

            GUIStyle tabButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 35
            };

            GUIStyle codeBoxStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };

            Rect menuRect = new Rect(20, 20, 420, 600);

            GUILayout.BeginArea(menuRect, boxStyle);

            GUILayout.Label("SISTEMA MULTIJUGADOR", titleStyle);
            GUILayout.Space(8);

            if (NetworkManager.Singleton == null)
            {
                GUILayout.Label("<color=red>Error: NetworkManager no encontrado en la escena.</color>");
                GUILayout.EndArea();
                return;
            }

            bool isConnected =
                NetworkManager.Singleton.IsClient ||
                NetworkManager.Singleton.IsServer;

            if (!isConnected)
            {
                GUILayout.BeginHorizontal();

                GUI.backgroundColor =
                    (currentMode == NetworkMode.OnlineRelay)
                    ? Color.green
                    : Color.gray;

                if (GUILayout.Button("🌐 ONLINE (INTERNET)", tabButtonStyle))
                {
                    currentMode = NetworkMode.OnlineRelay;
                    statusMessage = "";
                }

                GUI.backgroundColor =
                    (currentMode == NetworkMode.LocalLAN)
                    ? Color.cyan
                    : Color.gray;

                if (GUILayout.Button("🏠 LAN (RED LOCAL)", tabButtonStyle))
                {
                    currentMode = NetworkMode.LocalLAN;
                    statusMessage = "";
                }

                GUI.backgroundColor = Color.white;

                GUILayout.EndHorizontal();

                GUILayout.Space(12);

                if (currentMode == NetworkMode.OnlineRelay)
                {
                    GUILayout.Label("<b>1. CREAR SALA ONLINE (INTERNET)</b>");
                    GUILayout.Label("<size=11><color=grey>Genera un código global sin necesidad de abrir puertos.</color></size>");

                    GUI.enabled = !isConnecting;

                    if (GUILayout.Button(
                        isConnecting
                            ? "CONECTANDO A RELAY..."
                            : "CREAR SALA ONLINE (CÓDIGO RELAY)",
                        bigButtonStyle))
                    {
                        StartOnlineHost();
                    }

                    GUI.enabled = true;

                    GUILayout.Space(15);

                    GUILayout.Label("<b>2. UNIRSE CON CÓDIGO RELAY</b>");

                    GUILayout.BeginHorizontal();

                    GUILayout.Label("Código:", GUILayout.Width(60));

                    onlineInputCode =
                        GUILayout.TextField(
                            onlineInputCode.ToUpper(),
                            codeBoxStyle,
                            GUILayout.Height(32));

                    GUILayout.EndHorizontal();

                    GUI.enabled = !isConnecting;

                    if (GUILayout.Button(
                        "UNIRSE A SALA ONLINE",
                        bigButtonStyle))
                    {
                        JoinOnlineByCode();
                    }

                    GUI.enabled = true;
                }
                else
                {
                    GUILayout.Label("<b>1. CREAR SALA LAN (HOST)</b>");

                    GUILayout.BeginHorizontal();

                    GUILayout.Label("Nombre:", GUILayout.Width(60));

                    roomName = GUILayout.TextField(roomName);

                    GUILayout.EndHorizontal();

                    if (GUILayout.Button(
                        "CREAR SALA LOCAL Y OBTENER CÓDIGO",
                        bigButtonStyle))
                    {
                        StartLANHost();
                    }

                    GUILayout.Space(15);

                    GUILayout.Label("<b>2. UNIRSE A SALA LAN CON CÓDIGO/IP</b>");

                    GUILayout.BeginHorizontal();

                    GUILayout.Label("Código/IP:", GUILayout.Width(80));

                    lanInputCode =
                        GUILayout.TextField(
                            lanInputCode,
                            codeBoxStyle,
                            GUILayout.Height(30));

                    GUILayout.EndHorizontal();

                    if (GUILayout.Button(
                        "UNIRSE A SALA LAN",
                        bigButtonStyle))
                    {
                        JoinLANByCode();
                    }

                    GUILayout.Space(15);

                    GUILayout.Label("<b>3. SALAS DETECTADAS EN TU RED (1 Clic)</b>");

                    scrollPosition =
                        GUILayout.BeginScrollView(
                            scrollPosition,
                            GUILayout.Height(100));

                    if (availableServers.Count == 0)
                    {
                        GUILayout.Label(
                            "<color=grey>Buscando partidas en tu Wi-Fi/red local...</color>");
                    }
                    else
                    {
                        foreach (var server in availableServers)
                        {
                            GUILayout.BeginHorizontal(GUI.skin.box);

                            GUILayout.BeginVertical();

                            GUILayout.Label($"<b>{server.ServerName}</b>");

                            GUILayout.Label(
                                $"Código LAN: <color=yellow>{IPToRoomCode(server.HostIP)}</color>");

                            GUILayout.EndVertical();

                            if (GUILayout.Button(
                                "ENTRAR",
                                GUILayout.Width(70),
                                GUILayout.Height(30)))
                            {
                                JoinLANGame(
                                    server.HostIP,
                                    server.Port);
                            }

                            GUILayout.EndHorizontal();
                        }
                    }

                    GUILayout.EndScrollView();
                }

                if (!string.IsNullOrEmpty(statusMessage))
                {
                    GUILayout.Space(8);

                    GUILayout.Label(
                        $"<color=yellow>{statusMessage}</color>");
                }
            }
            else
            {
                string role =
                    NetworkManager.Singleton.IsHost
                    ? "HOST (CREADOR)"
                    : "CLIENTE (JUGADOR)";

                string modeText =
                    (currentMode == NetworkMode.OnlineRelay)
                    ? "ONLINE (RELAY)"
                    : "LAN LOCAL";

                GUILayout.Label(
                    $"<b>MODO:</b> <color=cyan>{modeText}</color>");

                GUILayout.Label(
                    $"<b>ESTADO:</b> <color=green>CONECTADO COMO {role}</color>");

                GUILayout.Label(
                    $"Jugadores en la partida: <b>{NetworkManager.Singleton.ConnectedClientsIds.Count}</b>");

                if (!string.IsNullOrEmpty(activeRoomCode))
                {
                    GUILayout.Space(15);

                    string codeLabel =
                        (currentMode == NetworkMode.OnlineRelay)
                        ? "CÓDIGO DE SALA ONLINE (RELAY):"
                        : "CÓDIGO DE SALA LAN:";

                    GUILayout.Label($"<b>{codeLabel}</b>");

                    GUILayout.BeginHorizontal();

                    GUILayout.TextField(
                        activeRoomCode,
                        codeBoxStyle,
                        GUILayout.Height(35));

                    if (GUILayout.Button(
                        "COPIAR",
                        GUILayout.Width(65),
                        GUILayout.Height(35)))
                    {
                        GUIUtility.systemCopyBuffer =
                            activeRoomCode;

                        statusMessage =
                            "¡Código copiado al portapapeles!";
                    }

                    GUILayout.EndHorizontal();

                    GUILayout.Label(
                        "<size=11><color=grey>Comparte este código a tus amigos en cualquier parte del mundo.</color></size>");
                }

                if (!string.IsNullOrEmpty(statusMessage))
                {
                    GUILayout.Space(5);

                    GUILayout.Label(
                        $"<color=cyan>{statusMessage}</color>");
                }

                if (NetworkManager.Singleton.IsHost && ControlInicio != null && ControlInicio.suficiente)
                {
                    if (GUILayout.Button("INICIAR PARTIDA", bigButtonStyle))
                    {
                        ControlInicio.IniciarPartida();
                    }
                }
                GUILayout.Space(25);

                if (GUILayout.Button(
                    "SALIR / DESCONECTAR",
                    bigButtonStyle))
                {
                    Disconnect();
                }
            }

            GUILayout.EndArea();
        }
    }
}