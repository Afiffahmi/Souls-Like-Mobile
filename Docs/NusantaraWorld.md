# Nusantara Quest world

The world is authored from the supplied regional sketch and Bujang Senang concept sheet, saved in `Docs/References`. The working scene is **Assets/NusantaraWorld/Scenes/NusantaraQuest_World.unity**, saved as a copy of the open `main_scene_test` including its current settings. The original scene file is preserved. Regions 7 and 8 are excluded.

The new scene keeps the existing player, camera rigs, UI, lighting, equipment and horse. Its old test plane, enemy and navigation surface are preserved under an inactive **Original test content (preserved)** object. Generated collision and navigation replace that test floor in this scene. The configured story start is in Region 1; the current scene preview is positioned at Region 5's Kampung Sungai for inspection.

The original geography stage was not playtested. The **12 October 2026 Region 5 enhancement** has since received editor physics, seam, landmark, foundation and Addressables checks described below. Device performance and controller playtesting remain outstanding.

## Opening and moving around

Open the copied world scene. Use **Tools → Nusantara → Preview** to choose Region 1 or any of the nine Region 5 locations. A bookmark moves the existing player and loads a 3 × 3 chunk editor preview. Press Play yourself when ready. Choose **Region 1 - Player start** to restore the intended starting location before saving a shipping scene.

The editor preview is tagged `EditorOnly`, so it is stripped from builds. It disables itself when you enter Play; runtime Addressables streaming owns the actual world. Only the nearby preview is held in the editing scene, not all generated terrain.

For lasting environment edits, open the corresponding chunk prefab, or apply the preview's prefab overrides to it before changing bookmarks. The preview is a temporary view of those assets. Saving only scene overrides does not change the prefabs loaded by runtime streaming.

Open **Tools → Nusantara → World Foundation** in Unity.

## Assets and defaults

- `Assets/NusantaraWorld/NusantaraWorld.asset`: six traced region polygons, nine positioned Region 5 landmarks, starting coordinates, chunk records, streaming and walking settings.
- `Assets/NusantaraWorld/ReferenceGeography.asset`: editable river centerlines, widths, water elevation, walking routes, coastline, terrain resolution and placement density. The initial generator reads this profile. Changing it does not silently overwrite already authored chunk meshes.
- `Assets/NusantaraWorld/Generated/Chunks`: each coordinate has editable essential, distant, optional props and optional gameplay prefabs, plus mesh assets.
- `Assets/models/Map/Props/Region5`: the 18 source FBXs, reusable prop prefabs and simplified LOD meshes. Prefabs are scaled in metres; moved assets retain their GUIDs.
- `Assets/Materials`: prop materials have the `Region5_` prefix; shared world materials have `Nusantara_`. No additional material folder is created. The project uses **Built-in rendering**, with Standard solid materials, Built-in mist and a compatible river shader pass. Installing the URP package does not activate URP.
- `Assets/Textures`: prop textures have the `Region5_` prefix; shared generated textures have `Nusantara_`. Prop textures use mipmaps, a 1024 maximum and ASTC 6×6 Android/iOS overrides.
- `Assets/NusantaraWorld/Generated/Navigation`: per-chunk navigation data.
- `Assets/NusantaraWorld/WorldBootstrap.prefab`: reusable inactive streamer template. The copied scene has its own configured active World object with the existing player assigned.
- `Assets/NusantaraWorld/Region5PropCatalog.asset`: metadata for the 18 original models; references are GUID/path strings.
- `Assets/AddressableAssetsData`: **Nusantara World Chunks** Addressables group, packed separately by content prefab.

Default scale is 1 unit = 1 metre. A 48 × 48 grid of 256 m chunks covers **12.288 × 12.288 km**. The traced coastline determines the irregular landmass and occupied chunks, with a surrounding coastal strip. The sketch places Region 1 centrally, 2 west, 3 southwest, 4 south, 5 east and 6 north. The concept sheet's Map 3 arrow is treated as a westward route through Region 1, preserving the sketch's regional adjacency. Gameplay radius is 1 chunk, detailed visual radius 3, distant terrain radius 5, and outer retention radius 6. Prediction looks up to 4 seconds / 256 m ahead. At most two requests run concurrently and at most one new request starts per update.

## Water, bridges and Region 5

Rivers, branches, delta channels, pools, two Region 1 freshwater basins and coastal water use shared animated water materials compatible with the active render pipeline. **Water entry blockers** are non-trigger mesh colliders in essential chunks. They extend above the banks and cover the wet region, with openings for bridge corridors. Smooth solid bridge decks span those openings, and their rails prevent stepping off. `WorldWaterSafety` also returns a player or horse to their last dry position if a combat displacement or teleport bypasses normal collision. Swimming is not enabled. Floating lily pads and boats have no walkable collision.

The generated world contains **1,769 chunks**, **13,342 editable Region 5 prop instances**, and **83 chunks with river water**. The available models populate Region 5 only. Houses, trees, bamboo, roots, logs, stumps, crates, barrels, docks, boats, lily pads, ruins, arches, crocodile statues and skulls remain separate prefab instances. Placement is deterministic and serialized in chunk prefabs. Missing props are skipped; they are not replaced with fake finished assets. Forest paths and landmark clearing areas are reserved from random tree placement. Essential trunk/building collision is independent from streamed visual decoration.

Region 5 includes village courts, a forest route, branching delta, swamp channels with local ground mist, a waterfall environment, ruined structures, a hollow walk-in cave, elite territory and a broad boss clearing. Water and Nature encounter-zone markers are editable; they do not implement boss combat. Procedural terrain, cave vault and waterfall cliff geometry are an environment foundation, and still need art polish. The current asset set cannot supply all vegetation, sacred structures, nests and architecture shown in the concept.

## Integrating authored content

1. Keep the starting chunk in Region 1. The initial polygons and landmark coordinates have been authored from the supplied references. Inspect them in the geography/world assets when refining the layout.
2. Author each chunk around its southwest world-space corner. Content prefabs must have a zero local position, identity rotation and unit scale under the chunk. Keep the World root at unit world scale and identity rotation.
3. Add each content prefab as an Addressable. Add a `WorldChunkRecord` to the definition with its coordinate, region and stable Addressables keys. Keep keys stable after shipping because persistence is scoped by chunk, content address and local placement ID.
4. The essential prefab contains the terrain, all essential collision, deep-water barriers, smooth bridge/ramp collision and baked navigation. The distant prefab contains simplified terrain only. Put decoration into several small batches, and gameplay objects into separate batches. Avoid a single enormous prefab that would cause an instantiation spike.
5. Keep Regions 1, 2, 3, 4 and 6 at geographical-foundation detail: terrain, water, essential walking crossings and collision, without decorative environment or encounters. The definition validator rejects decoration and gameplay batches for these regions.
6. Add the inactive bootstrap to an authored world scene, assign the existing player's Transform and the definition, then activate it when the layout/content are ready. The streamer does not move the player or replace its controller/camera. Spawn the player at the configured `playerStart` through your existing spawn flow.
7. Add `WorldStreamingPlayerGate` for loading protection. Assign the streamer, the existing CharacterController and **only** the movement scripts that should pause. The guard suspends movement until collision is ready and restores each script's previous enabled state. It also stops movement across an unloaded boundary. Teleports should pause movement, move the player, call `RequestReplan`, and wait for `IsCollisionReady` before resuming.
8. Build Addressables content for the target platform before making a player build. The Editor fast-mode checks below are not Android/iOS bundle or device tests.

The runtime hierarchy is `World / Region / Chunk / essential, distant, decoration, gameplay`. Content starts under inactive holders; saved state is restored before activation. Essential requests outrank gameplay, which outranks decoration and distant scenery. Gameplay only loads after its chunk collision. Pending requests are deduplicated. Separate retention bands reduce terrain-detail churn and outer unload thrashing. Late completions after unload/shutdown release their instances and handles.

## Persistence and AI

`WorldPersistentObject` provides a JSON state adapter and a `restored` UnityEvent. Assign a unique, stable ID to each authored placement within a content prefab. Call `SetState` when a chest opens, loot is collected, a checkpoint activates, or an encounter should remain defeated. Loaded adapters write through to the store immediately, so calling `SetState` before destroying a collected object preserves its progress. Existing gameplay scripts must explicitly connect to this adapter; they have not been rewritten or auto-wired. Custom components can implement `IWorldPersistentState` instead; if they destroy themselves before capture, they must write their final state to the store first.

State is captured on content unload, autosave, application pause and shutdown. Saves are world-scoped under `Application.persistentDataPath/Nusantara`, with atomic file replacement and a backup. A corrupt or mismatched save is reported and protected from automatic overwrite. Normal autosave disk writes use a worker task; pause/shutdown flushes are synchronous to protect progress.

`WorldChunkNavigation` registers the generated chunk's baked `NavMeshData` and removes it on unload. East/north edges own short, bidirectional walking links to loaded neighbors; endpoints are sampled on the corresponding agent's navigation data. There are no jump links. Bakes use 15° slope, 0.2 m climb, 0.5 m radius and 2 m height. Cross-border behavior has not been playtested. Re-bake affected chunks and edge samples after changing terrain, paths or essential collision.

Place new enemy prefabs in gameplay batches so their AI only activates in the nearby gameplay ring. Existing enemy AI is unchanged. Custom far-AI throttling, pooling and individual persistent enemy/death adapters remain integration work when encounters are authored.

## Terrain and walking validation

`WorldTerrainMesher.Build` samples global world coordinates; both sides of every chunk use the same lattice and derivative interval. Generated terrain uses 32 intervals per chunk, with 8 for distant terrain. Distant meshes have 12 m skirts for mixed-resolution edges. Highlands use broad height functions, main routes flatten the terrain corridor, and mesh decks carry paths over channels.

The world walking limit is **15°**, with a conservative **0.2 m** step threshold, **0.5 m** character radius and **2 m** height. The current player controller permits 45°/0.3 m; the world configuration intentionally uses the stricter brief limits.

Add `WorldWalkabilityArea` to intended playable ground, assign ground/obstacle layers, entry and required destinations, then validate it in the World Foundation window. Scope the volume around routes rather than scenic cliffs. The validator samples slopes, floor presence and character clearance, checks intermediate edge samples and capsule sweeps, then flood-fills connected walking routes. It reports disconnected destinations, gaps, abrupt steps and obstacles. It processes bounded batches across Editor updates and caps individual runs at 300,000 samples. Reports go to `Library/Nusantara/WalkabilityReport.json`; the Scene view shows up to 1,000 issue markers.

This is a sampling-based editor check. Tiny features between samples, exact controller behaviour, moving obstacles, seam geometry and NavMesh connectivity still need playtesting once the world exists.

## Verification

- **Tools → Nusantara → Run Foundation Checks**: coordinate edges, prediction, hysteresis, terrain height/normal seams, persistence round trips, atomic replacement, flat ground, blocked paths, slopes, gaps and steps. Uses temporary preview-scene geometry and deletes its fixtures.
- **Tools → Nusantara → Run Addressables Streaming Checks**: real Addressables prefab loads in an isolated preview scene, priority, concurrency, unloading, persistent revisit, shutdown and cancellation cleanup. The test harness explicitly advances resource callbacks and simulated delay time because Edit Mode does not provide a normal gameplay clock. Test-only reflection is pinned to the installed Addressables 4.1.1 internals; it is excluded from player builds.
- Reports are written to `Library/Nusantara/FoundationChecks.txt` and `StreamingChecks.txt`.

## Remaining production work

The user will test movement, water edges, bridges, landmarks and streaming. Missing props can be added to the existing editable chunks later. Further work includes art polish, authored encounters and quests, persistence adapters for individual gameplay systems, content bundle builds for each target, then memory/loading/rendering profiling on Android and iOS. Instancing, LODs and streaming are implemented, but their presence alone is not evidence of mobile frame rate or production readiness.

The generator refuses to overwrite an existing generated chunk folder. Back up authored content before intentionally rebuilding. Generation progress is recorded in `Library/Nusantara/Generation.txt`.

## Region 5 reference enhancement — 12 October 2026

The enhancement edits the existing **305 Region 5 chunks**. It preserves their coordinates, terrain mesh GUIDs, main path and bridge heights, original prop sources, streaming keys, player, cameras and combat. Gentle relief is added to existing terrain vertices away from routes, waterways, landmark clearings and regional borders; distant terrain and affected navigation are updated too. A hash comparison of **14,166 files** in Regions 1, 2, 3, 4 and 6 found no changes. No new region is generated.

The reference's dashed outer boundary is treated as an exclusion boundary as requested. Scenery placement uses actual Region 5 polygon containment, with an inset for new props, rather than the streamer's nearest-region fallback. **24 irregular shoreline island props** sit in existing river channels and basins, clear of walking crossings. These islands are scenery: no floor collider, navigation destination, bridge, or encounter is placed on them. Existing water-entry blockers remain, and `WorldGeographyProfile.scenicIslands` also rejects entry through the player's water safety guard. Existing main destinations retain walking access.

The pass adds about **11,200 editable details** using the 18 existing source props and 13 small shared mesh prefabs: mossy rocks, ferns, broadleaf bushes, reeds, shrines, nests, small rib bones, fishing nets, market stalls, vines, crystals, mushrooms and irregular island shores. Small vegetation has no collision; larger solid props use simple essential-chunk colliders with updated navigation.

- **Kampung Sungai:** nets, market stalls, storage detail and a riverside shrine alongside the existing houses and docks.
- **Hutan Rimba:** clustered undergrowth, mushrooms, moss, roots and one giant ancient-tree grove using the existing tree model.
- **Delta Sungai:** riverbank clusters, driftwood and unreachable island scenery in the existing channels.
- **Air Terjun Suci:** shrines, irregular mossy cliff faces, hanging roots, crest vegetation and localized spray capped at 48 particles.
- **Gua Sunyi:** emissive crystal clusters against the cave walls, leaving the central walking corridor open.
- **Runtuhan Lama:** additional ruins, shrines, vines, moss and vegetation around the existing structures.
- **Rawa Berkabus:** root clusters, reeds, muddy rocks and fallen timber; the existing local mist is retained.
- **Pulau Buaya:** crocodile nests, small rib bones and broken vegetation; its old giant skull is removed.
- **Sarang Bujang Senang:** one giant crocodile landmark, a 160 m circular stone combat floor, guardian shrines and perimeter roots around the existing arena clearing. Floor pieces are clipped to their existing chunks and share a continuous walking height.

### Leaves and optimization

The existing bare tree model is unchanged. **276 chunk-level particle VFX batches** provide its canopy leaves. A shared 256 px clump texture derives its silhouette from the project's existing `leaf.png`. Depth-writing alpha cutouts, mipmaps, no particle shadows, no collision and paused particle simulation keep the effect bounded. Each batch has a **384-particle cap**, with fewer leaves on the two lowest Unity quality levels. Radius and distance controls are editable on `WorldCanopyVfx`; camera-to-player obstruction clearing removes leaves blocking that sightline.

The existing shared materials, prop LODs, instancing, frustum culling, Addressables streaming, prediction, hysteresis and persistent-state system are retained. Enhancement batches have stable addresses appended to the existing chunk records. Solid collision remains in essential chunks. Unique landmarks receive stable `WorldLandmarkIdentity` IDs; runtime duplicate activation is rejected. The giant crocodile is authored explicitly and is absent from biome prop lists. These controls have not been profiled on Android or iOS, and no 30/60 FPS claim is made.

### Reusable authoring tools and evidence

`Region5Biomes.asset` exposes allowed prop lists, cluster counts, scale, slope, height, river distance, path/building clearance, spacing and deterministic seeds. The original reference generator also now uses strict Region 5 containment for its prop placements and reserves the giant skull for the boss arena.

The Editor menus under **Tools → Nusantara** provide **Enhance Existing Region 5**, **Polish Sacred Waterfall**, **Finish Circular Boss Arena**, **Finish Region 5 Prop Collision**, **Repair Region 5 Bridge Junctions** and **Validate Region 5 Enhancement**. Re-running the enhancement recreates its generated enhancement batches; apply final collision, bridge-junction and validation passes after changing their placement rules. Preserve handmade edits first. Automatic snapshots of touched assets are kept under `Library/Nusantara/Region5*Backup/`; Library is not versioned, so retain a separate backup for long-term recovery.

The validation checks every Region 5 chunk, stable IDs, island exclusions, decoration collision, configuration, terrain edges and **8,005 main-route samples**. Its route checks use actual essential collision for floor, slope, capsule clearance and adjacent heights at 2 m intervals. The initial run found two obstructed village-junction samples; removing eight rail pieces at that connection and rebaking its navigation resolved them. The green/yellow/red heatmap combines geography slope/water sampling with the actual route samples. This is sampled evidence, not a proof against every sub-sample obstacle or exact controller behavior.

Reports and previews are saved in `Docs/Region5Validation.txt` and `Docs/References/Region5_*.png`. The **26 foundation checks** and **six isolated Addressables checks** passed. No Play mode or device session was run. Further work includes finer art matching to the illustrated concept, missing bespoke architecture and decorations, NPC/quest/encounter authoring, controller playtesting and target-device memory/render/loading profiling.
