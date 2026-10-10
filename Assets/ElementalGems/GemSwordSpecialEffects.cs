using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace ElementalGems
{
    /// <summary>Gem-coloured body rim, motion ribbons and a bounded pool of posed afterimages.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(260)]
    public sealed class GemSwordSpecialEffects : MonoBehaviour
    {
        [Range(0, 3)] public float glowIntensity = 1.25f;
        [Range(.04f, .2f)] public float afterimageInterval = .085f;
        [Range(.1f, .5f)] public float afterimageLifetime = .28f;
        [Range(.05f, .4f)] public float trailLifetime = .18f;
        public bool Active { get; private set; }
        public Color Tint { get; private set; } = Color.white;
        public int VisibleAfterimages { get; private set; }
        const int PoolSize = 4;
        PlayerStateManager player;
        GemManager gems;
        GemLightSlashEffects slashTiming;
        readonly Dictionary<Renderer, bool> characterVisibility = new Dictionary<Renderer, bool>();
        SkinnedMeshRenderer source, shell;
        Material material;
        MaterialPropertyBlock properties;
        readonly Mesh[] meshes = new Mesh[PoolSize];
        readonly MeshRenderer[] ghosts = new MeshRenderer[PoolSize];
        readonly float[] ages = new float[PoolSize];
        readonly TrailRenderer[] trails = new TrailRenderer[3];
        readonly Transform[] trailBones = new Transform[3];
        GameObject pool;
        int nextGhost;
        float debt;
        Vector3 lastGhostPosition;

        void Awake() { player = GetComponent<PlayerStateManager>(); gems = GetComponent<GemManager>(); slashTiming = GetComponent<GemLightSlashEffects>(); }
        bool EnsureVisuals()
        {
            if (shell != null) return true;
            var template = Resources.Load<Material>("SwordSpecialEnergy");
            if (template == null) return false;
            // Select the main skinned body, not a small separately skinned shield or accessory.
            foreach (var candidate in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (candidate.sharedMesh != null && (source == null || candidate.sharedMesh.vertexCount > source.sharedMesh.vertexCount)) source = candidate;
            if (source == null) return false;
            material = new Material(template) { name = "Sword Special Energy (Runtime)" };
            properties = new MaterialPropertyBlock();
            var go = new GameObject("Special Body Glow"); go.layer = source.gameObject.layer; go.transform.SetParent(source.transform, false);
            shell = go.AddComponent<SkinnedMeshRenderer>(); shell.sharedMesh = source.sharedMesh;
            shell.bones = source.bones; shell.rootBone = source.rootBone; shell.localBounds = source.localBounds;
            shell.quality = source.quality;
            Assign(shell, source.sharedMaterials.Length);
            pool = new GameObject("Sword Special Afterimages (Runtime)");
            for (int i = 0; i < PoolSize; i++)
            {
                go = new GameObject("Afterimage " + i); go.layer = source.gameObject.layer; go.transform.SetParent(pool.transform, false);
                meshes[i] = new Mesh { name = "Special Pose " + i }; meshes[i].MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                ghosts[i] = go.AddComponent<MeshRenderer>(); Assign(ghosts[i], source.sharedMaterials.Length);
                ghosts[i].enabled = false; ages[i] = afterimageLifetime;
            }
            HumanBodyBones[] bones = { HumanBodyBones.Spine, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm };
            for (int i = 0; i < trails.Length; i++)
            {
                go = new GameObject("Special Motion Ribbon " + i); go.layer = source.gameObject.layer; go.transform.SetParent(transform, false);
                trails[i] = go.AddComponent<TrailRenderer>(); trails[i].sharedMaterial = material;
                trails[i].time = trailLifetime; trails[i].minVertexDistance = .06f;
                trails[i].widthMultiplier = i == 0 ? .3f : .12f;
                trails[i].widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
                trails[i].numCornerVertices = 2; trails[i].numCapVertices = 2;
                trails[i].shadowCastingMode = ShadowCastingMode.Off; trails[i].receiveShadows = false;
                trails[i].emitting = false;
                trailBones[i] = player.anim != null && player.anim.isHuman ? player.anim.GetBoneTransform(bones[i]) : null;
            }
            return true;
        }
        void Assign(Renderer renderer, int slots)
        {
            var materials = new Material[Mathf.Max(1, slots)];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        public void Begin()
        {
            if (Active) End();
            if (player == null) Awake();
            Active = true; debt = 0; nextGhost = 0;
            if (!EnsureVisuals()) return;
            // Keep the rig, equipment logic and particles running. Only suppress solid geometry;
            // the separate energy shell, motion ribbons and afterimages remain visible.
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer != shell && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                    characterVisibility[renderer] = renderer.forceRenderingOff;
            RevealCharacter(false);
            lastGhostPosition = transform.position;
            shell.enabled = true;
            RefreshTint();
            foreach (var trail in trails) { trail.Clear(); trail.emitting = true; }
        }
        void RevealCharacter(bool reveal)
        {
            foreach (var entry in characterVisibility)
                if (entry.Key != null) entry.Key.forceRenderingOff = entry.Value || !reveal;
        }
        public void TraceDash(Vector3 from, Vector3 to)
        {
            if (!Active || shell == null || (to - from).sqrMagnitude < .0001f) return;
            // Record both ends explicitly so even a dash completed in one frame leaves a ribbon.
            // Use the controller's actual displacement, keeping the trail on the collision-limited path.
            for (int i = 0; i < trails.Length; i++)
            {
                Vector3 end = trailBones[i] != null ? trailBones[i].position : to + Vector3.up;
                trails[i].AddPosition(end - (to - from));
                trails[i].AddPosition(end);
                trails[i].transform.position = end;
            }
        }
        void RefreshTint()
        {
            Tint = gems != null && gems.Equipped != null ? gems.Equipped.color : new Color(.8f,.9f,1);
            Tint = new Color(Tint.r, Tint.g, Tint.b, 1);
        }
        void Paint(Renderer renderer, float opacity, float mode)
        {
            properties.Clear(); properties.SetColor("_Tint", Tint);
            properties.SetFloat("_Opacity", opacity); properties.SetFloat("_Intensity", glowIntensity);
            properties.SetFloat("_Ribbon", mode); renderer.SetPropertyBlock(properties);
        }
        void LateUpdate()
        {
            if (!Active) return;
            if (player == null || !player.isActiveAndEnabled || !player.IsSwordSpecialAttacking) { End(); return; }
            if (!EnsureVisuals()) return;
            float delta = player.anim != null && player.anim.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (player.anim != null && player.anim.speed <= 0) delta = 0;
            Tick(delta);
        }
        // Explicit clock makes pause and cleanup verifiable without baking a mesh every render frame.
        public void Tick(float delta)
        {
            if (!Active || shell == null) return;
            // Share the slash presentation clock, including windows crossed at high attack speed.
            RevealCharacter(slashTiming != null && slashTiming.isActiveAndEnabled && slashTiming.TrailActive);
            RefreshTint();
            Paint(shell, .45f + .25f * Mathf.Exp(-player.SwordSpecialStepProgress * 8), 0);
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].transform.position = trailBones[i] != null ? trailBones[i].position : transform.position + Vector3.up;
                trails[i].startColor = Color.white; trails[i].endColor = new Color(1,1,1,0);
                Paint(trails[i], .8f, 1);
            }
            delta = Mathf.Max(0, delta);
            debt += delta;
            if (debt >= afterimageInterval && (transform.position - lastGhostPosition).sqrMagnitude > .01f)
            {
                debt %= afterimageInterval;
                // Compensate the imported rig scale because the snapshot transform applies it below.
                source.BakeMesh(meshes[nextGhost], true);
                var t = ghosts[nextGhost].transform;
                t.SetPositionAndRotation(source.transform.position, source.transform.rotation); t.localScale = source.transform.lossyScale;
                ghosts[nextGhost].enabled = true; ages[nextGhost] = 0;
                nextGhost = (nextGhost + 1) % PoolSize;
                lastGhostPosition = transform.position;
            }
            VisibleAfterimages = 0;
            for (int i = 0; i < PoolSize; i++)
            {
                ages[i] += delta;
                float fade = Mathf.Clamp01(1 - ages[i] / Mathf.Max(.01f, afterimageLifetime));
                ghosts[i].enabled = fade > 0;
                if (fade > 0) { Paint(ghosts[i], fade * .42f, 0); VisibleAfterimages++; }
            }
        }
        public void End()
        {
            RevealCharacter(true);
            characterVisibility.Clear();
            Active = false; VisibleAfterimages = 0;
            if (shell != null) shell.enabled = false;
            foreach (var ghost in ghosts) if (ghost != null) ghost.enabled = false;
            for (int i = 0; i < PoolSize; i++) ages[i] = afterimageLifetime;
            foreach (var trail in trails) if (trail != null) { trail.emitting = false; trail.Clear(); }
        }
        void OnDisable() => End();
        void OnDestroy()
        {
            End();
            if (shell != null) Destroy(shell.gameObject);
            foreach (var trail in trails) if (trail != null) Destroy(trail.gameObject);
            if (pool != null) Destroy(pool);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
