using UnityEngine;

public class PottedPlant : ToolBehaviour
{
    public override bool OnToolLightAttack()
    {
        if (grabObject.Durability <= 0) return false;
        return false;
    }

    public override bool OnToolHeavyAttack()
    {
        // Heavy = throw the tool (let PhysicCharacterController handle throw)
        return false;
    }
}
