# Armed-Target AI Combat Logic (RabbleHouse)

## Overview
When the AI is holding an object and faces a target (armed or unarmed), it uses a strict state machine to decide behavior. This replaces the old loose "dodge/circle/retreat" fallback that accidentally fell through to unarmed combat even when the target was armed.

## Core Rules

1. **Never fall through to unarmed logic when target is armed**  
   All armed-target sub-states (`Bait`, `Back-Up`, `Charge`) must `return` immediately—no shared execution paths.

2. **State Machine**
   - **Bait**: Sprint toward target until within `baitDistance` or `baitEndTime` expires.
   - **Back-Up**: Retreat using `GetSafeRetreatDirection()` while maintaining safe distance.
   - **Charge**: Sprint in and heavy punch when in range, subject to `controller.HeavyPunchReady`.

3. **Armed Combat Checklist**
   - Ensure target is armed via `IsTargetArmed(target)` using `GetComponentInParent<PhysicCharacterController>()`.
   - Verify `controller.HeavyPunchReady` before initiating Charge.
   - Use distance-based bait (`distToTarget <= baitDistance`) rather than time-only timers.
   - Wall-aware retreat: Slide along walls via `GetSafeRetreatDirection()` instead of pushing into them.

4. **Distance‑Based Bait**
   - `baitDistance` is a configurable serialized float (default 3.5 meters).
   - When `distToTarget <= baitDistance`, force `isBaiting = true` and set `baitEndTime = Time.time` to transition to Phase B.
   - Cap `baitDuration` (max 2 seconds) to prevent infinite bait loops on unreachable targets.

5. **Wall‑Safe Retreat**
   ```csharp
   private Vector3 GetSafeRetreatDirection(Vector3 awayDir)
   {
       // Slide along walls instead of pushing into them
       if (Physics.Raycast(coreRb.position, awayDir, 1.5f, LayerMask.GetMask("Default", "Wall", "Environment")))
       {
           // Parallel to wall escape
           return Vector3.ProjectOnPlane(awayDir, hit.normal);
       }
       return awayDir.normalized;
   }
   ```

6. **Heavy Punch Gate**
   ```csharp
   if (controller.HeavyPunchReady && distToTarget < attackRange + (controller.HeldObject?.AttackRangeBonus ?? 0f))
   {
       // Initiate Charge → heavy punch
   }
   ```

## Behavior Flow
- **Bait → (reaches distance) → Back-Up or Charge transition**
- **Back-Up → (time expires) → Charge if still viable**
- **Charge → HeavyPunch when in range and ready**

## References
- `references/ai-input-pattern.md` – Core AI input contract and decision cycle.
- `references/ai-size-aware-attack-range.md` – Adapts attack range based on held object size.
- `references/ai-retreat-grab.md` – Grab detection and fallback logic.
- `references/active-ragdoll-system.md` – Physics and joint constraints for ragdoll characters.