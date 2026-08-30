using RabbleHouse;
using UnityEngine;

[System.Serializable]
public class ToolArmProfile
{
    [Header("Throw timing")]
    public float throwWindupTime = 0.5f;   // wind-up
    public float throwSwingTime = 0.5f;
    public float throwHoldTime = 0.5f;   // the throw itself / impact
    public float throwReturnTime = 0.5f;   // return to neutral
    public float throwSwingAngle = 45;

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

    [Header("Throwing pose (target rotations during throw animation)")]
    public Vector3 throwRightUpper;
    public Vector3 throwRightLower;
}

public abstract class ToolBehaviour : MonoBehaviour
{
    [Header("Arm Profile")]
    [SerializeField] private ToolArmProfile armProfile;
    public ToolArmProfile ArmProfile => armProfile;

    [Header("Generals")]
    [SerializeField] private bool oneHanded;
    public bool OneHanded => oneHanded;

    // Reference to the grab object (resolved in Awake)
    protected GrabbableObject grabObject;
    protected PhysicCharacterController holder;
    protected virtual void Awake()
    {
        grabObject = GetComponent<GrabbableObject>();
    }

    /// <summary>
    /// Called by PhysicCharacterController when tool is picked up.
    /// </summary>
    public virtual void OnToolGrabbed(PhysicCharacterController holder)
    {
        this.holder = holder;
    }
    /// <summary>
    /// Called by PhysicCharacterController when tool is dropped.
    /// </summary>
    public virtual void OnToolReleased()
    {
        this.holder = null;
    }
    /// <summary>
    /// Called when LightAttack is pressed while holding this tool.
    /// Returns true if the input was consumed.
    /// </summary>
    public abstract bool OnToolLightAttack();
    /// <summary>
    /// Called when HeavyAttack is pressed while holding this tool.
    /// Returns true if the input was consumed.
    /// </summary>
    public abstract bool OnToolHeavyAttack();
}
