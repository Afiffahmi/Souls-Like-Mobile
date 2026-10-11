HORSE RIDING SETUP

Scene object: Rideable Horse
Reusable prefab: Assets/Prefabs/Horse/RideableHorse.prefab
Animation profile: Assets/Animation/Horse/HorseAnimations.asset

PLAYER AND SADDLE
Select Rideable Horse. Horse Interaction System > Player is the player allowed to mount.
The current scene player is assigned. A prefab with an empty Player reference resolves a local MountedPlayerController at runtime.
SaddlePoint is an EMPTY child of the animated spine_3 bone, so it follows the horse torso during dash attacks and other animations. Its position and rotation define the PLAYER ROOT attachment, so adjust it to line up the rider's hips with the horse's back. Do not manually parent the scene player under it; mounting does that at runtime.
DismountPoint is the preferred landing position. The system checks ground slope and capsule clearance and tries the other sides if blocked.

INPUT
Approach within Interaction Distance. Tap/click Mount, or press E (configurable Interaction Key).
Use the existing Move action / joystick to choose a camera-relative travel direction. The horse rotates toward it and walks forward along a curved path. Backward input makes a U-turn toward the side the joystick leans toward; an exactly centered reversal keeps the last turn side.
Walk Left / Walk Right play while the horse rotates; Walk resumes once aligned. Hold the existing Sprint input to run straight ahead once aligned. Turning immediately cancels running. Turn Speed and Heading Tolerance are adjustable on HorseController; Movement Reference can override the camera frame.
The existing Special Attack button/action triggers a mounted charge only while running. The same PlayerStateManager.SpecialAttack API also routes to it.
Release movement completely to show Dismount. Dismount is disabled while moving, attacking or dying.

ANIMATIONS
Player clips and controller: Assets/Animation/Player/HorseRiding
Horse clips, controller and shared animation profile: Assets/Animation/Horse
The nine Player_Horse clips are Humanoid muscle animations baked through the Player.fbx PlayerAvatar. Export parent prefixes, preview-camera tracks and exporter unit scales were removed during conversion. The existing riding controller still owns saddle attachment and movement. Replace Horse Clip and Player Clip in HorseAnimations.asset as needed; source-controller references allow the runtime overrides to pick up replacements on the next mount/play session.
Each row has paired state paths and a shared duration. Duration=0 uses the longer clip; a positive duration retimes both clips to that duration.
Horse and rider use separate Animators driven by a shared normalized timeline. Smooth changes use Transition Seconds. Death1 cannot be interrupted by input; it completes before Death2.
Mount/Dismount/Ejected player timing and attachment fractions are on MountedPlayerController.

HEALTH AND SPECIAL ATTACK
HorseController exposes TakeDamage(int), Die(), health, damage, radius, cooldown and hit-window settings.
Absorb Rider Damage defaults ON: incoming PlayerOverall damage goes to the mounted horse. Turn it off for independent player health.
The mounted special charge damages each Enemy once per attack inside the configurable hit area.
Horse death locks input and starts Death1, then ejects the rider at Eject At Death Normalized. The rider detaches during its Ejected animation and uses CharacterController collision/gravity until grounded. Normal controls restore after animation completion and safe landing. Dead horses cannot be mounted.

Authoring command: Tools > Riding > Set Up Horse In Active Scene.
No Play Mode or gameplay tests were run, at the user's request. Final animation art and feel should be evaluated in the user's own play session.

Walking Legs animator layer: a transform mask keeps all four legs on the Walk clip while the base layer plays left/right torso turns. The walking cycle remains continuous across steering changes and fades out for other motions. Configure through HorseWalkingLayerSetup.Configure when rebuilding the horse controller.

HorseController > Turning animation end frames: Left Turn End Frame and Right Turn End Frame select the last source-clip frame before returning to Walk. 0 uses the full clip; values above the clip length clamp to its end. Frames use each horse clip's frame rate and respect the shared animation duration. Facing the requested heading ends the animation earlier. Once finished, the turn animation stays finished until alignment, release, or opposite steering; physical steering and walking legs continue.

ELEMENTAL HORSE SUMMONING
The Player's HorseSummoner owns one active horse. The new Summon Horse / Dismiss Horse button fills the ninth slot of Canvas/Action Buttons. A clear nearby ground position is required. Dismount before dismissing; summon/dismiss transitions block repeated presses.
The authored Rideable Horse is kept as an inactive runtime template so new summons preserve its scene Inspector adjustments, saddle placement, scale and turn end frames. In Edit Mode it remains visible for authoring. HorseSummoner.horsePrefab also supports scenes without a template. Play starts with no summoned horse.
HorseTailAura captures the equipped gem at summon. It follows gem changes only during Mounting/Riding, then keeps its own element and color after dismounting. Changing the player's gem on foot does not recolor the existing horse; the next summon captures the new gem. Normal uses neutral silver-blue energy.
HorseTailAura exposes strand count, segments, minimum/maximum random length, width, spread, wave amount, flow speed, brightness and seed. Defaults: 36 hair strands with 3 glow layers each and varied extensions of 0.65-1.9 metres. The effect follows the animated tail bones; all sizes are world metres to avoid the imported skeleton's large scale.
Tools/Riding/Set Up Elemental Horse Summoning reconnects the prefab, aura and scene HUD through Unity APIs. Gameplay tests are left to the user.

Summon/dismiss size animation uses a runtime Horse Reveal Visuals pivot around the model. The horse root and CharacterController retain full authored scale throughout, avoiding scaled step-offset validation errors. The Animator bone paths and saddle attachment remain together under the visual pivot.

Tail shape update: smooth travelling hair waves now grow from a stable root to freely moving tips. Wave Amount controls amplitude (default 0.35 m), Wave Cycles controls the broad curve frequency (0.85), and Flow Speed controls motion (1.4). Each strand varies in phase, frequency, speed and length; the skeleton-tip elbow remains removed.

Tail gait shapes: actual movement speed blends a hanging, softly curled idle tail into a shallower walking curve and finally the original running stream. Turning uses walking speed. HorseTailAura exposes Idle Droop (0.7), Walk Droop (0.22), and Shape Blend Speed (4). Existing wave, glow and random strand settings remain in use. No gameplay tests were run.

HORSE EQUIPMENT AND GROUND CONTACT
Equipment > Horse shares the existing equipment modal, input pause, slot cards and stat comparison layout. Saddle and Horseshoes are separate slots. Six sample items in Assets/Data/Horse/Equipment are available to equip/remove; as with the weapon demo collection, they need no purchase or unlock. The loadout lives on PlayerHorseEquipment and survives dismissing/resummoning the horse within the scene session.
Speed bonuses from both slots add together, then multiply the base walk/run speeds. Actual movement speed advances the horse/rider locomotion clock and walking-leg layer, keeping animation cadence in step with movement. Special attack movement and animation use the same captured speed multiplier. Damage = (base special damage + flat bonuses) * (1 + total damage percent / 100); knockback = base special knockback * (1 + total knockback percent / 100). Enemy immunity and knockback resistance still apply. A special attack captures its equipment stats when started, so changing equipment affects the next dash.
The floating model offset was corrected by aligning the actual baked horse mesh minimum with the collision capsule bottom, preserving the skeletal saddle attachment. The horse motor now snaps down to nearby walkable ground (Ground Snap Distance, default 0.8m) after movement; larger drops use gravity rather than teleporting. Tools/Riding/Set Up Horse Equipment And Ground Contact creates the sample catalog and Horse tab and can realign the model after an authoring offset change. No gameplay tests run.

HORSE LEVEL
The horse has its own level (1-100), independent of the player's Combat Level. Equipment > Horse > Upgrade +1 raises it. Like the existing weapon demo upgrades, this button does not charge currency. Configure its starting level and per-level growth under Player > PlayerHorseEquipment > Horse Level. Its loadout and level survive dismissing/resummoning within the scene session; no cross-session save system is added.
Default growth per horse level above 1: +10% special damage, +2% movement/animation speed and +5% special knockback. Gear and horse-level percentages add together, with limits of 4x speed, 100x damage and 4x knockback. The Horse tab displays its level, effective stats and mount/dismount durations.
Mount and dismount duration = the animation profile duration / horse speed multiplier (level plus equipment). The same scaled clock drives the rider animation, movement to/from the saddle and normalized attach/detach milestones. Speed is captured at the start of each action; an upgrade during an action applies to the next action. Ejection/death timings remain driven by their existing clocks. No Play Mode or gameplay tests were run.