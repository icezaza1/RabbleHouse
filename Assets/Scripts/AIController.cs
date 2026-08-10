using UnityEngine;

namespace RabbleHouse
{
    /// <summary>
    /// Placeholder AI brain. Attached to AI-controlled opponents.
    ///
    /// CURRENT STATE: the AI simply stands idle.
    /// Behaviour (moving to furniture, picking it up, chasing the player,
    /// throwing, dodging) is intentionally left for a later design session.
    ///
    /// PlayerController checks for this component BEFORE reading PlayerInput,
    /// so AI characters never consume keyboards/gamepads.
    /// </summary>
    [DisallowMultipleComponent]
    public class AIController : MonoBehaviour
    {
        [Header("AI Behaviour (TBD)")]
        [Tooltip("Reserved: how often the AI re-evaluates its plan.")]
        [SerializeField] private float decisionInterval = 0.25f;

        [Tooltip("Reserved: distance at which the AI notices the player.")]
        [SerializeField] private float awarenessRange = 6f;

        // Input values consumed by PlayerController.ReadAIInput().
        // The future AI brain sets these; today they stay idle/zero.
        public Vector2 MoveInput { get; private set; } = Vector2.zero;
        public bool GrabPressed { get; private set; }
        public bool GrabReleased { get; private set; }
        public bool PunchPressed { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool ThrowHeld { get; private set; }

        private void Update()
        {
            // TODO: AI behaviour. For now the opponent stands idle.
            ClearTransientInputs();
        }

        /// <summary>
        /// One-frame presses must be cleared every frame so they are not
        /// re-consumed by PlayerController on a later frame.
        /// </summary>
        private void ClearTransientInputs()
        {
            GrabPressed = false;
            GrabReleased = false;
            PunchPressed = false;
            JumpPressed = false;
        }
    }
}
