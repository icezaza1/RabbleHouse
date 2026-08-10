using UnityEngine;

public class CopyMotion : MonoBehaviour
{
    public Transform targetLimb;
    public bool mirror;
    private ConfigurableJoint cj;
    private void Awake()
    {
        cj = GetComponent<ConfigurableJoint>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!mirror)
        {
            cj.targetRotation = targetLimb.rotation;
        }
        else
        {
            cj.targetRotation = Quaternion.Inverse(targetLimb.rotation);
        }
    }
}
