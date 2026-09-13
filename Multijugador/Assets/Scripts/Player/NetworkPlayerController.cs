using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

namespace MultiplayerLAN
{
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayerController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 7.0f;
        [SerializeField] private float turnSpeed = 20.0f;
        [SerializeField] private float jumpHeight = 1.8f;
        [SerializeField] private float gravity = -22.0f;

        [Header("Visuals")]
        [SerializeField] private MeshRenderer playerRenderer;

        private CharacterController characterController;
        private Vector3 velocity;

        private readonly NetworkVariable<Color> playerColor = new NetworkVariable<Color>(
            Color.white,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private static readonly Color[] PresetColors = new Color[]
        {
            new Color(0.2f, 0.6f, 1.0f),
            new Color(1.0f, 0.4f, 0.4f),
            new Color(0.4f, 0.8f, 0.4f),
            new Color(1.0f, 0.8f, 0.2f),
            new Color(0.8f, 0.4f, 1.0f)
        };

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            if (playerRenderer == null)
            {
                playerRenderer = GetComponentInChildren<MeshRenderer>();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            playerColor.OnValueChanged += OnColorChanged;

            if (playerRenderer != null)
            {
                ApplyColor(playerColor.Value);
            }

            if (IsServer)
            {
                int colorIndex = (int)(OwnerClientId % (ulong)PresetColors.Length);
                playerColor.Value = PresetColors[colorIndex];
            }

            Debug.Log($"[NetworkPlayer] Jugador spawneado ID: {OwnerClientId} (Es Local: {IsOwner})");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            playerColor.OnValueChanged -= OnColorChanged;
        }

        private void Update()
        {
            if (!IsOwner) return;

            HandleMovement();
        }

        private void HandleMovement()
        {
            if (characterController == null) return;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector3 moveInput = new Vector3(horizontal, 0f, vertical).normalized;

            bool isGrounded = characterController.isGrounded;

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            if (moveInput.magnitude >= 0.1f)
            {
                characterController.Move(moveInput * moveSpeed * Time.deltaTime);

                Quaternion targetRotation = Quaternion.LookRotation(moveInput, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime
                );
            }

            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }

        private void OnColorChanged(Color previousValue, Color newValue)
        {
            ApplyColor(newValue);
        }

        private void ApplyColor(Color color)
        {
            if (playerRenderer != null)
            {
                playerRenderer.material.color = color;
            }
        }
    }
}
