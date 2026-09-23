using UnityEngine;

public class ActiveRagdollMaster : MonoBehaviour
{
    [SerializeField] private GameObject animatedRig;
    private ActiveRagdollBone[] ragdollBones;
    private ActiveRagdollBalancer balancer; // Reference to the balancer script from earlier

    private ConfigurableJoint coreJoint;
    // Stores the default strengths of the hips
    private float savedSpringX;
    private float savedDamperX;
    private float savedSpringYZ;
    private float savedDamperYZ;

    /// <summary>Read-only access to the Animated_Character rig (used by PhysicCharacterController to pin it to the physics body).</summary>
    public GameObject AnimatedRig => animatedRig;

    private bool isFullRagdoll = false;

    void Start()
    {
        // Automatically gather all the tracking bone scripts in the hierarchy
        ragdollBones = GetComponentsInChildren<ActiveRagdollBone>();
        balancer = GetComponentInChildren<ActiveRagdollBalancer>();
        coreJoint = FindCoreJoint();

        // Save the muscle values
        savedSpringX = coreJoint.angularXDrive.positionSpring;
        savedDamperX = coreJoint.angularXDrive.positionDamper;
        savedSpringYZ = coreJoint.angularYZDrive.positionSpring;
        savedDamperYZ = coreJoint.angularYZDrive.positionDamper;
    }

    void Update()
    {

    }

    private ConfigurableJoint FindCoreJoint()
    {
        ConfigurableJoint[] bodies = GetComponentsInChildren<ConfigurableJoint>(true);
        foreach (var rb in bodies)
            if (rb.transform.name.Contains("Hips"))
                return rb;
        return bodies.Length > 0 ? bodies[0] : null;
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
            bone.TurnOnMuscleStrength(false);
        }

        // Set Core muscle strength
        // Scale Angular X Drive
        JointDrive xDrive = coreJoint.angularXDrive;
        xDrive.positionSpring = 0f;
        xDrive.positionDamper = 0f;
        coreJoint.angularXDrive = xDrive;

        // Scale Angular YZ Drive
        JointDrive yzDrive = coreJoint.angularYZDrive;
        yzDrive.positionSpring = 0f;
        yzDrive.positionDamper = 0f;
        coreJoint.angularYZDrive = yzDrive;
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
            bone.TurnOnMuscleStrength(true);
        }

        // Set Core muscle strength
        // Scale Angular X Drive
        JointDrive xDrive = coreJoint.angularXDrive;
        xDrive.positionSpring = savedSpringX;
        xDrive.positionDamper = savedDamperX;
        coreJoint.angularXDrive = xDrive;

        // Scale Angular YZ Drive
        JointDrive yzDrive = coreJoint.angularYZDrive;
        yzDrive.positionSpring = savedSpringYZ;
        yzDrive.positionDamper = savedDamperYZ;
        coreJoint.angularYZDrive = yzDrive;
    }
}
