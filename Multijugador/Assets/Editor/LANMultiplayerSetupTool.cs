using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;

namespace MultiplayerLAN.Editor
{
    public class LANMultiplayerSetupTool : EditorWindow
    {
        [MenuItem("Tools/Multijugador/Abrir Ventana de Configuración", false, 1)]
        public static void OpenSetupWindow()
        {
            LANMultiplayerSetupTool window = GetWindow<LANMultiplayerSetupTool>("Configuración Multijugador");
            window.minSize = new Vector2(420, 380);
            window.Show();
        }

        [MenuItem("Tools/Multijugador/Configurar Escena Completa (1 Clic)", false, 2)]
        public static void ExecuteOneClickSetup()
        {
            PerformFullSetup();
        }

        [MenuItem("Tools/Multijugador/Limpiar Caché de Burst (Solucionar Error de Build)", false, 3)]
        public static void ClearBurstCache()
        {
            string burstCachePath = Path.Combine(Directory.GetCurrentDirectory(), "Library", "BurstCache");
            if (Directory.Exists(burstCachePath))
            {
                try
                {
                    Directory.Delete(burstCachePath, true);
                    Debug.Log("[MultiplayerTool] ¡Caché de Burst eliminada exitosamente! Se regenerará en la siguiente compilación.");
                    EditorUtility.DisplayDialog("Caché de Burst Limpiada", "La carpeta 'Library/BurstCache' fue eliminada con éxito.\n\nYa puedes reintentar el Build (File > Build Settings > Build).", "Aceptar");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MultiplayerTool] Error al eliminar BurstCache: {ex.Message}");
                }
            }
            else
            {
                Debug.Log("[MultiplayerTool] La carpeta BurstCache no existía o ya estaba limpia.");
                EditorUtility.DisplayDialog("Caché de Burst Limpiada", "No se encontró caché pendiente en Library/BurstCache.", "Aceptar");
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(15);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.7f, 1.0f) }
            };

            GUILayout.Label("SISTEMA MULTIJUGADOR ONLINE & LAN", headerStyle);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Esta herramienta configurará automáticamente en la escena activa:\n" +
                "• NetworkManager y UnityTransport (con PlayerPrefab asignado)\n" +
                "• RelayManager para conexión Online por Internet (Unity Relay - UGS)\n" +
                "• LANDiscoveryManager para auto-descubrimiento en Wi-Fi Local (UDP)\n" +
                "• Menú Dual Completo (MultiplayerNetworkMenu - Pestañas Online / LAN)\n" +
                "• Prefab de Jugador corregido con ClientNetworkTransform\n" +
                "• Suelo URP (sin color rosa), Luces y Cámara",
                MessageType.Info
            );

            GUILayout.Space(20);

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 45
            };

            if (GUILayout.Button("CONFIGURAR ESCENA CON 1 CLIC", buttonStyle))
            {
                PerformFullSetup();
            }

            GUILayout.Space(15);
            EditorGUILayout.LabelField("Acciones Individuales:", EditorStyles.boldLabel);

            if (GUILayout.Button("Crear solo Prefab de Jugador (NetworkPlayer.prefab)"))
            {
                CreateOrUpdatePlayerPrefab();
            }

            if (GUILayout.Button("Crear solo Entorno Básico (Suelo URP y Luz)"))
            {
                SetupEnvironment();
            }

            if (GUILayout.Button("Limpiar Caché de Burst (Solución Error de Build)"))
            {
                ClearBurstCache();
            }
        }

        public static void PerformFullSetup()
        {
            Debug.Log("[MultiplayerTool] Iniciando configuración de escena multijugador (Online + LAN)...");

            // 1. Create or update Player Prefab
            GameObject playerPrefab = CreateOrUpdatePlayerPrefab();

            // 2. Setup NetworkManager
            SetupNetworkManager(playerPrefab);

            // 3. Setup RelayManager
            SetupRelayManager();

            // 4. Setup LAN Discovery Manager
            SetupDiscoveryManager();

            // 5. Setup Network Menu UI (Dual Online + LAN)
            SetupNetworkMenu();

            // 6. Setup Scene Environment (Floor, Lighting, Camera)
            SetupEnvironment();

            // Mark Scene Dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Configuración Multijugador Completada",
                "La escena activa ha sido configurada exitosamente para Multijugador Online (Relay) y LAN.\n\n" +
                "• Servidor Online: Unity Relay + Authentication (Códigos de 6 letras).\n" +
                "• Red Local: UDP Broadcast (Códigos Hexadecimales / Auto-descubrimiento).\n" +
                "• Prefab asignado en: Assets/Prefabs/NetworkPlayer.prefab\n" +
                "• Movimiento fluido de cliente: ClientNetworkTransform.\n\n" +
                "¡Dale a Play en Unity para probar!",
                "Entendido"
            );
        }

        private static GameObject CreateOrUpdatePlayerPrefab()
        {
            string folderPath = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            string prefabPath = $"{folderPath}/NetworkPlayer.prefab";

            // Create Root GameObject
            GameObject rootPlayer = new GameObject("NetworkPlayer");

            // CharacterController setup on Root
            CharacterController cc = rootPlayer.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0, 1.0f, 0);

            // Create Child Visual Mesh
            GameObject visualCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visualCapsule.name = "VisualCapsule";
            visualCapsule.transform.SetParent(rootPlayer.transform);
            visualCapsule.transform.localPosition = new Vector3(0, 1.0f, 0);
            visualCapsule.transform.localRotation = Quaternion.identity;
            visualCapsule.transform.localScale = Vector3.one;

            // Remove Collider from child mesh so only CharacterController handles physics
            CapsuleCollider childCollider = visualCapsule.GetComponent<CapsuleCollider>();
            if (childCollider != null)
            {
                DestroyImmediate(childCollider);
            }

            // Material assignment
            MeshRenderer renderer = visualCapsule.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Shader playerShader = Shader.Find("Universal Render Pipeline/Lit");
                if (playerShader == null) playerShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (playerShader == null) playerShader = Shader.Find("Standard");

                Material mat = new Material(playerShader);
                mat.color = new Color(0.2f, 0.6f, 1.0f); // Default Blue
                renderer.sharedMaterial = mat;
            }

            // Add Netcode components to Root
            rootPlayer.AddComponent<NetworkObject>();
            rootPlayer.AddComponent<ClientNetworkTransform>();
            rootPlayer.AddComponent<NetworkPlayerController>();

            // Save as Prefab Asset
            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(rootPlayer, prefabPath);
            DestroyImmediate(rootPlayer);

            Debug.Log($"[MultiplayerTool] Prefab de jugador guardado en: {prefabPath}");
            return prefabAsset;
        }

        private static void SetupNetworkManager(GameObject playerPrefab)
        {
            NetworkManager netManager = FindObjectOfType<NetworkManager>();
            GameObject netManagerObj;

            if (netManager == null)
            {
                netManagerObj = new GameObject("NetworkManager");
                netManager = netManagerObj.AddComponent<NetworkManager>();
            }
            else
            {
                netManagerObj = netManager.gameObject;
            }

            UnityTransport transport = netManagerObj.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = netManagerObj.AddComponent<UnityTransport>();
            }

            if (netManager.NetworkConfig == null)
            {
                netManager.NetworkConfig = new NetworkConfig();
            }

            netManager.NetworkConfig.NetworkTransport = transport;
            netManager.NetworkConfig.ProtocolVersion = 1;
            netManager.NetworkConfig.TickRate = 30;

            if (playerPrefab != null)
            {
                netManager.NetworkConfig.Prefabs.Add(new NetworkPrefab
                {
                    Prefab = playerPrefab
                });

                SerializedObject serializedNetManager = new SerializedObject(netManager);
                SerializedProperty playerPrefabProp = serializedNetManager.FindProperty("NetworkConfig.PlayerPrefab");
                if (playerPrefabProp != null)
                {
                    playerPrefabProp.objectReferenceValue = playerPrefab;
                    serializedNetManager.ApplyModifiedProperties();
                }
            }

            Undo.RegisterCreatedObjectUndo(netManagerObj, "Setup NetworkManager");
        }

        private static void SetupRelayManager()
        {
            RelayManager relay = FindObjectOfType<RelayManager>();
            if (relay == null)
            {
                GameObject relayObj = new GameObject("RelayManager");
                relayObj.AddComponent<RelayManager>();
                Undo.RegisterCreatedObjectUndo(relayObj, "Setup RelayManager");
            }
        }

        private static void SetupDiscoveryManager()
        {
            LANDiscoveryManager discovery = FindObjectOfType<LANDiscoveryManager>();
            if (discovery == null)
            {
                GameObject discoveryObj = new GameObject("LANDiscoveryManager");
                discoveryObj.AddComponent<LANDiscoveryManager>();
                Undo.RegisterCreatedObjectUndo(discoveryObj, "Setup LANDiscoveryManager");
            }
        }

        private static void SetupNetworkMenu()
        {
            // Remove old LAN-only menu if exists
            LANNetworkMenu oldMenu = FindObjectOfType<LANNetworkMenu>();
            if (oldMenu != null)
            {
                Undo.DestroyObjectImmediate(oldMenu.gameObject);
            }

            MultiplayerNetworkMenu menu = FindObjectOfType<MultiplayerNetworkMenu>();
            if (menu == null)
            {
                GameObject menuObj = new GameObject("MultiplayerNetworkMenu");
                menuObj.AddComponent<MultiplayerNetworkMenu>();
                Undo.RegisterCreatedObjectUndo(menuObj, "Setup MultiplayerNetworkMenu");
            }
        }

        private static void SetupEnvironment()
        {
            // Ground Plane
            GameObject floor = GameObject.Find("GroundFloor");
            if (floor == null)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "GroundFloor";
                floor.transform.position = new Vector3(0, 0, 0);
                floor.transform.localScale = new Vector3(5, 1, 5); // 50x50 area
                Undo.RegisterCreatedObjectUndo(floor, "Setup GroundFloor");
            }

            // Assign URP Lit Material to GroundFloor so it's not magenta
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

            // Directional Light
            Light light = FindObjectOfType<Light>();
            if (light == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
                light.intensity = 1.2f;
                Undo.RegisterCreatedObjectUndo(lightObj, "Setup Light");
            }

            // Main Camera setup
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.position = new Vector3(0, 10, -14);
                mainCam.transform.rotation = Quaternion.Euler(35, 0, 0);
            }
        }
    }
}
