using UnityEngine;
using UnityEngine.InputSystem;

namespace RabbleHouse
{
    /// <summary>
    /// Sets up PlayerInput component with action maps.
    /// Attach this to the player prefab.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputSetup : MonoBehaviour
    {
        [Header("Input Configuration")]
        [SerializeField] private InputActionReference[] additionalActions;

        private PlayerInput playerInput;
        private InputActionAsset actionAsset;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();

            // Ensure PlayerInput is configured
            if (playerInput.actions == null)
            {
                Debug.LogWarning("No action asset assigned to PlayerInput. Please assign one in the inspector.");
            }
        }

        public bool IsMoving()
        {
            if (playerInput?.actions?.FindAction("Move") == null) return false;
            return playerInput.actions.FindAction("Move").ReadValue<Vector2>().magnitude > 0.1f;
        }

        public bool IsGrabPressed()
        {
            if (playerInput?.actions?.FindAction("Grab") == null) return false;
            return playerInput.actions.FindAction("Grab").WasPressedThisFrame();
        }

        public bool IsGrabReleased()
        {
            if (playerInput?.actions?.FindAction("Grab") == null) return false;
            return playerInput.actions.FindAction("Grab").WasReleasedThisFrame();
        }

        public bool IsThrowHeld()
        {
            if (playerInput?.actions?.FindAction("Throw") == null) return false;
            return playerInput.actions.FindAction("Throw").IsPressed();
        }

        public bool IsPunchPressed()
        {
            if (playerInput?.actions?.FindAction("Punch") == null) return false;
            return playerInput.actions.FindAction("Punch").WasPressedThisFrame();
        }

        public bool IsJumpPressed()
        {
            if (playerInput?.actions?.FindAction("Jump") == null) return false;
            return playerInput.actions.FindAction("Jump").WasPressedThisFrame();
        }

        public Vector2 GetMoveInput()
        {
            if (playerInput?.actions?.FindAction("Move") == null) return Vector2.zero;
            return playerInput.actions.FindAction("Move").ReadValue<Vector2>();
        }

        public Vector2 GetLookInput()
        {
            if (playerInput?.actions?.FindAction("Look") == null) return Vector2.zero;
            return playerInput.actions.FindAction("Look").ReadValue<Vector2>();
        }
    }
}