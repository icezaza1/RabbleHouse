using UnityEngine;

public class ActiveRagdollMaster : MonoBehaviour
{
    [SerializeField] private GameObject animatedRig;
    private ActiveRagdollBone[] ragdollBones;
    private ActiveRagdollBalancer balancer; // Reference to the balancer script from earlier

    /// <summary>Read-only access to the Animated_Character rig (used by PhysicCharacterController to pin it to the physics body).</summary>
    public GameObject AnimatedRig => animatedRig;

    private bool isFullRagdoll = false;

    void Start()
    {
        // Automatically gather all the tracking bone scripts in the hierarchy
        ragdollBones = GetComponentsInChildren<ActiveRagdollBone>();
        balancer = GetComponentInChildren<ActiveRagdollBalancer>();
    }

    void Update()
    {
        // Example: Press Spacebar to toggle between Active and Limp states, this is for debugging purpose
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isFullRagdoll)
            {
                EnableActiveRagdoll();
            }
            else
            {
                EnableFullRagdoll();
            }
        }
    }

    public void EnableFullRagdoll()
    {
        isFullRagdoll = true;

        // 1. Turn off the master balance force so the hips drop
        if (balancer != null) balancer.enabled = false;

        // 2. Shut off the animations entirely so the hidden rig stops moving
        if (animatedRig != null) animatedRig.SetActive(false);

        // 3. Drop all bone muscle strength to 0%
        foreach (var bone in ragdollBones)
        {
            bone.SetMuscleStrength(0f);
        }
    }

    public void EnableActiveRagdoll()
    {
        isFullRagdoll = false;

        // 1. Wake up the animated rig
        if (animatedRig != null) animatedRig.SetActive(true);

        // 2. Turn back on the hips balancer
        if (balancer != null) balancer.enabled = true;

        // 3. Restore bone muscle strength to 100%
        foreach (var bone in ragdollBones)
        {
            bone.SetMuscleStrength(1f);
        }
    }
}
