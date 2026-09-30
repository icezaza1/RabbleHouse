using UnityEngine;

namespace RabbleHouse
{
    public partial class AIInputHandler
    {
        // DECISION MAKING
        private void MakeDecision()
        {
            targetPlayer = FindNearestPlayer();

            // Only pick a new target object if we don't already have a valid one.
            // (Re-rolling every tick made the AI wander between objects.)
            if (nearestGrabbableStatic == null || nearestGrabbableStatic.IsHeld)
                nearestGrabbableStatic = FindRandomGrabbable();

            bool isHolding = controller != null && controller.IsHoldingObject;
            float distToTarget = targetPlayer != null ? Vector3.Distance(coreRb.position, targetPlayer.position) : float.MaxValue;

            // No alive targets found — idle/wander instead of attacking nothing
            if (targetPlayer == null)
            {
                currentBehavior = AIBehavior.Idle;
                MoveInput = Vector2.zero;
                SprintPressed = false;
                LightAttackPressed = false;
                HeavyAttackPressed = false;
                GrabPressed = false;
                return;
            }

            // Track object grab time and randomize throw timing (only when holding)
            if (isHolding && objectGrabTime < 0f)
            {
                objectGrabTime = Time.time;
                randomThrowHoldTime = Random.Range(minThrowHoldTime, maxThrowHoldTime);
            }

            if (isHolding)
            {
                // Armed vs armed/unarmed target — behavior selection extracted to helper below
                bool targetIsArmed = targetPlayer != null && IsTargetArmed(targetPlayer);
                float bonusRange = attackRange + (controller.HeldObject?.AIRangeBonus ?? 0f);
                if (targetIsArmed)
                {
                    // ---- ARMED AI vs ARMED TARGET ----
                    currentBehavior = AIBehavior.Attack; // HandleArmedCombat decides sub-behavior
                    return;
                }

                // Ranged weapons and melee objects can use their own physical/effective attack range.
                bool targetInRange = IsCurrentAttackTypeRanged() ? distToTarget <= GetEffectiveAttackRange(targetPlayer) : IsTargetInMeleeRange(targetPlayer);
                // ---- ARMED AI vs UNARMED TARGET ----
                if ((Time.time - objectGrabTime) > randomThrowHoldTime)
                    currentBehavior = AIBehavior.Throw;
                else if (targetInRange)
                    currentBehavior = AIBehavior.Attack;
                else if (targetPlayer != null && ((currentPhase != CombatPhase.BaitPhase1) || (currentPhase != CombatPhase.BaitPhase2)))
                    currentBehavior = AIBehavior.Chase;

                return;
            }
            else
            {
                // Reset object grab time when not holding
                objectGrabTime = -1f;

                bool targetIsArmed = targetPlayer != null && IsTargetArmed(targetPlayer);
                if (targetIsArmed && currentBehavior != AIBehavior.Grab) // If not heading for targeted object, go for unarmed combat state
                {
                    // ---- UNARMED AI vs ARMED TARGET ----
                    currentBehavior = AIBehavior.Attack; // HandleUnarmedCombat decides sub-behavior
                    return;
                }

                // ---- UNARMED AI vs UNARMED TARGET ----
                if (nearestGrabbableStatic != null && distToTarget > 1.5f)
                    currentBehavior = AIBehavior.Grab;
                else if (targetPlayer != null && distToTarget <= 1.5f)
                    currentBehavior = AIBehavior.Attack; // Only fight unarmed if really close
                else if (targetPlayer != null && ((currentPhase != CombatPhase.BaitPhase1) || (currentPhase != CombatPhase.BaitPhase2)))
                    currentBehavior = AIBehavior.Chase;
                else
                    currentBehavior = AIBehavior.Idle;
            }
        }


        // -----------------------------------------------------------------------
        //  DECISION HELPERS — armed-target behavior selection (holding an object)
        //  Extracted from MakeDecision() so the enum-state logic lives in one
        //  named place instead of inline. Returns the behavior; MakeDecision assigns it.
        // -----------------------------------------------------------------------
        private void ExecuteBehavior()
        {
            switch (currentBehavior)
            {
                case AIBehavior.Idle:
                    MoveInput = Vector2.zero;
                    break;

                case AIBehavior.Chase:
                    if (targetPlayer != null)
                    {
                        float distToTarget = Vector3.Distance(coreRb.position, targetPlayer.position);
                        bool targetIsArmed = IsTargetArmed(targetPlayer);

                        if (distToTarget > 0.5f)
                            SprintPressed = true;

                        HandleChasing(targetPlayer, distToTarget);
                    }
                    else
                        currentBehavior = AIBehavior.Idle;
                    break;

                case AIBehavior.Grab:
                    GrabbableObject grabbable = nearestGrabbableStatic;
                    if (grabbable != null)
                    {
                        float distToGrabbable = Vector3.Distance(coreRb.position, grabbable.transform.position);
                        if (distToGrabbable > 1f)
                            SprintPressed = true;
                        HandleChasing(grabbable.transform, distToGrabbable);
                        if (Time.time >= nextGrabTime && distToGrabbable < grabRange)
                        {
                            GrabPressed = true;
                            nextGrabTime = Time.time + grabCooldown;
                            objectGrabTime = Time.time;
                            randomThrowHoldTime = Random.Range(minThrowHoldTime, maxThrowHoldTime);
                            // Force transition to Chase — don't wait for MakeDecision to catch up
                            currentBehavior = AIBehavior.Chase;
                        }
                    }
                    else
                    {
                        currentBehavior = AIBehavior.Chase;
                    }
                    break;
                case AIBehavior.Attack:
                    if (targetPlayer != null)
                    {
                        float distToTarget = Vector3.Distance(coreRb.position, targetPlayer.position);
                        if (!controller.IsHoldingObject)
                        {
                            // Unarmed combat behaviors
                            HandleUnarmedCombat(targetPlayer, distToTarget);
                        }
                        else
                        {
                            // Holding object — swing when close, throw when too long
                            HandleArmedCombat(targetPlayer, distToTarget);
                        }
                    }
                    else
                    {
                        currentBehavior = AIBehavior.Idle;
                    }
                    break;

                case AIBehavior.Retreat:
                    SprintPressed = true;
                    Transform threat = FindNearestPlayer();
                    GrabbableObject retreatTarget = FindRetreatGrabbable(threat);
                    if (threat != null)
                    {
                        bool targetIsArmed = IsTargetArmed(threat);

                        // Chance to seek a random object while retreating
                        if (Random.value < retreatGrabChance && !controller.IsHoldingObject)
                        {
                            if (retreatTarget != null)
                            {
                                currentBehavior = AIBehavior.Grab;
                                break;
                            }
                        }

                        // Default: retreat straight away from threat
                        Vector3 away = (coreRb.position - threat.position).normalized;
                        MoveInput = GetSafeRetreatDirection(away);
                    }
                    else
                        currentBehavior = AIBehavior.Idle;
                    break;

                case AIBehavior.Throw:
                    if (targetPlayer != null)
                    {
                        MoveInput = Vector2.zero;
                        if (Time.time >= nextAttackTime)
                        {
                            HeavyAttackPressed = true;
                            nextAttackTime = Time.time + attackCooldown;
                            objectGrabTime = -1f;
                        }
                    }
                    else
                        currentBehavior = AIBehavior.Idle;
                    break;
            }
        }

        // -----------------------------------------------------------------------
    }
}
