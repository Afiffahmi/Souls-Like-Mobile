# Isometric player movement and lock-on

Configured scene: `Assets/Revamp/scene combine.unity`.

## Controls

- WASD / gamepad left stick: move.
- Hold either Shift / gamepad left-stick press: run.
- Q / middle mouse / gamepad right-stick press: toggle lock-on.
- The scene's touch joystick drives movement. Hold the touch Sprint button to run; press the touch Lock button to toggle targeting. A custom UI button can also call `PlayerLockOn.ToggleLock()`.

Unlocked movement uses the isometric camera's horizontal axes and smoothly turns the player toward travel. Every joystick direction uses only the forward Walk or Run animation (`MoveX = 0`, `MoveY = 1`), including while turning. Running works in any joystick direction. At rest, Speed and both direction values return to zero.

In this isometric scene, locked movement stays camera-relative: joystick up/down/left/right keeps the same world direction before and after locking. The player continues facing the enemy. The Animator receives movement relative to the player's facing, so walking toward the enemy plays Forward, moving away plays Backward, and moving across its direction plays a strafe or diagonal. The correct eight-direction animation therefore depends on where the enemy is on screen, rather than directly copying the joystick axes.

All eight directions can walk or run while locked. Translation clamps diagonal magnitude; facing smoothly tracks the enemy even while idle. Releasing lock-on immediately restores the forward-only animation behavior. `PlayerLockOn > Movement Space` is set to **Camera Relative** for this scene; **Target Relative** remains available for a third-person camera where joystick up should always approach the enemy.

## Animator

The existing `PlayerLocomotion.controller` uses `MoveX`, `MoveY`, and `Speed`.
Speed 0 = idle, 1 = walk, 2 = run. Analog input and acceleration blend between these values.

| Direction | MoveX | MoveY |
|---|---:|---:|
| Forward | 0 | 1 |
| Forward Right | 1 | 1 |
| Right | 1 | 0 |
| Backward Right | 1 | -1 |
| Backward | 0 | -1 |
| Backward Left | -1 | -1 |
| Left | -1 | 0 |
| Forward Left | -1 | 1 |

Both `Walk_8Directions` and `Run_8Directions` use these coordinates while locked on. Exploration selects their forward entries only; a separate Animator is unnecessary. The three existing forward Sprint motions are preserved. The five side/back Run slots use the project's DoubleL in-place Run animations. The existing Walk and forward Sprint clips retain their previous assignments under `Placeholders`; replace any desired motion in the Animator without changing positions or parameters. Root motion stays disabled because the CharacterController owns translation.

## Components

- `PlayerLockOn`: acquisition, current target, validity, toggle input, and target-facing rotation. It lends a movement frame to the motor and restores its prior settings on release.
- `LockOnTarget`: eligibility and aim position. Attach to each enemy root (above its colliders). Assign `aimPoint`, or adjust `localAimOffset`.
- `PlayerLocomotion`: movement, acceleration, gravity, eight-direction animation, and Walk/Run speeds. `AllowOmnidirectionalRun` is enabled by lock-on and restored afterward.
- `IsometricPlayerCamera`: the active scene's fixed orthographic overhead camera. It follows only the player at a fixed angle, distance, and orthographic zoom. Lock-on never recentres the view toward the enemy or changes zoom. The older FreeLook object is inactive, and the old `PlayerLockOnCamera` adapter is no longer attached to this scene's player.
- `LockOnIndicator`: positions a replaceable Canvas visual above the target. It hides on release, behind the camera, and outside the view.

## Target selection and release

Each enemy owns a spherical lock-on area. On its `LockOnTarget` component, set **Lock On Radius** (20 metres by default) and **Release Radius** (25 metres by default). The player must be inside the smaller sphere when pressing the lock-on button. Merely entering the area does not lock automatically. No trigger collider or Rigidbody is required for the area.

Select the enemy with Scene-view Gizmos enabled: the **cyan sphere** is its acquisition area and the **orange sphere** is its release boundary. Both follow the enemy root. Assign **Area Center** to another Transform if needed. Radius values use world metres and are independent of model scale and the aim point.

When several areas overlap the player, visible enemies nearest the screen centre are preferred. If eligible enemies are off-screen, the closest one is selected. An enemy does not have to be on-screen to lock. The fixed camera does not zoom out to show distant targets; their indicator appears when they are on screen.

Lock releases when toggled again, when the target is destroyed, deactivated, disabled, marked unavailable, or when the player leaves that enemy's Release Radius. The larger release sphere prevents flicker at the acquisition boundary. Set both radii equal to release immediately on leaving the acquisition area.

Area membership is sufficient by default. Enable **Require Line Of Sight** on `PlayerLockOn` if walls should block acquisition and release an existing lock after the configured obstruction grace time. This optional rule uses Obstruction Mask and Obstruction Grace Time on the player; radius settings are on each enemy.

Targets with the project's existing `Enemy` component automatically observe its existing health through the read-only `IsAlive` property. No damage or health behavior was added. For another enemy implementation, call `LockOnTarget.SetTargetable(false)` when it dies or becomes unavailable; set true for respawn. Disabling or destroying the target also works.

## Replace the indicator

In the scene hierarchy, expand `LockOn UI` and replace the children of `LockOn Indicator - Replace Visual`. Keep its RectTransform, or assign a replacement RectTransform to the player's `LockOnIndicator.visual` field. Keep decorative graphics' Raycast Target disabled. `worldOffset` controls the marker's position relative to the enemy's aim point.

## Camera tuning

Select **Isometric Camera** in the hierarchy and adjust its **Isometric Player Camera** component:

- **Elevation**: 55 degrees downward by default. Increase for a more overhead view.
- **Yaw**: 45 degrees by default; controls the fixed compass angle.
- **Orthographic Size**: 8 by default. Increase to zoom out; reduce to zoom in.
- **Distance**: camera depth from the focus, independent of orthographic zoom.
- **Follow Damping**: player-follow response speed.
Zoom is fixed at the configured Orthographic Size; there is no automatic target framing or zoom adjustment.

The camera script references only the player and has no dependency on targeting. `PlayerLockOn.viewCamera` and `PlayerStateManager.cameraMain` reference that same render camera. Cinemachine Brain lens override is enabled for orthographic projection. The camera keeps its overhead angle when locking/unlocking; it does not return to the old third-person view.

Other prototype scenes and prefabs are not automatically migrated. To use this setup elsewhere, assign the same locomotion Animator/input assets, camera references, target markers, and indicator visual.

