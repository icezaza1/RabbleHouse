using UnityEngine;

public class NarfGun : ToolBehaviour
{
    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;        // muzzle position
    [SerializeField] private float projectileSpeed = 30f;
    [SerializeField] private int projectileDamage = 15;
    [SerializeField] private float fireRate = 0.25f;

    private float nextFireTime;

    public override bool OnToolLightAttack()
    {
        if (Time.time < nextFireTime) return false;
        if (grabObject.Durability <= 0) return false;

        nextFireTime = Time.time + fireRate;

        // Spawn projectile from fire point
        GameObject proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        Projectile p = proj.GetComponent<Projectile>();
        if (p != null)
        {
            p.damage = projectileDamage;
            p.owner = holder;
            p.Launch(firePoint.forward * projectileSpeed);
        }

        // Reduce durability
        grabObject.ApplyDurabilityDamage(1);

        return true;
    }

    public override bool OnToolHeavyAttack()
    {
        // Heavy = throw the tool (let PhysicCharacterController handle throw)
        return false;
    }
}
