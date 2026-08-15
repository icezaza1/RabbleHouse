using UnityEngine;
using UnityEngine.InputSystem;

namespace RabbleHouse
{
    /// <summary>
    /// Handles InputSystem input for Physic_Character.
    /// Designed to be easily testable and isolated from the main controller logic.
    /// </summary>
    public class PhysicInputHandler : MonoBehaviour
    {
        // --- INPUT ACTIONS ---
        private PlayerInput playerInput;

        // Public input values
        public Vector2 MoveInput => playerInput?.actions["Move"].ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 LookInput => playerInput?.actions["Look"].ReadValue<Vector2>() ?? Vector2.zero;
        public bool GrabPressed => playerInput?.actions["Grab"].WasPressedThisFrame() ?? false;
        public bool GrabReleased => playerInput?.actions["Grab"].WasReleasedThisFrame() ?? false;
        public bool LightPunchPressed => playerInput?.actions["LightPunch"].WasPressedThisFrame() ?? false;
        public bool HeavyPunchPressed => playerInput?.actions["HeavyPunch"].WasPressedThisFrame() ?? false;
        public bool JumpPressed => playerInput?.actions["Jump"].WasPressedThisFrame() ?? false;
        public bool SprintPressed => playerInput?.actions["Sprint"].IsPressed() ?? false;
        public bool ThrowHeld => playerInput?.actions["Throw"].IsPressed() ?? false;

        // --- LIFECYCLE ---
        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            // Enable input actions when this object becomes active
            if (playerInput != null)
                playerInput.ActivateInput();
        }

        private void OnDisable()
        {
            // Disable input when this object becomes inactive
            if (playerInput != null)
                playerInput.DeactivateInput();
        }

        // --- HELPERS ---
        public bool HasInputComponent()
        {
            return playerInput != null;
        }
    }
}