using UnityEngine;

namespace RabbleHouse
{
    public partial class AIInputHandler
    {
        private void HandleUnarmedCombat(Transform target, float distToTarget)
        {
            if (controller == null || target == null) return;

            bool targetIsArmed = IsTargetArmed(target);
            // ===================================================================
            //  ARMED TARGET — ONLY these three behaviors are allowed.
            // ===================================================================
            if (targetIsArmed)
            {
                // --- Currently in an active armed-target sub-state? ----------
                // (These states persist across frames so the AI commits to one
                //  behaviour instead of re-rolling every frame.)

                // 1) BAIT: sprint in until within baitDistance, then step away
                if (currentPhase == CombatPhase.BaitPhase1 && Time.time < baitEndTime)
                {
                    // Phase A: close distance to baitDistance — sprint toward target
                    SprintPressed = true;
                    HandleChasing(target, distToTarget);
                    // Transition to Phase B once we are close enough to bait
                    if (distToTarget <= baitDistance)
                    {
                        currentPhase = CombatPhase.BaitPhase2;
                        baitEndTime = Time.time; // force Phase B next frame
                    }
                    return;
                }
                if (currentPhase == CombatPhase.BaitPhase2 && Time.time >= baitEndTime)
                {
                    // Phase 2: step away briefly after reaching bait distance
                    //SprintPressed = false;
                    Vector3 awayDir = (coreRb.position - target.position).normalized;
                    awayDir.y = 0;
                    // Wall-aware retreat: if backing into a wall, slide along it
                    MoveInput = GetSafeRetreatDirection(awayDir);
                    // Following up with a charge if step back far enough
                    if (distToTarget > baitDistance * 1.5f)
                    {
                        currentPhase = CombatPhase.Charge;
                        chargeEndTime = Time.time + chargeDuration;
                    }
                    return;
                }

                // CHARGE: sprint in and heavy punch when in range. Then fall back
                if (currentPhase == CombatPhase.Charge && Time.time < chargeEndTime)
                {
                    float bonusRange = attackRange * 2f;
                    SprintPressed = true;
                    HandleChasing(target, distToTarget);
                    if (distToTarget < bonusRange && controller.HeavyPunchReady && Time.time >= nextAttackTime)
                    {
                        HeavyAttackPressed = true;
                        nextAttackTime = Time.time + attackCooldown;
                        scheduledRetreatTime = Time.time + retreatAfterChargeDelay; // schedule retreat AFTER punch
                    }
                    if (scheduledRetreatTime > 0f && Time.time >= scheduledRetreatTime)
                    {
                        //Retreat after a set timer after heavy punch
                        scheduledRetreatTime = -1f;
                        currentPhase = CombatPhase.None;
                        currentBehavior = AIBehavior.Retreat;
                        chargeEndTime = Time.time; // force end charge
                    }
                    return;
                }

                // --- No active sub-state: pick one based on chances ----------
                if (Random.value < baitChance)
                {
                    currentPhase = CombatPhase.BaitPhase1;
                    // Phase A ends either when close enough (baitDistance) or after this max time
                    baitEndTime = Time.time + 2f;
                    return;
                }

                if (Random.value < backingUpChance)
                {
                    currentBehavior = AIBehavior.Retreat;
                    return;
                }

                if (Random.value < chargeAgainstArmedChance)
                {
                    currentPhase = CombatPhase.Charge;
                    chargeEndTime = Time.time + chargeDuration;
                    return;
                }

                // Default fallback: back up
                currentBehavior = AIBehavior.Retreat;
                return;
            }

            // ===================================================================
            //  UNARMED TARGET — full combat personality.
            // ===================================================================
            else
            {
                // Currently dodging
                if (currentPhase == CombatPhase.Dodge)
                {
                    SprintPressed = true;
                    Vector3 awayFromTarget = (coreRb.position - target.position).normalized;
                    awayFromTarget.y = 0;
                    MoveInput = new Vector2(awayFromTarget.x, awayFromTarget.z).normalized;
                    if (Time.time >= dodgeEndTime) currentPhase = CombatPhase.None;

                    return;
                }

                // Currently circling
                if (currentPhase == CombatPhase.Circle)
                {
                    SprintPressed = true;
                    Vector3 toTarget = (target.position - coreRb.position).normalized;
                    Vector3 perp = Vector3.Cross(toTarget, Vector3.up) * circleDirection;
                    MoveInput = new Vector2(perp.x, perp.z).normalized;

                    // Wall collision during circling -> reverse direction
                    if (Physics.Raycast(coreRb.position, perp.normalized, 1.5f, LayerMask.GetMask("Default", "Wall", "Environment")))
                    {
                        circleDirection *= -1f;
                    }
                    if (Time.time >= circleEndTime) currentPhase = CombatPhase.None;

                    return;
                }

                // Currently charging against unarmed — same logic as armed charge:
                // keep the charge alive through the full heavy punch duration.
                if (currentPhase == CombatPhase.Charge && Time.time < chargeEndTime)
                {
                    SprintPressed = true;
                    HandleChasing(target, distToTarget);
                    if (distToTarget < attackRange && controller.HeavyPunchReady && Time.time >= nextAttackTime)
                    {
                        HeavyAttackPressed = true;
                        nextAttackTime = Time.time + attackCooldown;
                        scheduledRetreatTime = Time.time + retreatAfterChargeDelay; // schedule retreat AFTER punch
                    }
                    if (scheduledRetreatTime > 0f && Time.time >= scheduledRetreatTime)
                    {
                        //Retreat after a set timer after heavy punch
                        scheduledRetreatTime = -1f;
                        currentPhase = CombatPhase.None;
                        currentBehavior = AIBehavior.Retreat;
                    }
                    return;
                }

                // In combat range — decide action
                MoveInput = Vector2.zero;
                SprintPressed = false;

                // Retreat?
                if (Random.value < retreatChance)
                {
                    GrabbableObject nearbyObject = nearestGrabbableStatic;
                    if (nearbyObject != null)
                        currentBehavior = AIBehavior.Grab;
                    else
                        currentBehavior = AIBehavior.Retreat;
                    return;
                }

                // Dodge?
                if (Random.value < dodgeChance)
                {
                    SprintPressed = true;
                    currentPhase = CombatPhase.Dodge;
                    dodgeEndTime = Time.time + Random.Range(0.3f, 0.6f);
                    return;
                }

                // Circle?
                if (Random.value < circleChance)
                {
                    SprintPressed = true;
                    currentPhase = CombatPhase.Circle;
                    circleDirection = Random.value > 0.5f ? 1f : -1f;
                    circleEndTime = Time.time + Random.Range(1f, 2.5f);
                    return;
                }

                // Charge?
                if (Random.value < chargeChance)
                {
                    SprintPressed = true;
                    currentPhase = CombatPhase.Charge;
                    chargeEndTime = Time.time + chargeDuration;
                    return;
                }

                // Attack (light combo / heavy)
                if (Time.time >= nextAttackTime)
                {
                    if (Random.value < 0.6f)
                    {
                        LightAttackPressed = true;
                        nextAttackTime = Time.time + Random.Range(0.2f, 0.4f);
                    }
                    else
                    {
                        HeavyAttackPressed = true;
                        nextAttackTime = Time.time + Random.Range(minAttackInterval, maxAttackInterval);
                    }
                }
            }
        }

        /// <summary>
        /// Combat logic executed when the AI is unarmed.
        /// ARMED TARGET  -> Bait.
        /// UNARMED TARGET -> Chase, Throw.
        /// </summary>
        private void HandleArmedCombat(Transform target, float distToTarget)
        {
            if (controller == null || target == null) return;

            bool targetIsArmed = IsTargetArmed(target);
            float bonusRange = attackRange + (controller.HeldObject?.AIRangeBonus ?? 0f);

            float aiRange = controller.HeldObject?.AIRangeBonus ?? 0f;
            bool isRanged = aiRange >= 2f; // guns, ranged tools — melee objects have AIRangeBonus ~0-2
            // ===================================================================
            //  ARMED TARGET — ONLY these three behaviors are allowed.
            // ===================================================================
            if (targetIsArmed)
            {
                if (isRanged)
                {
                    HandleRangedFire(target, distToTarget);
                }
                else
                {
                    // --- MELEE OBJECT: existing behavior ---
                    if (currentPhase == CombatPhase.BaitPhase1 && Time.time < baitEndTime)
                    {
                        // Phase A: close distance to baitDistance — sprint toward target
                        HandleChasing(target, distToTarget);
                        // Transition to Phase B once we are close enough to bait
                        if (distToTarget <= baitDistance * 1.5f)
                        {
                            currentPhase = CombatPhase.BaitPhase2;
                            baitEndTime = Time.time; // force Phase B next frame
                        }
                        return;
                    }
                    if (currentPhase == CombatPhase.BaitPhase2 && Time.time >= baitEndTime)
                    {
                        // Phase 2: step away briefly after reaching bait distance
                        Vector3 awayDir = (coreRb.position - target.position).normalized;
                        awayDir.y = 0;
                        // Wall-aware retreat: if backing into a wall, slide along it
                        MoveInput = GetSafeRetreatDirection(awayDir);
                        // Following up with a charge if step back far enough
                        if (distToTarget > baitDistance * 1.8f)
                        {
                            currentPhase = CombatPhase.Charge;
                            chargeEndTime = Time.time + chargeDuration;
                        }
                        return;
                    }

                    // CHARGE: close in with object swing
                    if (currentPhase == CombatPhase.Charge && Time.time < chargeEndTime)
                    {
                        HandleChasing(target, distToTarget);

                        // Check swing readiness
                        if (IsTargetInMeleeRange(target) && controller.HeavyPunchReady && Time.time >= nextAttackTime && controller.SwingReady)
                        {
                            if (controller.SwingReady)
                            {
                                LightAttackPressed = true;
                                nextAttackTime = Time.time + attackCooldown;
                                scheduledRetreatTime = Time.time + retreatAfterChargeDelay; // schedule retreat AFTER punch
                            }
                        }
                        // Swing — stop charging, retreat after hit lands
                        if (scheduledRetreatTime > 0f && Time.time >= scheduledRetreatTime)
                        {
                            //Retreat after a set timer after heavy punch
                            scheduledRetreatTime = -1f;
                            currentPhase = CombatPhase.None;
                            currentBehavior = AIBehavior.Retreat;
                        }
                        return;
                    }

                    // --- No active sub-state: pick one based on chances ----------
                    if (Random.value < armedBaitChance && controller.HeavyPunchReady)
                    {
                        currentPhase = CombatPhase.BaitPhase1;
                        // Phase A ends either when close enough (baitDistance) or after this max time
                        baitEndTime = Time.time + 2f;
                        return;
                    }

                    // Step back if swing isn't ready
                    if (!controller.SwingReady)
                    {
                        currentBehavior = AIBehavior.Retreat;
                        return;
                    }
                }
            }
            // ===================================================================
            //  UNARMED TARGET — ONLY these three behaviors are allowed.
            // ===================================================================
            if (isRanged)
            {
                HandleRangedFire(target, distToTarget);
            }
            else
            {
                bool inMeleeRange = IsTargetInMeleeRange(target);
                // --- MELEE OBJECT: existing behavior ---
                if (inMeleeRange)
                {
                    if (controller.SwingReady && Time.time >= nextAttackTime)
                    {
                        LightAttackPressed = true; // Swing
                        nextAttackTime = Time.time + Random.Range(minAttackInterval, maxAttackInterval);
                        scheduledRetreatTime = Time.time + retreatAfterChargeDelay; // schedule retreat AFTER swing
                    }
                    if (scheduledRetreatTime > 0f && Time.time >= scheduledRetreatTime)
                    {
                        //Retreat after a set timer after heavy punch
                        scheduledRetreatTime = -1f;
                        currentPhase = CombatPhase.None;
                        currentBehavior = AIBehavior.Retreat;
                    }
                    return;
                }
                else if ((Time.time - objectGrabTime) > randomThrowHoldTime)
                {
                    // Close but held too long, or far — throw it
                    if (Time.time >= nextAttackTime)
                    {
                        HeavyAttackPressed = true; // Throw
                        nextAttackTime = Time.time + attackCooldown;
                        objectGrabTime = -1f;
                        return;
                    }
                }

                // Step back if swing isn't ready
                if (!controller.HeavyPunchReady)
                {
                    currentBehavior = AIBehavior.Retreat;
                    return;
                }
                // If target is close, stop trying to intercept
                HandleChasing(target, distToTarget);
            }
        }

        // --- SHARED RANGED FIRE LOGIC: called from both armed and unarmed contexts ---
        private void HandleRangedFire(Transform target, float distToTarget)
        {
            float aiRange = controller.HeldObject?.AIRangeBonus ?? 0f;

            float optimalRange = attackRange + aiRange * 0.7f;   // comfortable firing zone
            float minimumRange = attackRange + 1f;                // too close — back up
            bool canFire = false;

            if (distToTarget < minimumRange)
            {
                SprintPressed = true;
                currentBehavior = AIBehavior.Retreat;
                canFire = false;
            }
            else if (distToTarget > optimalRange)
            {
                HandleChasing(target, distToTarget);
                canFire = false;
            }
            else
            {
                canFire = true;
                MoveInput = Vector2.zero;

                // Face the target (rotate hips toward target)
                Vector3 toTarget = (target.position - coreRb.position).normalized;
                toTarget.y = 0;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toTarget);
                    MoveInput = new Vector2(toTarget.x, toTarget.z).normalized * 0.1f;
                }
            }

            if (Time.time >= nextAttackTime && controller.SwingReady && canFire)
            {
                LightAttackPressed = true;
                nextAttackTime = Time.time + Random.Range(minAttackInterval, maxAttackInterval);
            }
        }
    }
}
