using UnityEngine;
using UnityEngine.InputSystem;

namespace RabbleHouse
{
    /// <summary>
    /// Simple component to create a player character at runtime.
    /// Attach to an empty GameObject in the scene to spawn a test player.
    /// Set <c>isAI</c> to spawn an AI-controlled opponent instead.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(RagdollController))]
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerSpawner : MonoBehaviour
    {
        [Header("Player Settings")]
        [SerializeField] private int playerIndex = 0;
        [SerializeField] private string playerName = "Player";
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 15f;
        [SerializeField] private float jumpForce = 6f;
        [SerializeField] private float stunDuration = 2f;
        [SerializeField] private float knockdownDuration = 1.2f;
        [SerializeField] private float recoveryTime = 3f;

        [Header("Grab Settings")]
        [SerializeField] private float grabRange = 1.5f;
        [SerializeField] private float grabOffset = 0.3f;
        [SerializeField] private float throwForce = 20f;
        [SerializeField] private float maxThrowForce = 40f;
        [SerializeField] private float throwChargeTime = 1f;

        [Header("Punch")]
        [SerializeField] private float punchCooldown = 0.5f;
        [SerializeField] private float punchForce = 15f;
        [SerializeField] private float punchRange = 1f;
        [SerializeField] private int punchDamage = 10;

        [Header("Visuals")]
        [SerializeField] private Color playerColor = Color.blue;
        [SerializeField] private float height = 1.8f;
        [SerializeField] private float radius = 0.4f;

        [Header("AI")]
        [Tooltip("If true, this character is controlled by AIController instead of a device. " +
                 "PlayerInput is skipped so the AI never claims a keyboard/gamepad.")]
        [SerializeField] private bool isAI = false;

        private void Awake()
        {
            // Ensure we have all required components
            var controller = GetComponent<PlayerController>();
            if (controller == null)
                controller = gameObject.AddComponent<PlayerController>();

            if (!isAI)
            {
                var input = GetComponent<PlayerInput>();
                if (input == null)
                    input = gameObject.AddComponent<PlayerInput>();
            }

            var ragdoll = GetComponent<RagdollController>();
            if (ragdoll == null)
                ragdoll = gameObject.AddComponent<RagdollController>();

            var health = GetComponent<PlayerHealth>();
            if (health == null)
                health = gameObject.AddComponent<PlayerHealth>();

            if (isAI)
            {
                var ai = GetComponent<AIController>();
                if (ai == null)
                    ai = gameObject.AddComponent<AIController>();
            }

            // Apply settings to PlayerController
            ApplySettingsToController(controller);
        }

        private void ApplySettingsToController(PlayerController controller)
        {
            // We use reflection to set private fields since they're SerializeField
            // In practice, you'd want to set these in the inspector or use public properties
            // This is a helper for quick scene setup
            controller.PlayerIndex = playerIndex;
            controller.name = $"Player_{playerIndex}";
        }

        private void Start()
        {
            SetupVisuals();
        }

        private void SetupVisuals()
        {
            // Create a simple capsule visual
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();

            // Create capsule mesh
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var capsuleMesh = capsule.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(capsule);

            meshFilter.sharedMesh = capsuleMesh;

            // Create material
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = playerColor;
            meshRenderer.sharedMaterial = mat;

            // Scale to proper size
            transform.localScale = new Vector3(radius * 2, height * 0.5f, radius * 2);

            // Position capsule collider
            var capsuleCollider = GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
            {
                capsuleCollider.height = height;
                capsuleCollider.radius = radius;
                capsuleCollider.center = Vector3.up * height * 0.5f;
            }

            // Setup rigidbody
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = 3f;
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }

        private void OnValidate()
        {
            if (playerIndex < 0) playerIndex = 0;
            if (moveSpeed < 1f) moveSpeed = 1f;
            if (jumpForce < 1f) jumpForce = 1f;
        }

        [ContextMenu("Apply Settings to PlayerController")]
        private void ApplySettings()
        {
            var controller = GetComponent<PlayerController>();
            if (controller != null)
            {
                ApplySettingsToController(controller);
            }
        }
    }
}