using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace MultiplayerLAN
{
    public class LANNetworkMenu : MonoBehaviour
    {
        [Header("Room Options")]
        [SerializeField] private string roomName = "Partida de Amigos";
        [SerializeField] private string inputRoomCode = "";
        [SerializeField] private ushort port = 7777;

        private List<DiscoveredServer> availableServers = new List<DiscoveredServer>();
        private Vector2 scrollPosition;
        private string generatedRoomCode = "";
        private string statusMessage = "";

        private void Awake()
        {
            if (GetComponent<MultiplayerNetworkMenu>() != null || FindObjectOfType<MultiplayerNetworkMenu>() != null)
            {
                Debug.LogWarning("[LANNetworkMenu] Desactivando menú antiguo LAN porque MultiplayerNetworkMenu (Online + LAN) está presente en la escena.");
                enabled = false;
                Destroy(this);
            }
        }

        private void Start()
        {
            if (LANDiscoveryManager.Instance != null)
            {
                LANDiscoveryManager.Instance.OnServerListUpdated += OnServerListUpdated;
                LANDiscoveryManager.Instance.StartListening();
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

        public void StartHost()
        {
            if (NetworkManager.Singleton == null) return;

            string localIP = GetLocalIPAddress();
            ConfigureTransport("0.0.0.0", port);

            if (NetworkManager.Singleton.StartHost())
            {
                generatedRoomCode = IPToRoomCode(localIP);
                statusMessage = "¡Sala creada con éxito!";
                Debug.Log($"[LANMenu] Host iniciado. IP: {localIP}, Código de Sala: {generatedRoomCode}");

                if (LANDiscoveryManager.Instance != null)
                {
                    LANDiscoveryManager.Instance.StartBroadcasting(roomName, port);
                }
            }
            else
            {
                statusMessage = "Error al iniciar Host.";
            }
        }

        public void JoinByCode()
        {
            if (string.IsNullOrEmpty(inputRoomCode))
            {
                statusMessage = "Ingresa un Código de Sala o IP válido.";
                return;
            }

            string destinationIP = RoomCodeToIP(inputRoomCode);
            JoinGame(destinationIP, port);
        }

        public void JoinGame(string ipAddress, ushort gamePort)
        {
            if (NetworkManager.Singleton == null) return;

            ConfigureTransport(ipAddress, gamePort);

            if (NetworkManager.Singleton.StartClient())
            {
                statusMessage = $"Conectando a {ipAddress}...";
                Debug.Log($"[LANMenu] Conectando a {ipAddress}:{gamePort}...");
                if (LANDiscoveryManager.Instance != null)
                {
                    LANDiscoveryManager.Instance.StopListening();
                }
            }
            else
            {
                statusMessage = "Error al intentar conectar.";
            }
        }

        public void Disconnect()
        {
            if (NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.Shutdown();
            generatedRoomCode = "";
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

        // Converts IP 192.168.1.71 to Hex Code C0A80147
        public static string IPToRoomCode(string ipStr)
        {
            try
            {
                if (IPAddress.TryParse(ipStr, out var ip))
                {
                    byte[] bytes = ip.GetAddressBytes();
                    if (bytes.Length == 4)
                    {
                        uint ipNum = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | (uint)bytes[3];
                        return ipNum.ToString("X8");
                    }
                }
            }
            catch { }
            return ipStr;
        }

        // Decodes Hex Code C0A80147 back to IP 192.168.1.71
        public static string RoomCodeToIP(string code)
        {
            if (string.IsNullOrEmpty(code)) return "127.0.0.1";

            string clean = code.Trim().ToUpper();

            if (clean.Length == 8 && uint.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out uint ipNum))
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

            GUIStyle codeBoxStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };

            Rect menuRect = new Rect(20, 20, 400, 580);
            GUILayout.BeginArea(menuRect, boxStyle);

            GUILayout.Label("SISTEMA MULTIJUGADOR", titleStyle);
            GUILayout.Space(10);

            if (NetworkManager.Singleton == null)
            {
                GUILayout.Label("<color=red>Error: NetworkManager no encontrado.</color>");
                GUILayout.EndArea();
                return;
            }

            bool isConnected = NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer;

            if (!isConnected)
            {
                // HOST SECTION
                GUILayout.Label("<b>1. CREAR UNA SALA (HOST)</b>");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Nombre:", GUILayout.Width(60));
                roomName = GUILayout.TextField(roomName);
                GUILayout.EndHorizontal();

                if (GUILayout.Button("CREAR SALA Y OBTENER CÓDIGO", bigButtonStyle))
                {
                    StartHost();
                }

                GUILayout.Space(15);
                // JOIN BY CODE SECTION
                GUILayout.Label("<b>2. UNIRSE A UNA SALA CON CÓDIGO</b>");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Código:", GUILayout.Width(60));
                inputRoomCode = GUILayout.TextField(inputRoomCode, codeBoxStyle, GUILayout.Height(30));
                GUILayout.EndHorizontal();

                if (GUILayout.Button("UNIRSE A SALA", bigButtonStyle))
                {
                    JoinByCode();
                }

                GUILayout.Space(15);
                // AUTO LAN DISCOVERY SECTION
                GUILayout.Label("<b>3. SALAS EN TU MISMA RED (1 Clic)</b>");
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(110));

                if (availableServers.Count == 0)
                {
                    GUILayout.Label("<color=grey>Buscando salas en tu red local...</color>");
                }
                else
                {
                    foreach (var server in availableServers)
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box);
                        GUILayout.BeginVertical();
                        GUILayout.Label($"<b>{server.ServerName}</b>");
                        GUILayout.Label($"Código: <color=yellow>{IPToRoomCode(server.HostIP)}</color>");
                        GUILayout.EndVertical();

                        if (GUILayout.Button("ENTRAR", GUILayout.Width(70), GUILayout.Height(30)))
                        {
                            JoinGame(server.HostIP, server.Port);
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndScrollView();

                if (!string.IsNullOrEmpty(statusMessage))
                {
                    GUILayout.Space(5);
                    GUILayout.Label($"<color=yellow>{statusMessage}</color>");
                }
            }
            else
            {
                // IN-GAME LOBBY / STATUS SECTION
                string role = NetworkManager.Singleton.IsHost ? "HOST (CREADOR)" : "CLIENTE (JUGADOR)";
                GUILayout.Label($"<b>ESTADO:</b> <color=green>CONECTADO COMO {role}</color>");
                GUILayout.Label($"Jugadores en la partida: <b>{NetworkManager.Singleton.ConnectedClientsIds.Count}</b>");

                if (NetworkManager.Singleton.IsHost)
                {
                    GUILayout.Space(15);
                    GUILayout.Label("<b>CÓDIGO DE TU SALA PARA TUS AMIGOS:</b>");
                    
                    GUILayout.BeginHorizontal();
                    GUILayout.TextField(generatedRoomCode, codeBoxStyle, GUILayout.Height(35));
                    if (GUILayout.Button("COPIAR", GUILayout.Width(65), GUILayout.Height(35)))
                    {
                        GUIUtility.systemCopyBuffer = generatedRoomCode;
                        statusMessage = "¡Código copiado al portapapeles!";
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Label("<size=11><color=grey>Comparte este código a tus amigos para que lo peguen en 'Código de Sala'.</color></size>");
                }

                if (!string.IsNullOrEmpty(statusMessage))
                {
                    GUILayout.Space(5);
                    GUILayout.Label($"<color=cyan>{statusMessage}</color>");
                }

                GUILayout.Space(25);

                if (GUILayout.Button("SALIR / DESCONECTAR", bigButtonStyle))
                {
                    Disconnect();
                }
            }

            GUILayout.EndArea();
        }
    }
}
