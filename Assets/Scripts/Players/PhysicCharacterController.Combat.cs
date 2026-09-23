using UnityEngine;
using System.Collections;

namespace RabbleHouse
{
    public partial class PhysicCharacterController
    {
        // --- LIGHT / HEAVY PUNCH ---
        private void HandlePunchCooldown()
        {
            if (punchCooldownTimer > 0f)
                punchCooldownTimer -= Time.deltaTime;
            if (swingCooldownTimer > 0f)
                swingCooldownTimer -= Time.deltaTime;
            if (heavyPunchCooldownTimer > 0f)
                heavyPunchCooldownTimer -= Time.deltaTime;
        }

        private void CheckLightPunchWindowExpiry()
        {
            if (leftArmWaitingForWindow || rightArmWaitingForWindow)
            {
                if (!isInLightPunchWindow)
                {
                    LUpperBoneScript.enabled = true;
                    LLowerBoneScript.enabled = true;
                    RUpperBoneScript.enabled = true;
                    RLowerBoneScript.enabled = true;
                    leftArmWaitingForWindow = false;
                    rightArmWaitingForWindow = false;
                    leftPunching = false;
                    rightPunching = false;
                    lightPunchActive = false;
                }
            }
        }

        private void HandleLightAttack()
        {
            if (isHeavyPunching) return;

            if (heldObject != null)
            {
                // Tool: delegate to ToolBehaviour
                if (activeTool != null)
                {
                    if (lightAttackPressed)
                    {
                        bool consumed = activeTool.OnToolLightAttack();
                        if (consumed) return;
                    }
                }

                // Existing: Small/Large object swing
                if (lightAttackPressed && swingCooldownTimer <= 0f)
                {
                    HandleHeldObjectSwing();
                }
                return;
            }

            // Unarmed: LightAttack = light punch
            if (currentState == CharacterState.Grabbing) return;

            // Determine which arm is next based on toggle direction
            bool nextIsLeft = !lightPunchDirection;

            // Check if that arm is free (not currently punching)
            bool armFree = nextIsLeft ? !leftPunching : !rightPunching;

            // Start a new punch if the button is pressed, the target arm is free, and cooldown is ready
            if (lightAttackPressed && armFree && punchCooldownTimer <= 0f)
            {
                StartLightPunch();
            }
        }

        private void HandleHeldObjectSwing()
        {
            if (isHeavyPunching) return;

            if (heldObject == null || swingCooldownTimer > 0f) return;
            swingCooldownTimer = heavyPunchCooldown;

            isHeavyPunching = true;
            StartCoroutine(HeldObjectSwingRoutine());
        }

        private IEnumerator HeldObjectSwingRoutine()
        {
            var activeTool = heldObject?.GetComponent<ToolBehaviour>();
            var toolProfile = activeTool?.ArmProfile;

            bool isOneHanded = activeTool != null ? activeTool.OneHanded : false;

            // Source swing timing from tool profile if available, else use controller defaults
            float swingWindup = toolProfile != null ? toolProfile.throwWindupTime : heavyTravelTime;
            float swingTarget = toolProfile != null ? toolProfile.throwSwingTime : heavyTravelTime / 2;
            float swingHold = toolProfile != null ? toolProfile.throwHoldTime : heavyHoldTime;
            float swingReturn = toolProfile != null ? toolProfile.throwReturnTime : heavyTravelTime;
            float swingAngle = toolProfile != null ? toolProfile.throwSwingAngle : smallObjectSwingAngle;

            // Use hip rotation for swing — like heavy punch but simpler
            hipRotationSuppressed = true;

            Quaternion hipStart = hipJoint.targetRotation;
            float swingYaw = swingAngle; // positive = rightward swing
            Quaternion windupRot = Quaternion.Euler(0, -swingYaw, 0);
            Quaternion swingRot = Quaternion.Euler(0, swingYaw, 0);
            Quaternion hipWindupRot = hipStart * windupRot;
            Quaternion hipSwingTarget = hipStart * swingRot;

            // Source arm poses from tool profile if available, else use default raised-arms
            // Right Arm Profile
            Quaternion rightUpperStart = toolProfile != null
                ? rightUpperArm.targetRotation   // current pose (already raised from RaiseArms)
                : rightUpperArm.targetRotation;
            Quaternion rightLowerStart = rightLowerArm.targetRotation;

            Quaternion rightUpperWindup = toolProfile != null
                ? Quaternion.Euler(toolProfile.rightUpperWindUp)
                : rightUpperArm.targetRotation;
            Quaternion rightLowerWindup = toolProfile != null
                ? Quaternion.Euler(toolProfile.rightLowerWindUp)
                : rightLowerArm.targetRotation;
            Quaternion rightUpperSwing = toolProfile != null
                ? Quaternion.Euler(toolProfile.rightUpperSwing)
                : rightUpperArm.targetRotation;
            Quaternion rightLowerSwing = toolProfile != null
                ? Quaternion.Euler(toolProfile.rightLowerSwing)
                : rightLowerArm.targetRotation;

            // Left Arm Profile
            Quaternion leftUpperStart = toolProfile != null
                ? leftUpperArm.targetRotation   // current pose (already raised from RaiseArms)
                : leftUpperArm.targetRotation;
            Quaternion leftLowerStart = leftLowerArm.targetRotation;

            Quaternion leftUpperWindup = toolProfile != null
                ? Quaternion.Euler(toolProfile.leftUpperWindUp)
                : leftUpperArm.targetRotation;
            Quaternion leftLowerWindup = toolProfile != null
                ? Quaternion.Euler(toolProfile.leftLowerWindUp)
                : leftLowerArm.targetRotation;
            Quaternion leftUpperSwing = toolProfile != null
                ? Quaternion.Euler(toolProfile.leftUpperSwing)
                : leftUpperArm.targetRotation;
            Quaternion leftLowerSwing = toolProfile != null
                ? Quaternion.Euler(toolProfile.leftLowerSwing)
                : leftLowerArm.targetRotation;

            // Rotate hip to windup angle
            float elapsed = 0f;
            float total = swingWindup;

            while (elapsed < total)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                hipJoint.targetRotation = Quaternion.Slerp(hipStart, hipWindupRot, t);
                rightUpperArm.targetRotation = Quaternion.Lerp(rightUpperStart, rightUpperWindup, t);
                rightLowerArm.targetRotation = Quaternion.Lerp(rightLowerStart, rightLowerWindup, t);
                if (!isOneHanded)
                {
                    leftUpperArm.targetRotation = Quaternion.Lerp(leftUpperStart, leftUpperWindup, t);
                    leftLowerArm.targetRotation = Quaternion.Lerp(leftLowerStart, leftLowerWindup, t);
                }

                yield return new WaitForFixedUpdate();
            }

            // Rotate windup to swing
            elapsed = 0f;
            total = swingTarget;
            bool swingHitDone = false;
            while (elapsed < total)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                hipJoint.targetRotation = Quaternion.Slerp(hipWindupRot, hipSwingTarget, t);
                rightUpperArm.targetRotation = Quaternion.Lerp(rightUpperWindup, rightUpperSwing, t);
                rightLowerArm.targetRotation = Quaternion.Lerp(rightLowerWindup, rightLowerSwing, t);

                if (!isOneHanded)
                {
                    leftUpperArm.targetRotation = Quaternion.Lerp(leftUpperWindup, leftUpperSwing, t);
                    leftLowerArm.targetRotation = Quaternion.Lerp(leftLowerWindup, leftLowerSwing, t);
                }
                // Swing impact lands at ~50% of the swing arc
                if (!swingHitDone && t >= 0.5f)
                {
                    if (heldObject != null)
                    {
                        heldObject.StartSwingDetection();
                        if (heldObject.Durability <= 0)
                            ReleaseObject();
                    }
                    swingHitDone = true;
                }
                yield return new WaitForFixedUpdate();
            }
            // Phase 2: Hold briefly
            elapsed = 0f;
            while (elapsed < swingHold)
            {
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            // Phase 3: Return to facing direction
            elapsed = 0f;
            total = swingReturn;
            while (elapsed < total)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                hipJoint.targetRotation = Quaternion.Slerp(hipSwingTarget, hipStart, t);
                rightUpperArm.targetRotation = Quaternion.Lerp(rightUpperSwing, rightUpperStart, t);
                rightLowerArm.targetRotation = Quaternion.Lerp(rightLowerSwing, rightLowerStart, t);
                if (!isOneHanded)
                {
                    leftUpperArm.targetRotation = Quaternion.Lerp(leftUpperSwing, leftUpperStart, t);
                    leftLowerArm.targetRotation = Quaternion.Lerp(leftLowerSwing, leftLowerStart, t);
                }

                yield return new WaitForFixedUpdate();
            }

            // Stop object-based collision detection
            if (heldObject != null)
            {
                heldObject.StopSwingDetection();
            }

            // Re-enable hip rotation. HandleRotation() (called every frame in Update for
            // Moving/Grabbing states) will smoothly reorient the hip toward currentMoveDir
            // at the held object's hipRotationResistance rate — no instant snap here.
            hipRotationSuppressed = false;
            swingCooldownTimer = heavyPunchCooldown;
            isHeavyPunching = false;
        }

        private void StartLightPunch()
        {
            // Determine which arm is about to punch based on toggle direction
            bool isLeft = !lightPunchDirection;
            // Mark that arm as punching
            if (isLeft) leftPunching = true;
            else rightPunching = true;

            lastLightPunchTime = Time.time;
            punchCooldownTimer = lightPunchCooldown;

            // Disable bone scripts so we can manually set target rotations
            LUpperBoneScript.enabled = false;
            LLowerBoneScript.enabled = false;
            RUpperBoneScript.enabled = false;
            RLowerBoneScript.enabled = false;

            // Call PerformPunch with the correct arm
            PerformPunch(isLeft);

            // Flip the direction for the next press
            lightPunchDirection = !lightPunchDirection;

            // Mark the punch as active (used elsewhere)
            lightPunchActive = true;

            // Deal damage if a target is in range — called once per punch
            CheckLightPunchHit();
        }

        /// <summary>Light punch damage check — small damage, no stun.</summary>
        private void CheckLightPunchHit()
        {
            // Light punch: pure damage, no disable effect
            CheckHit(punchDamage, punchForce, HitType.None, 0f, false);
        }

        private void PerformPunch(bool isLeft)
        {
            // Grab the appropriate profile for the arm
            ArmPunchProfile profile = isLeft ? leftArmProfile : rightArmProfile;

            // Capture the current target rotations (the wind-up pose)
            Quaternion startUpper = Quaternion.Euler(profile.windUpUpper.x, profile.windUpUpper.y, profile.windUpUpper.z);
            Quaternion startLower = Quaternion.Euler(profile.windUpLower.x, profile.windUpLower.y, profile.windUpLower.z);

            // Compute the target punch rotations using the stored profile values.
            Quaternion targetUpper = Quaternion.Euler(profile.punchUpper.x, profile.punchUpper.y, profile.punchUpper.z);
            Quaternion targetLower = Quaternion.Euler(profile.punchLower.x, profile.punchLower.y, profile.punchLower.z);
                                                  

            // Start the lerp coroutine
            StartCoroutine(PunchLerpRoutine(isLeft, startUpper, targetUpper, startLower, targetLower));
        }

        private IEnumerator PunchLerpRoutine(bool isLeft, 
            Quaternion startUpper,
            Quaternion targetUpper,
            Quaternion startLower,
            Quaternion targetLower)
        {
            // Get the joint references for the arm being punched
            ConfigurableJoint upperJoint = (isLeft) ? leftUpperArm : rightUpperArm;
            ConfigurableJoint lowerJoint = (isLeft) ? leftLowerArm : rightLowerArm;

            // --- Move from wind-up to punch rotation ---
            float elapsed = 0f;
            float total = punchTravelTime;
            while (elapsed < total)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                upperJoint.targetRotation = Quaternion.Slerp(startUpper, targetUpper, t);
                lowerJoint.targetRotation = Quaternion.Slerp(startLower, targetLower, t);
                yield return new WaitForFixedUpdate();
            }

            // --- Hold the apex briefly ---
            elapsed = 0f;
            while (elapsed < punchHoldTime)
            {
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            // --- Move back from punch to wind-up ---
            elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                upperJoint.targetRotation = Quaternion.Slerp(targetUpper, startUpper, t);
                lowerJoint.targetRotation = Quaternion.Slerp(targetLower, startLower, t);
                yield return new WaitForFixedUpdate();
            }

            // Clean-up: mark arm as finished
            if (isLeft)
            {
                leftPunching = false;
                leftArmWaitingForWindow = true;
            }
            else
            {
                rightPunching = false;
                rightArmWaitingForWindow = true;
            }
        }

        // --- HEAVY ATTACK ---
        public void HandleHeavyAttack()
        {
            if (isHeavyPunching) return;

            if (heldObject != null)
            {
                // Tool: delegate to ToolBehaviour first
                if (activeTool != null)
                {
                    if (heavyAttackPressed)
                    {
                        bool consumed = activeTool.OnToolHeavyAttack();
                        if (consumed) return;
                        // If not consumed, fall through to throw
                    }
                }

                // Small Object and Large Object
                if (heldGrabbableType != GrabbableType.LargeObject)
                {
                    // SmallObject: throw (mid-swing)
                    isHeavyPunching = true;
                    StartCoroutine(SmallObjectThrowRoutine());
                }
                else
                {
                    // LargeObject (or any non-small): drop immediately
                    ReleaseObject();
                }
                return;
            }

            // Unarmed: HeavyAttack = heavy punch
            if (lightPunchActive) return;
            if (punchCooldownTimer > 0f) return;
            if (heavyPunchCooldownTimer > 0f) return;

            heavyPunchLeftArm = !heavyPunchLeftArm;
            isHeavyPunching = true;
            StartCoroutine(HeavyPunchRoutine(heavyPunchLeftArm));
        }

        private void ThrowHeldObject()
        {
            if (heldObject == null) return;

            // Tell the object who threw it (for self-damage prevention)
            heldObject.SetThrower(this);

            // Release grab joints
            if (leftHandJoint != null) Destroy(leftHandJoint);
            if (rightHandJoint != null) Destroy(rightHandJoint);
            leftHandJoint = rightHandJoint = null;

            // Clean up tool
            if (activeTool != null)
            {
                activeTool.OnToolReleased();
                activeTool = null;
            }

            // Throw in character's forward direction
            Vector3 throwDir = coreRigidbody.transform.forward;
            throwDir.y = 0.2f;
            throwDir.Normalize();
            heldObject.ThrowByDirection(throwDir * throwForce);

            heldObject = null;
            heldGrabbableType = GrabbableType.SmallObject;
            SetState(CharacterState.Idle);
        }

        private IEnumerator HeavyPunchRoutine(bool isLeft)
        {
            ArmPunchProfile profile = isLeft ? leftHeavyProfile : rightHeavyProfile;
            Transform hand = isLeft ? leftHand : rightHand;
            GameObject swingEffect = isLeft ? vfxs?.leftSwingPrefab : vfxs?.rightSwingPrefab;

            // Capture wind-up hip rotation (rotate hips toward the punching arm)
            Quaternion hipStart = hipJoint.targetRotation;
            // Compute relative Y-rotation deltas from the current hip target
            float windupYaw = isLeft ? HipHookRotation : -HipHookRotation;
            float hookYaw = isLeft ? -HipHookRotation : HipHookRotation;
            Quaternion relativeWindupYaw = Quaternion.Euler(0, windupYaw, 0);
            Quaternion relativeHookYaw = Quaternion.Euler(0, hookYaw, 0);
            Quaternion hipWindupRot = hipStart * relativeWindupYaw;
            Quaternion hipHookRot = hipStart * relativeHookYaw;

            // Suppress movement-based hip rotation during heavy punch
            hipRotationSuppressed = true;

            // Disable bone scripts for the punching arm + raise the OTHER arm (wind-up pose)
            if (isLeft)
            {
                LUpperBoneScript.enabled = false;
                LLowerBoneScript.enabled = false;
            }
            else
            {
                RUpperBoneScript.enabled = false;
                RLowerBoneScript.enabled = false;
            }

            // Phase 1: Hip rotates to wind-up, arm goes to wind-up pose
            float elapsed = 0f;
            float total = heavyTravelTime;
            Quaternion startUpper = Quaternion.Euler(profile.windUpUpper);
            Quaternion targetUpper = Quaternion.Euler(profile.punchUpper);
            Quaternion startLower = Quaternion.Euler(profile.windUpLower);
            Quaternion targetLower = Quaternion.Euler(profile.punchLower);

            ConfigurableJoint upperJoint = isLeft ? leftUpperArm : rightUpperArm;
            ConfigurableJoint lowerJoint = isLeft ? leftLowerArm : rightLowerArm;

            while (elapsed < total)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                hipJoint.targetRotation = Quaternion.Slerp(hipStart, hipWindupRot, t);
                yield return new WaitForFixedUpdate();
            }

            // Phase 2: Hip hooks to opposite side, arm stays extended (hold) — THIS IS IMPACT
            elapsed = 0f;
            bool heavyHitDone = false;
            while (elapsed < heavyHoldTime)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / heavyHoldTime);

                hipJoint.targetRotation = Quaternion.Lerp(hipWindupRot, hipHookRot, t);
                upperJoint.targetRotation = Quaternion.Lerp(startUpper, targetUpper, t);
                lowerJoint.targetRotation = Quaternion.Lerp(startLower, targetLower, t);
                
                // Heavy hit lands at the very start of the hook phase
                if (!heavyHitDone && t >= 0.5f)
                {
                    // Heavy punch applies its configured HitType (Stun by default) with its chance
                    CheckHit(heavyPunchDamage, punchForce, heavyPunchHitType, heavyPunchEffectChance, true);
                    heavyHitDone = true;

                    if (swingEffect != null)
                        swingEffect.SetActive(true);
                }
                yield return new WaitForFixedUpdate();
            }

            // Phase 3: Return hip to neutral and arm back to wind-up
            elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                hipJoint.targetRotation = Quaternion.Slerp(hipHookRot, hipStart, t);
                upperJoint.targetRotation = Quaternion.Slerp(targetUpper, startUpper, t);
                lowerJoint.targetRotation = Quaternion.Slerp(targetLower, startLower, t);

                if (swingEffect != null)
                    swingEffect.SetActive(false);

                yield return new WaitForFixedUpdate();
            }

            // Re-enable bone scripts
            LUpperBoneScript.enabled = true;
            LLowerBoneScript.enabled = true;
            RUpperBoneScript.enabled = true;
            RLowerBoneScript.enabled = true;

            // Restore hip rotation to current facing direction
            hipRotationSuppressed = false;
            if (currentMoveDir != Vector3.zero)
            {
                hipJoint.targetRotation = Quaternion.Inverse(Quaternion.LookRotation(currentMoveDir));
            }

            isHeavyPunching = false;
            heavyPunchCooldownTimer = heavyPunchCooldown;
        }

        private IEnumerator SmallObjectThrowRoutine()
        {
            // Resolve the active tool's profile (null if holding SmallObject/LargeObject)
            var activeTool = heldObject?.GetComponent<ToolBehaviour>();
            var toolProfile = activeTool?.ArmProfile;

            // Source throw timing from tool profile if available, else use controller defaults
            float throwWindup = toolProfile != null ? toolProfile.throwWindupTime : heavyTravelTime;
            float throwSwing = toolProfile != null ? toolProfile.throwSwingTime : heavyTravelTime / 2;
            float throwHold = toolProfile != null ? toolProfile.throwHoldTime : heavyHoldTime;
            float throwReturn = toolProfile != null ? toolProfile.throwReturnTime : heavyTravelTime;
            float swingAngle = toolProfile != null ? toolProfile.throwSwingAngle : smallObjectSwingAngle;

            // Source arm poses from tool profile if available, else use default raised-arms
            Quaternion rightUpperStart = toolProfile != null
                ? rightUpperArm.targetRotation   // current pose (already raised from RaiseArms)
                : rightUpperArm.targetRotation;
            Quaternion rightLowerStart = rightUpperArm.targetRotation;

            Quaternion rightUpperWindup = toolProfile != null
                ? Quaternion.Euler(toolProfile.throwWindUpRightUpper) //toolProfile.throwWindUpRightUpper
                : Quaternion.Euler(0, 90, -60);
            Quaternion rightLowerWindup = toolProfile != null
                ? Quaternion.Euler(toolProfile.throwWindUpRightLower) //toolProfile.throwWindUpRightLower
                : Quaternion.Euler(330, 0, 0);
            Quaternion rightUpperThrow = toolProfile != null
                ? Quaternion.Euler(toolProfile.throwRightUpper) //toolProfile.throwRightUpper
                : Quaternion.Euler(0, 90, -90); // fallback swing-down pose
            Quaternion rightLowerThrow = toolProfile != null
                ? Quaternion.Euler(toolProfile.throwRightLower) //toolProfile.throwRightLower
                : Quaternion.Euler(270, 0, 0);

            // Wind-up → Swing → THROW (mid-swing) → Return
            hipRotationSuppressed = true;

            Quaternion hipStart = hipJoint.targetRotation;
            float swingYaw = swingAngle;
            Quaternion windupRot = Quaternion.Euler(0, -swingYaw, 0);
            Quaternion swingRot = Quaternion.Euler(0, swingYaw, 0);
            Quaternion hipWindupRot = hipStart * windupRot;
            Quaternion hipSwingTarget = hipStart * swingRot;
            
            // Rotate hip to windup angle
            float elapsed = 0f;
            float total = throwWindup;
            while (elapsed < total)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                hipJoint.targetRotation = Quaternion.Slerp(hipStart, hipWindupRot, t);
                rightUpperArm.targetRotation = Quaternion.Lerp(rightUpperStart, rightUpperWindup, t);
                rightLowerArm.targetRotation = Quaternion.Lerp(rightLowerStart, rightLowerWindup, t);
                yield return new WaitForFixedUpdate();
            }

            // Phase 2: Swing forward — at ~50% of this phase, THROW the object
            elapsed = 0f;
            total = throwSwing;
            bool objectThrown = false;
            while (elapsed < total)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                hipJoint.targetRotation = Quaternion.Slerp(hipWindupRot, hipSwingTarget, t);
                rightUpperArm.targetRotation = Quaternion.Lerp(rightUpperWindup, rightUpperThrow, t);
                rightLowerArm.targetRotation = Quaternion.Lerp(rightLowerWindup, rightLowerThrow, t);

                // Throw mid-swing (at 70% of the swing phase)
                if (!objectThrown && t >= 0.70f)
                {
                    ThrowHeldObject();
                    objectThrown = true;
                }
                yield return new WaitForFixedUpdate();
            }

            // Phase 3: Hold briefly
            elapsed = 0f;
            while (elapsed < throwHold)
            {
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            // Phase 4: Return to facing direction
            elapsed = 0f;
            total = throwReturn;
            while (elapsed < total)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                hipJoint.targetRotation = Quaternion.Slerp(hipSwingTarget, hipStart, t);
                rightUpperArm.targetRotation = Quaternion.Lerp(rightUpperThrow, rightUpperStart, t);
                rightLowerArm.targetRotation = Quaternion.Lerp(rightLowerThrow, rightLowerStart, t);
                yield return new WaitForFixedUpdate();
            }

            // Restore hip rotation to current facing
            hipRotationSuppressed = false;
            if (currentMoveDir != Vector3.zero)
            {
                hipJoint.targetRotation = Quaternion.Inverse(Quaternion.LookRotation(currentMoveDir));
            }
            ResetBothArms();
            isHeavyPunching = false;
        }

    }
}
