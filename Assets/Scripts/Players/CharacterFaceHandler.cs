using UnityEngine;
using RabbleHouse;

public class CharacterFaceHandler : MonoBehaviour
{
    [SerializeField] private GameObject normalFace;
    [SerializeField] private GameObject damagedFace;
    [SerializeField] private GameObject deadFace;

    private PhysicCharacterController character;
    private PhysicCharacterController.CharacterState? displayedState;

    private void Awake()
    {
        character = GetComponentInParent<PhysicCharacterController>();
        UpdateFace();
    }

    private void Update()
    {
        UpdateFace();
    }

    private void UpdateFace()
    {
        var state = character != null
            ? character.CurrentState
            : PhysicCharacterController.CharacterState.Idle;

        if (displayedState == state)
            return;

        displayedState = state;

        if (normalFace != null)
            normalFace.SetActive(state != PhysicCharacterController.CharacterState.Ragdoll &&
                                 state != PhysicCharacterController.CharacterState.Dead);

        if (damagedFace != null)
            damagedFace.SetActive(state == PhysicCharacterController.CharacterState.Ragdoll);

        if (deadFace != null)
            deadFace.SetActive(state == PhysicCharacterController.CharacterState.Dead);
    }
}
