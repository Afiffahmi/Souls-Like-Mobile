# Enemy AI integration

Implemented in `D:/GameMobile/Souls-Like-Mobile/Assets/EnemyAI/` and configured in `Assets/main_scene_test.unity`.

## Ready to use

- Prefab: `Assets/EnemyAI/Prefabs/Enemy1OneHand.prefab`
- Profile: `Assets/EnemyAI/Profiles/Enemy1OneHand.asset`
- Controller: `Assets/EnemyAI/Animations/Enemy1OneHand.controller`
- Attacks: `Assets/EnemyAI/Attacks/Attack1.asset` and `Attack2.asset`
- Scene object: `enemy_1_onehand`
- Navigation: `Enemy Navigation`, with saved NavMesh data in `Assets/EnemyAI/Navigation/`
- Scene backup before integration, including the previously unsaved edits: `Assets/EnemyAI/Backups/main_scene_test_before_enemy_ai.unity`

The imported One_hand_up Idle, Walk, Run, Hit, Die and Attack1/Attack2/Attack3 clips are connected. The enemy FBX uses a valid Humanoid Avatar so these clips can retarget onto its bones. All three melee attacks are enabled. **Hit plays to its final frame** (the current clip is about 0.733 seconds at 1× speed) before the enemy can resume locomotion or attacking. Repeated damage still reduces health but does not restart the reaction. The player escaping during Hit queues a return home after the reaction finishes. Death can interrupt immediately.

This character's damage windows use the zero-based frame numbers on Unity's clip timeline at 30 FPS:

| Attack | Start frame | End frame | Clip time at 1× speed |
|---|---:|---:|---|
| Attack1 | 14 | 19 | 0.467–0.633 s |
| Attack2 | 9 | 14 | 0.300–0.467 s |
| Attack3 | 9 | 14 | 0.300–0.467 s |

The slash is visible from the start frame until the end frame (end excluded). For this enemy, **Damage At Window End** is enabled: the visible window allows a timed parry, and damage resolves once when animation time reaches/crosses the end frame. A successful parry cancels the swing immediately. Holding the guard pose blocks the final damage; releasing it before resolution does not. Otherwise an in-range player receives the configured damage. The player must have been in valid range during the window and still be in range, within the attack arc, and unobstructed at resolution. A completely skipped window cannot produce an invisible hit. Interrupted/dead attackers cannot apply pending damage. Other profiles retain their own timing mode.

## Reusable enemy sword slash

### Windup speed

Each **EnemyAttackDefinition > Windup Speed Multiplier** slows only the animation before **Hit Start Frame** (or normalized **Hit Start** when frame mode is disabled). `1` means normal speed; `0.65` is 65% speed; `0.5` is half speed. It multiplies the attack's base **Playback Speed**. At the start frame, the animation returns to base speed for the slash and recovery.

The current enemy's Attack1/2/3 use `0.65`. For an attack window of frames 8–13, the windup before frame 8 is slower, the slash runs at normal speed from frame 8, and damage still resolves at frame 13. Slowdown changes elapsed time, not the authored frame numbers. Hit, Die, locomotion, and the global stun/pause speed are unaffected.

Changing the multiplier or Hit Start Frame applies without rebuilding. New/custom enemy controllers must be built with **Rebuild Controller from Profile** once to add the attack speed parameter; the current enemy controller is already updated. Shared attack assets share settings, so duplicate the attack asset when an enemy needs its own speed.

The scene enemy and `Enemy1OneHand` prefab have **EnemySlashVisual**. Their attacks share `Assets/EnemyAI/Prefabs/Shared Normal Sword Slash.prefab`, using the same crescent renderer as the player's normal sword. Each enemy caches its own visual instance and reuses it across swings. Parry, Hit interruption, death, disabling, and leaving the attack state clear it immediately.

On each **EnemyAttackDefinition**, **Hit Start Frame / Hit End Frame** are the authoritative variables for both the slash and attack window. Enable **Use Frame Window** and **Damage At Window End**, assign **Slash Prefab**, and enable **Slash**. Offset, rotation, size, sweep direction, and follow settings are per attack; the slash settings' separate start/end fields are ignored for enemies. Attack1 uses 14–19; Attack2 and Attack3 use 9–14. Reuse the component and shared prefab for other AI enemies with their own attack assets and frame values. Changing these window values does not require rebuilding the Animator.

**PlayerEnemyTarget > Parry Enabled** uses the player's existing timed parry input. **Block While Holding Guard** enables defense after the timing window expires, while the guard button and pose remain held. Rolling retains its existing evade behavior. Damage still goes through PlayerOverall and elemental armor.

**Hit timing:** a playable Hit clip always completes before transition. To shorten the visible reaction, edit/trim the clip or adjust the Animator Hit state's playback speed; the AI still waits for the end. **Hit Fallback Milliseconds** now applies only when there is no playable Hit clip. `0` uses Hit Fallback Duration instead. Elemental stun can hold an enemy longer. Assign clips through the profile, then rebuild the controller to keep both in sync.

## Enemy health bar

The prefab and current scene enemy have an **Enemy Health Bar** child: a red rectangular fill, dark background and border. It faces the camera and follows a fixed offset above the enemy, without text, handles or input blocking. It uses the existing Enemy/HealthBar components, so damage and healing update it directly. Death empties and hides it.

Edit the bar's RectTransform width/height for size, the HealthBar Gradient for fill color, and EnemyHealthBarBillboard **World Offset** for height (currently 3.15 world metres above the root). A pre-change scene copy is stored at `Assets/EnemyAI/Backups/before_healthbar_hit_completion.unity`.

## Player third-strike Hit reaction

On the scene **Player > Gem Sword Combat > Combo Finisher**, **Finisher Hit Reaction Only** is enabled and **Hit Reaction Attack Number** is 3. The player's light and heavy sword chains use their actual combo step, not a lifetime hit counter. Strikes 1 and 2 reduce health without the ordinary Hit reaction. A connected third strike requests the full Hit animation on a surviving enemy, in place. The added physical finisher push has been removed. Missing the third swing causes no reaction. Each enemy is affected once per swing even with multiple colliders. A lethal hit plays Die instead.

Existing elemental knockback/stun abilities remain independent; elemental hits may still apply their configured effects. Special attacks retain their existing reaction behavior. Disable Finisher Hit Reaction Only to restore ordinary reactions on each sword hit.

Burn, Poison and Void damage ticks reduce health and update the health bar without requesting a Hit reaction. This prevents a Fire first strike's lingering burn from bypassing the third-strike rule or restarting Hit after the finisher. The equipped gem can be restored from PlayerPrefs when entering Play mode, so test the saved elemental selection as well as Normal.

The animation repair also corrected Idle, which had been assigned the death animation, and replaced the profile/attack assets' missing references to deleted placeholders. A pre-repair scene copy is saved as `Assets/EnemyAI/Backups/before_animation_rig_fix.unity`.

## Replace animations

1. Import your clips with a rig compatible with the enemy model. Assign an appropriate Avatar to the model Animator if the clips use Humanoid retargeting. Generic clips must have matching transform paths relative to `Visual`.
2. Assign Idle, Walk, Run, Hit and Die on the profile. Assign Attack1/Attack2 on their attack assets.
3. Enable looping on locomotion clips. Disable looping on attacks, Hit and Die. Root motion is disabled because NavMeshAgent owns movement.
4. Select the scene enemy and press **Rebuild Controller from Profile** in the EnemyBrain Inspector. Rebuild after changing a clip, attack state name, or playback speed. This updates only that enemy controller.
5. On each attack, enable **Use Frame Window** and edit **Hit Start Frame / Hit End Frame**. Enable **Damage At Window End** to offer the slash window for parries and resolve damage at its end. With it disabled, the first eligible contact in the window resolves immediately. **Turn Until** remains normalized animation time. Range, facing, and obstruction are checked at impact. Animation speed changes are naturally reflected by Animator time; there is no independent damage timer.

To add another attack, create an **Enemies > Attack Definition** asset, give it a unique state name, assign its animation and effect, enable Available, add it to the profile's Attacks array, and rebuild. Ranged, Cast and Ability remain disabled future slots. Enable them only after filling the required fields. Run has its own controller slot, but current movement uses Walk until a sprint behavior is added.

## Inspector settings

| Setting | Where |
|---|---|
| Detection radius, home chase radius, sight rules | EnemyProfile |
| Speed, acceleration, turn speed, path refresh interval | EnemyProfile |
| Maximum/current health and optional health bar | Existing Enemy component |
| Attack range, damage, cooldown, weight, priority, health conditions | Each EnemyAttackDefinition |
| Available attacks and global recovery | EnemyProfile |
| Agent radius, height, area mask, avoidance quality | NavMeshAgent |
| Player roll evasion and parry window | PlayerEnemyTarget on Player |

Current configuration: 12 m detection, 22 m home territory, 2.8 m/s movement, **1,000 health for testing**. To change health, exit Play mode, select the root `enemy_1_onehand`, expand **Enemy > Health**, and set both **Max Health** and **Current Health** to the desired starting value. Changing only Max Health does not refill an already positive Current Health value. Apply the health overrides to the prefab if you want the same starting values on future instances. The current scene enemy and prefab both have 1,000/1,000 HP.

Attack1: 2.3 m, 12 damage, 2 s cooldown. Attack2: 2.6 m, 20 damage, 3 s cooldown. Attack3: 2.5 m, 25 damage, 4 s cooldown. Global recovery is 0.65 s after a completed attack. Hit waits for its actual animation to finish. Attack cooldown starts at attack initiation, so interrupted attacks retain their individual cooldown.

Selection first filters by range, cooldown, enabled/configured state and enemy health fraction. Higher priority wins; equal-priority attacks are weighted randomly. Immediate repeats are avoided when another equally prioritized attack is eligible. Minimum range supports ranged spacing; hybrids can cover the close-range gap with a melee attack. Custom boss sequences can use EnemyAttackSelector.

The chase radius is measured from the original spawn position. The enemy keeps pursuing its living target while both remain in that territory, including after losing sight or encountering a blocked path. Sight is still required for damage and optionally for acquiring a target. Stuck Path Retry Seconds (formerly Unreachable Timeout) resets a blocked route without dropping the target. Leaving the territory or target death starts a return to spawn; an eligible player within Detection Radius can be reacquired during the return. Health does not automatically reset, and an unreachable home is retried without teleporting through obstacles.

Ordinary Enemy.TakeDamage(damage) calls preserve walking and attacks. Use TakeDamage(damage, true) for an explicit knockback/finisher Hit reaction. ElementalDamage.Hit infers a reaction from knockback unless the caller supplies explicit combo permission; stun and root effects keep their own control rules. Interrupted or missing Hit animation playback recovers safely instead of trapping the AI in TakingDamage.

## Reuse for a new enemy

1. Duplicate the prefab and profile. Duplicate attack assets if this enemy needs different damage, clips or timing; shared assets intentionally affect every user.
2. Select the new profile, then **Tools > Enemy AI > Create Controller for Selected Profile**. Assign the resulting controller to the new model Animator.
3. Replace the Visual child with the new model, and assign that Animator to EnemyAnimationDriver. Keep the AI root at unit scale and the feet at the root's ground position. Assign the profile and a chest/weapon attack origin.
4. Configure existing Enemy health, CapsuleCollider, LockOnTarget, and NavMeshAgent dimensions. Use one movement owner; do not add a dynamic Rigidbody or CharacterController to the enemy.
5. Place the prefab on a baked NavMesh with a compatible agent type. Rebuild the controller after animation configuration.

The current scene has a local 70 × 16 × 70 m NavMesh volume around the enemy, using physics colliders and the default agent type. Expand/rebake the Enemy Navigation surface when the playable area or collision geometry changes. No links or dynamic obstacles were present or added. The existing player spawn remains unchanged; its controller falls onto the ground as before. The enemy was moved down onto the walkable plane.

## Modules and extension points

- **EnemyBrain:** state orchestration, detection, home territory, per-instance cooldowns and attack selection.
- **EnemyNavMeshMotor:** acceleration, path updates, stopping and smooth rotation.
- **EnemyAnimationDriver:** Animator crossfades and actual normalized state time. Animator is kept updating offscreen so combat timing remains reliable.
- **EnemyProfile / EnemyAttackDefinition:** shared, read-only runtime configuration.
- **EnemyAttackEffect:** stateless ScriptableObject effect, executed once at the hit point. Implement custom area attacks, summons, spells or abilities here.
- **EnemyMeleeEffect:** single-target range/arc/line-of-sight damage.
- **EnemyProjectileEffect / EnemyProjectile:** reusable straight-line projectile/spell delivery with swept collision, nearest obstacle handling, owner filtering and lifetime. Assign a visual projectile prefab containing EnemyProjectile. Projectile spawning currently uses Instantiate/Destroy; use pooling for high projectile counts. Ballistics, homing and area explosions are extension work.
- **EnemyAttackSelector:** optional custom boss/sequence selection. Keep mutable phase counters on an enemy component, not a shared ScriptableObject. Health ranges and priorities support basic phases without code.
- **EnemyTarget / PlayerEnemyTarget:** registered target contract and adapter into existing PlayerOverall. Health damage still passes through the existing gem armor calculation. Configurable roll evasion and parry use existing PlayerStateManager state.
- **EnemyControlStatus / ElementalEnemyControl:** adapter for the existing elemental stun, root and slow system. Leave ElementalEnemy's `interruptOnStun` empty for this prefab: the adapter handles interruption without disabling the health/AI subscription lifecycle.

Player movement, player combat scripts, player Animator, and lock-on scripts were not edited. Only PlayerEnemyTarget was added to the player scene object. Existing sword and arrow damage find the prefab's existing Enemy component, so its health remains the single source of truth. Death makes LockOnTarget unavailable and disables enemy colliders/navigation.

Detection and path refresh are throttled; detection starts are staggered across enemies. Hot-loop sight queries use a fixed buffer and fail closed if it fills. There are no repeated scene searches in the enemy AI. Profile/attack assets do not hold mutable per-enemy state. For large populations, profile on your target mobile device and adjust detection/path intervals and agent avoidance quality. This implementation has not been crowd-benchmarked on a mobile device.

## Validation

Run **Tools > Enemy AI > Run Play Mode Validation** from the configured main scene while in Edit mode. It temporarily controls the player during Play mode, tests the real prefab and existing health adapter, and automatically returns to Edit mode. Runtime changes are discarded. Results are saved in `Library/EnemyAIValidation.txt`.

Checks cover NavMesh placement, idle, obstructed detection, pursuit, Attack1/Attack2, no early damage, one hit per animation, renewed pursuit, damage interruption, stale-hit suppression, return-home behavior, damage during return, elemental stun, death, and lock-on release. Actual imported animation poses/contact frames must be reviewed after the real clips are assigned. Future projectile/boss variants should receive their own gameplay tests.

Unity API references: [Animator state time](https://docs.unity.cn/6000.2/Documentation/ScriptReference/AnimatorStateInfo.html) and [NavMeshAgent destination requests](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/ai/navmeshagent/setdestination).
