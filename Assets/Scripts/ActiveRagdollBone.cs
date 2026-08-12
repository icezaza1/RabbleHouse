using UnityEngine;

public class ActiveRagdollBone : MonoBehaviour
{
    [SerializeField] private Transform targetAnimatedBone;
    private ConfigurableJoint joint;
    private Quaternion initialRotation;

    // Stores the default strengths you set up in the Inspector
    private float savedSpringX;
    private float savedDamperX;
    private float savedSpringYZ;
    private float savedDamperYZ;

    void Start()
    {
        joint = GetComponent<ConfigurableJoint>();
        // Store the starting rotation relative to the joint's connected body
        initialRotation = transform.localRotation;

        // Save the muscle values
        savedSpringX = joint.angularXDrive.positionSpring;
        savedDamperX = joint.angularXDrive.positionDamper;
        savedSpringYZ = joint.angularYZDrive.positionSpring;
        savedDamperYZ = joint.angularYZDrive.positionDamper;
    }

    void FixedUpdate()
    {
        // Calculate the target rotation based on the hidden animated rig
        joint.targetRotation = CopyRotation();
    }

    public void SetMuscleStrength(float percentage)
    {
        if (joint == null) return;

        // Scale Angular X Drive
        JointDrive xDrive = joint.angularXDrive;
        xDrive.positionSpring = savedSpringX * percentage;
        xDrive.positionDamper = savedDamperX * percentage;
        joint.angularXDrive = xDrive;

        // Scale Angular YZ Drive
        JointDrive yzDrive = joint.angularYZDrive;
        yzDrive.positionSpring = savedSpringYZ * percentage;
        yzDrive.positionDamper = savedDamperYZ * percentage;
        joint.angularYZDrive = yzDrive;
    }

    private Quaternion CopyRotation()
    {
        return Quaternion.Inverse(targetAnimatedBone.localRotation) * initialRotation;
    }
}
