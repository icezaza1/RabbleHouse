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

    // Stores the original angular motion
    private ConfigurableJointMotion originalAngularX;
    private ConfigurableJointMotion originalAngularY;
    private ConfigurableJointMotion originalAngularZ;

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

        // Save the joint motions
        originalAngularX = joint.angularXMotion;
        originalAngularY = joint.angularYMotion;
        originalAngularZ = joint.angularZMotion;
    }

    void FixedUpdate()
    {
        // Calculate the target rotation based on the hidden animated rig
        joint.targetRotation = CopyRotation();
    }

    public void TurnOnMuscleStrength(bool toggle)
    {
        if (joint == null) return;

        if (!toggle)
        {
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;
            joint.enablePreprocessing = false;

            // Scale Angular X Drive
            JointDrive xDrive = joint.angularXDrive;
            xDrive.positionSpring = 0f;
            xDrive.positionDamper = 0f;
            joint.angularXDrive = xDrive;

            // Scale Angular YZ Drive
            JointDrive yzDrive = joint.angularYZDrive;
            yzDrive.positionSpring = 0f;
            yzDrive.positionDamper = 0f;
            joint.angularYZDrive = yzDrive;
        }
        else if (toggle)
        {
            joint.angularXMotion = originalAngularX;
            joint.angularYMotion = originalAngularY;
            joint.angularZMotion = originalAngularZ;
            joint.enablePreprocessing = true;

            // Scale Angular X Drive
            JointDrive xDrive = joint.angularXDrive;
            xDrive.positionSpring = savedSpringX;
            xDrive.positionDamper = savedDamperX;
            joint.angularXDrive = xDrive;

            // Scale Angular YZ Drive
            JointDrive yzDrive = joint.angularYZDrive;
            yzDrive.positionSpring = savedSpringYZ;
            yzDrive.positionDamper = savedDamperYZ;
            joint.angularYZDrive = yzDrive;
        }
    }

    private Quaternion CopyRotation()
    {
        return Quaternion.Inverse(targetAnimatedBone.localRotation) * initialRotation;
    }
}
