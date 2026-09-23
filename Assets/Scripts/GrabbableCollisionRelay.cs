using UnityEngine;

namespace RabbleHouse
{
    /// <summary>
    /// Forwards limb impacts to the character's root GrabbableObject, which owns
    /// the held/thrown state and damage settings for the whole ragdoll.
    /// </summary>
    public sealed class GrabbableCollisionRelay : MonoBehaviour
    {
        private GrabbableObject owner;

        public void Initialize(GrabbableObject grabbableObject)
        {
            owner = grabbableObject;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (owner != null)
                owner.HandleCollision(collision);
        }
    }
}
