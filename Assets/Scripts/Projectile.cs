using RabbleHouse;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Runtime (set by the firing tool)")]
    public int damage = 15;
    public PhysicCharacterController owner;          // who fired it (self-ignore)
    public HitType hitType = HitType.None;          // None = pure damage, no stun/knockdown
    public float effectChance = 0f;
    [Header("Tuning")]
    [SerializeField] private bool deleteOnHit = false;
    [SerializeField] private float lifeTime = 3f;   // auto-despawn
    [SerializeField] private float knockback = -1f; // -1 = target default
    private Rigidbody rb;
    private bool CanHit;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            //rb.useGravity = false;   // straight-line shot; set true for arcing weapons
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // avoid tunneling
        }
        CanHit = true;
        Destroy(gameObject, lifeTime);
    }
    /// <summary>Launch in a direction at a given speed. Call right after Instantiate.</summary>
    public void Launch(Vector3 velocity)
    {
        if (rb != null)
            rb.linearVelocity = velocity;
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (!CanHit) return;
        var targetHealth = collision.gameObject.GetComponentInParent<PlayerHealth>();
        if (targetHealth == null) return;
        // Don't hit the shooter

        if (owner != null && targetHealth.gameObject == owner.gameObject) return;

        Vector3 hitDir = collision.contacts[0].point - transform.position;

        if (hitDir == Vector3.zero) hitDir = rb != null ? rb.linearVelocity.normalized : transform.forward;
        hitDir = hitDir.normalized;
        hitDir.y = 0.3f; // slight upward pop, matches your thrown-object feel
        targetHealth.TakeDamage(damage, hitDir, hitType, effectChance, owner != null ? owner.PlayerIndex : -1);
        CanHit = false;
        var targetCtrl = targetHealth.GetComponentInParent<PhysicCharacterController>();
        if (targetCtrl != null)
            targetCtrl.ApplyKnockback(hitDir, knockback);

        if (deleteOnHit)
            Destroy(gameObject);
    }
}
