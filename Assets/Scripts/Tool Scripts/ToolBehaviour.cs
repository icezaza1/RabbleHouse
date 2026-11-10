using RabbleHouse;
using UnityEngine;

public abstract class ToolBehaviour : MonoBehaviour
{
    [Header("Tool Data")]
    [SerializeField] private ToolData toolData;
    public ToolData Data => toolData;

    [Header("General")]
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
