using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;

namespace MultiplayerLAN
{
    public class LANManagerSetup : MonoBehaviour
    {
        private static GameObject defaultPlayerPrefabTemplate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeLANSystem()
        {
            // Ensure NetworkManager exists
            if (NetworkManager.Singleton == null)
            {
                GameObject netManagerObj = new GameObject("NetworkManager");
                NetworkManager netManager = netManagerObj.AddComponent<NetworkManager>();
                UnityTransport transport = netManagerObj.AddComponent<UnityTransport>();

                netManager.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = transport,
                    ProtocolVersion = 1,
                    TickRate = 30
                };

                // Try to load player prefab from Resources/Assets or create template
                GameObject playerPrefab = Resources.Load<GameObject>("NetworkPlayer");
                if (playerPrefab == null)
                {
                    if (defaultPlayerPrefabTemplate == null)
                    {
                        defaultPlayerPrefabTemplate = CreateDefaultPlayerPrefabTemplate();
                    }
                    playerPrefab = defaultPlayerPrefabTemplate;
                }

                if (playerPrefab != null)
                {
                    netManager.NetworkConfig.Prefabs.Add(new NetworkPrefab
                    {
                        Prefab = playerPrefab
                    });
                }

                netManager.OnClientConnectedCallback += OnClientConnected;

                DontDestroyOnLoad(netManagerObj);
                Debug.Log("[LANSetup] NetworkManager & UnityTransport inicializados automáticamente.");
            }
            else
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }

            // Ensure LANDiscoveryManager exists
            if (LANDiscoveryManager.Instance == null)
            {
                GameObject discoveryObj = new GameObject("LANDiscoveryManager");
                discoveryObj.AddComponent<LANDiscoveryManager>();
                Debug.Log("[LANSetup] LANDiscoveryManager inicializado automáticamente.");
            }

            // Ensure LANNetworkMenu exists
            if (FindObjectOfType<LANNetworkMenu>() == null)
            {
                GameObject menuObj = new GameObject("LANNetworkMenu");
                menuObj.AddComponent<LANNetworkMenu>();
                Debug.Log("[LANSetup] LANNetworkMenu inicializado automáticamente.");
            }

            // Setup basic environment with URP materials
            SetupBasicEnvironment();
        }

        private static void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                if (NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId) != null)
                {
                    Debug.Log($"[LANSetup] El jugador clientId: {clientId} ya fue spawneado automáticamente por Netcode.");
                    return;
                }

                GameObject playerPrefab = defaultPlayerPrefabTemplate;
                if (playerPrefab == null)
                {
                    playerPrefab = Resources.Load<GameObject>("NetworkPlayer");
                }

                if (playerPrefab != null)
                {
                    Vector3 spawnPos = new Vector3(
                        Random.Range(-3f, 3f),
                        0f,
                        Random.Range(-3f, 3f)
                    );

                    GameObject playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
                    playerInstance.SetActive(true);

                    NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        netObj.SpawnAsPlayerObject(clientId, true);
                        Debug.Log($"[LANSetup] Jugador (CharacterController) spawneado manualmente para ClientId: {clientId}");
                    }
                }
            }
        }

        private static GameObject CreateDefaultPlayerPrefabTemplate()
        {
            GameObject rootPlayer = new GameObject("LANPlayerPrefab");
            rootPlayer.transform.position = Vector3.zero;

            CharacterController cc = rootPlayer.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0, 1.0f, 0);

            GameObject visualCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visualCapsule.name = "VisualCapsule";
            visualCapsule.transform.SetParent(rootPlayer.transform);
            visualCapsule.transform.localPosition = new Vector3(0, 1.0f, 0);
            visualCapsule.transform.localRotation = Quaternion.identity;
            visualCapsule.transform.localScale = Vector3.one;

            CapsuleCollider primitiveCollider = visualCapsule.GetComponent<CapsuleCollider>();
            if (primitiveCollider != null) Destroy(primitiveCollider);

            MeshRenderer rend = visualCapsule.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader == null) urpShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (urpShader == null) urpShader = Shader.Find("Sprites/Default");
                if (urpShader == null) urpShader = Shader.Find("Standard");

                Material mat = new Material(urpShader);
                mat.color = new Color(0.2f, 0.6f, 1.0f);
                rend.sharedMaterial = mat;
            }

            rootPlayer.AddComponent<NetworkObject>();
            rootPlayer.AddComponent<ClientNetworkTransform>();
            rootPlayer.AddComponent<NetworkPlayerController>();

            GameObject holder = new GameObject("PlayerPrefabTemplateHolder");
            rootPlayer.transform.SetParent(holder.transform);
            rootPlayer.SetActive(false);
            DontDestroyOnLoad(holder);

            return rootPlayer;
        }

        private static void SetupBasicEnvironment()
        {
            GameObject floor = GameObject.Find("GroundFloor");
            if (floor == null)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "GroundFloor";
                floor.transform.position = new Vector3(0, 0, 0);
                floor.transform.localScale = new Vector3(5, 1, 5); // 50x50 area
            }

            MeshRenderer floorRenderer = floor.GetComponent<MeshRenderer>();
            if (floorRenderer != null)
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader == null) urpShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (urpShader == null) urpShader = Shader.Find("Sprites/Default");
                if (urpShader == null) urpShader = Shader.Find("Standard");

                Material floorMat = new Material(urpShader);
                floorMat.color = new Color(0.18f, 0.22f, 0.28f); // Dark slate blue
                floorRenderer.sharedMaterial = floorMat;
            }

            if (RenderSettings.sun == null && FindObjectOfType<Light>() == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
                light.intensity = 1.2f;
            }
        }
    }
}
