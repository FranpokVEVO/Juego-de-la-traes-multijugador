using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Unity.Netcode;

namespace MultiplayerLAN
{
    [Serializable]
    public class DiscoveryPacket
    {
        public string ServerName;
        public string HostIP;
        public ushort Port;
        public int CurrentPlayers;
        public int MaxPlayers;
    }

    public class DiscoveredServer
    {
        public string ServerName;
        public string HostIP;
        public ushort Port;
        public int CurrentPlayers;
        public int MaxPlayers;
        public float LastSeenTime;
    }

    public class LANDiscoveryManager : MonoBehaviour
    {
        public static LANDiscoveryManager Instance { get; private set; }

        [Header("Discovery Settings")]
        [SerializeField] private int discoveryPort = 47777;
        [SerializeField] private float broadcastInterval = 1.0f;
        [SerializeField] private float serverTimeout = 3.5f;

        public event Action<List<DiscoveredServer>> OnServerListUpdated;

        private UdpClient udpClient;
        private IPEndPoint listenEndPoint;
        private bool isBroadcasting = false;
        private bool isListening = false;
        private float broadcastTimer = 0f;

        private string serverName = "Partida LAN";
        private ushort gamePort = 7777;
        private int maxPlayers = 4;

        private readonly Dictionary<string, DiscoveredServer> discoveredServers = new Dictionary<string, DiscoveredServer>();

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

        private void Update()
        {
            if (isBroadcasting)
            {
                broadcastTimer += Time.deltaTime;
                if (broadcastTimer >= broadcastInterval)
                {
                    broadcastTimer = 0f;
                    SendBroadcast();
                }
            }

            if (isListening)
            {
                CheckForPackets();
                CleanupStaleServers();
            }
        }

        public void StartBroadcasting(string name, ushort port, int maxPlay = 4)
        {
            serverName = string.IsNullOrEmpty(name) ? "Partida LAN" : name;
            gamePort = port;
            maxPlayers = maxPlay;

            StopListening();

            try
            {
                if (udpClient == null)
                {
                    udpClient = new UdpClient();
                    udpClient.EnableBroadcast = true;
                }
                isBroadcasting = true;
                broadcastTimer = broadcastInterval; // Broadcast immediately
                Debug.Log($"[LANDiscovery] Anunciando servidor LAN '{serverName}' en puerto {discoveryPort}...");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LANDiscovery] Error iniciando transmisión UDP: {ex.Message}");
            }
        }

        public void StopBroadcasting()
        {
            isBroadcasting = false;
        }

        public void StartListening()
        {
            StopBroadcasting();
            discoveredServers.Clear();
            OnServerListUpdated?.Invoke(new List<DiscoveredServer>());

            try
            {
                if (udpClient == null)
                {
                    udpClient = new UdpClient();
                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udpClient.ExclusiveAddressUse = false;
                    listenEndPoint = new IPEndPoint(IPAddress.Any, discoveryPort);
                    udpClient.Client.Bind(listenEndPoint);
                }
                isListening = true;
                Debug.Log($"[LANDiscovery] Escuchando servidores LAN en puerto {discoveryPort}...");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LANDiscovery] Error iniciando escucha UDP: {ex.Message}");
            }
        }

        public void StopListening()
        {
            isListening = false;
            CloseSocket();
        }

        private void SendBroadcast()
        {
            try
            {
                int currentPlayers = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 1;
                
                DiscoveryPacket packet = new DiscoveryPacket
                {
                    ServerName = serverName,
                    HostIP = GetLocalIPAddress(),
                    Port = gamePort,
                    CurrentPlayers = currentPlayers,
                    MaxPlayers = maxPlayers
                };

                string jsonStr = JsonUtility.ToJson(packet);
                byte[] data = Encoding.UTF8.GetBytes(jsonStr);

                IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
                udpClient.Send(data, data.Length, broadcastEP);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LANDiscovery] Error enviando broadcast: {ex.Message}");
            }
        }

        private void CheckForPackets()
        {
            if (udpClient == null) return;

            try
            {
                while (udpClient.Available > 0)
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udpClient.Receive(ref remoteEP);
                    string jsonStr = Encoding.UTF8.GetString(data);

                    DiscoveryPacket packet = JsonUtility.FromJson<DiscoveryPacket>(jsonStr);
                    if (packet != null && !string.IsNullOrEmpty(packet.HostIP))
                    {
                        // Use remote sender IP if HostIP packet was local
                        string actualIP = (packet.HostIP == "127.0.0.1" || string.IsNullOrEmpty(packet.HostIP))
                            ? remoteEP.Address.ToString()
                            : packet.HostIP;

                        string key = $"{actualIP}:{packet.Port}";

                        DiscoveredServer server = new DiscoveredServer
                        {
                            ServerName = packet.ServerName,
                            HostIP = actualIP,
                            Port = packet.Port,
                            CurrentPlayers = packet.CurrentPlayers,
                            MaxPlayers = packet.MaxPlayers,
                            LastSeenTime = Time.time
                        };

                        discoveredServers[key] = server;
                        NotifyListUpdated();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LANDiscovery] Error recibiendo paquete UDP: {ex.Message}");
            }
        }

        private void CleanupStaleServers()
        {
            List<string> toRemove = new List<string>();
            foreach (var kvp in discoveredServers)
            {
                if (Time.time - kvp.Value.LastSeenTime > serverTimeout)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            if (toRemove.Count > 0)
            {
                foreach (string key in toRemove)
                {
                    discoveredServers.Remove(key);
                }
                NotifyListUpdated();
            }
        }

        private void NotifyListUpdated()
        {
            List<DiscoveredServer> list = new List<DiscoveredServer>(discoveredServers.Values);
            OnServerListUpdated?.Invoke(list);
        }

        public string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private void CloseSocket()
        {
            if (udpClient != null)
            {
                try
                {
                    udpClient.Close();
                }
                catch { }
                udpClient = null;
            }
        }

        private void OnDestroy()
        {
            StopBroadcasting();
            StopListening();
            CloseSocket();
        }

        private void OnApplicationQuit()
        {
            StopBroadcasting();
            StopListening();
            CloseSocket();
        }
    }
}
