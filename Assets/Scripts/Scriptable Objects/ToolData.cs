using UnityEngine;

[CreateAssetMenu(fileName = "ToolData", menuName = "Game/ToolData")]
public class ToolData : ScriptableObject
{
    [Header("Swing timing")]
    public float swingWindupTime = 0.5f;   // wind-up
    public float swingSwingTime = 0.5f;
    public float swingHoldTime = 0.5f;   // the throw itself / impact
    public float swingReturnTime = 0.5f;   // return to neutral
    public float swingAngle = 45;
    public Vector3 leftHandRotationOffset;

    [Header("Holding pose (target rotations while idle-held)")]
    public Vector3 rightUpperHold;   // right arm raised holding the gun
    public Vector3 rightLowerHold;
    public Vector3 leftUpperHold;
    public Vector3 leftLowerHold;

    [Header("Winding up pose (target rotations while preparing to swing melee weapon)")]
    public Vector3 rightUpperWindUp;
    public Vector3 rightLowerWindUp;
    public Vector3 leftUpperWindUp;
    public Vector3 leftLowerWindUp;

    [Header("Swing pose (target rotations for swinging melee weapon)")]
    public Vector3 rightUpperSwing;
    public Vector3 rightLowerSwing;
    public Vector3 leftUpperSwing;
    public Vector3 leftLowerSwing;

    [Header("Winding up Throw pose (target rotations while preparing to throw)")]
    public Vector3 throwWindUpRightUpper;
    public Vector3 throwWindUpRightLower;
    public Vector3 throwWindUpLeftUpper;
    public Vector3 throwWindUpLeftLower;

    [Header("Throwing pose (target rotations during throw animation)")]
    public Vector3 throwRightUpper;
    public Vector3 throwRightLower;
    public Vector3 throwLeftUpper;
    public Vector3 throwLeftLower;
}
