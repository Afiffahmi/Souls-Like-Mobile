using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ElementalGems.Editor
{
    public static class GemSystemSetup
    {
        public const string Root = "Assets/ElementalGems";
        private static readonly Color[] Colors = {
            new Color(.7f,.78f,.88f), new Color(1,.26f,.055f), new Color(.12f,.65f,1),
            new Color(.25f,.92f,.4f), new Color(.85f,.6f,.27f), new Color(.64f,.48f,1),
            new Color(.65f,1,.92f), new Color(.7f,.15f,.94f) };
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Install outside Play Mode.");
            foreach (var dir in new[] { "Definitions", "Textures", "Materials", "Prefabs", "Prefabs/VFX", "UI" }) Directory.CreateDirectory(Root + "/" + dir);
            AssetDatabase.Refresh();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerStateManager>();
            if (player == null) throw new InvalidOperationException("No active PlayerStateManager in the scene.");
            var definitions = new GemDefinition[8];
            for (int i = 0; i < definitions.Length; i++) definitions[i] = CreateGem((ElementType)i);
            var manager = Ensure<GemManager>(player.gameObject); manager.gems = definitions;
            var melee = Ensure<GemSwordCombat>(player.gameObject);
            var swordVisuals = new SerializedObject(player.GetComponent<PlayerSwordVisuals>());
            var sword = swordVisuals.FindProperty("swordWithoutSheath").objectReferenceValue as GameObject;
            if (sword == null) throw new InvalidOperationException("Existing sword model is not assigned.");
            melee.blade = sword.transform;
            var effects = Ensure<GemWeaponEffects>(player.gameObject); effects.sword = sword.transform;
            var health = player.GetComponent<PlayerOverall>();
            if (health != null) { Undo.RecordObject(health, "Enable existing player health"); health.enabled = true; }
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Ensure<ElementalEnemy>(enemy.gameObject);
            // The existing lock-on practice target had no damage receiver.
            foreach (var target in UnityEngine.Object.FindObjectsByType<LockOnTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (target.GetComponentInParent<PlayerStateManager>() != null) continue;
                var enemy = target.GetComponentInParent<Enemy>();
                bool newEnemy = enemy == null;
                if (newEnemy) enemy = Ensure<Enemy>(target.gameObject);
                var elemental = Ensure<ElementalEnemy>(enemy.gameObject);
                if (newEnemy) elemental.element = ElementType.Nature;
                EditorUtility.SetDirty(elemental);
            }
            if (UnityEngine.Object.FindFirstObjectByType<GemSelectionUI>() == null) GemUIBuilder.Create(manager);
            EditorUtility.SetDirty(manager); EditorUtility.SetDirty(melee); EditorUtility.SetDirty(effects);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            return "Installed 8 gem definitions, 21 VFX prefabs, icons/materials, shared player components, elemental target and Gem Selection UI.";
        }
        private static T Ensure<T>(GameObject go) where T: Component => go.GetComponent<T>() ?? Undo.AddComponent<T>(go);
        private static GemDefinition CreateGem(ElementType type)
        {
            string path = $"{Root}/Definitions/{type}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GemDefinition>(path);
            if (existing != null) return existing; // Preserve Inspector tuning on a repeated installation.
            var gem = ScriptableObject.CreateInstance<GemDefinition>();
            gem.element = type; gem.displayName = type.ToString(); gem.color = Colors[(int)type];
            gem.icon = CreateTexture(type, true);
            var rows = new System.Collections.Generic.List<ElementMatchup>();
            void Relation(ElementType e, float n) => rows.Add(new ElementMatchup { defender = e, multiplier = n });
            StatusSpec S(StatusKind kind, float seconds, float strength, float chance = 1) => new StatusSpec { kind = kind, duration = seconds, magnitude = strength, chance = chance };
            switch (type)
            {
                case ElementType.Normal:
                    gem.description = "Your original sword and arrows. No elemental bonus, status effect or weapon aura."; break;
                case ElementType.Fire:
                    gem.description = "Scorch foes with searing strikes. Higher impact damage and a lingering burn. Strong against Nature; resisted by Water.";
                    Relation(ElementType.Nature, 2); Relation(ElementType.Water, .5f); gem.damageScale = 1.25f;
                    gem.statuses = new[] { S(StatusKind.Burn, 3, 5) }; break;
                case ElementType.Water:
                    gem.description = "Crashing tides slow enemies and push them back. Strong against Fire; resisted by Lightning.";
                    Relation(ElementType.Fire, 2); Relation(ElementType.Lightning, .5f);
                    gem.knockback = 5; gem.statuses = new[] { S(StatusKind.Slow, 3, .45f) }; break;
                case ElementType.Nature:
                    gem.description = "Bind foes with roots, inflict poison and restore health on each successful hit. Strong against Earth; resisted by Fire.";
                    Relation(ElementType.Earth, 2); Relation(ElementType.Fire, .5f);
                    gem.healingOnHit = 2; gem.lifeSteal = .05f; gem.statuses = new[] { S(StatusKind.Root, 1.2f, 1), S(StatusKind.Poison, 5, 3) }; break;
                case ElementType.Earth:
                    gem.description = "Stone armor reduces incoming damage by 25%. Heavy elemental impacts stagger foes. Strong against Lightning; resisted by Nature.";
                    Relation(ElementType.Lightning, 2); Relation(ElementType.Nature, .5f);
                    gem.armorReduction = .25f; gem.statuses = new[] { S(StatusKind.Stagger, .4f, 1) }; break;
                case ElementType.Lightning:
                    gem.description = "Move 15% faster and shock enemies with stunning hits. Strong against Water; resisted by Earth.";
                    Relation(ElementType.Water, 2); Relation(ElementType.Earth, .5f);
                    gem.movementScale = 1.15f; gem.statuses = new[] { S(StatusKind.Stun, .7f, 1, .35f) }; break;
                case ElementType.Wind:
                    gem.description = "Move 20% faster. Consecutive hits build up to 25% bonus damage; arrows fly 35% faster. Neutral against every element.";
                    gem.movementScale = 1.2f; gem.projectileSpeedScale = 1.35f; gem.comboDamagePerHit = .05f; break;
                case ElementType.Darkness:
                    gem.description = "Void energy drains life and ravages weakened enemies. Deals double damage below 25% health. Matchups can be customized.";
                    gem.damageScale = 1.15f; gem.lifeSteal = .2f; gem.executeHealthThreshold = .25f; gem.executeDamageScale = 2;
                    gem.statuses = new[] { S(StatusKind.Void, 4, 4) }; break;
            }
            gem.matchups = rows.ToArray();
            if (type != ElementType.Normal)
            {
                var particleSprite = CreateTexture(type, false);
                var particle = MakeMaterial(type + " Particles", particleSprite.texture);
                gem.trailMaterial = MakeMaterial(type + " Trail", null);
                gem.swordAura = MakeParticles(type, "Sword Aura", particle, 0);
                gem.bowAura = MakeParticles(type, "Bow Aura", particle, 1);
                gem.impact = MakeParticles(type, "Impact", particle, 2);
            }
            AssetDatabase.CreateAsset(gem, path);
            return gem;
        }
        private static Material MakeMaterial(string name, Texture texture)
        {
            var material = new Material(Shader.Find("ElementalGems/Particles")); material.name = name;
            if (texture != null) material.SetTexture("_MainTex", texture);
            AssetDatabase.CreateAsset(material, $"{Root}/Materials/{name}.mat"); return material;
        }
        private static Sprite CreateTexture(ElementType type, bool icon)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size * 2 - 1, v = (y + .5f) / size * 2 - 1;
                float mask = Shape(type, u, v);
                if (icon)
                {
                    float diamond = Mathf.Abs(u) + Mathf.Abs(v) * .78f;
                    float alpha = Mathf.Clamp01((.86f - diamond) * 60);
                    Color color = Colors[(int)type] * (u < 0 ? .9f : .6f);
                    color = Color.Lerp(color, Color.white, Shape(type, u * 1.65f, v * 1.65f) * .9f);
                    if (diamond > .78f) color = Color.Lerp(Colors[(int)type], Color.white, .6f);
                    color.a = alpha; pixels[y * size + x] = color;
                }
                else pixels[y * size + x] = new Color(1, 1, 1, mask);
            }
            texture.SetPixels(pixels); texture.Apply();
            string path = $"{Root}/Textures/{type}{(icon ? " Icon" : " Particle")}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static float Shape(ElementType type, float x, float y)
        {
            float r = Mathf.Sqrt(x*x+y*y), a = Mathf.Atan2(y,x);
            switch (type)
            {
                case ElementType.Fire: return Mathf.Clamp01((.7f - Mathf.Abs(x - .14f * Mathf.Sin(y*6)) * (1.5f + y) - Mathf.Abs(y+.1f) * .65f) * 7);
                case ElementType.Water: return Mathf.Clamp01((.75f - Mathf.Sqrt(x*x*1.6f + (y+.2f)*(y+.2f)) - Mathf.Max(0,y) * .3f) * 12);
                case ElementType.Nature: return Mathf.Clamp01((1 - (x+y)*(x+y)*5 - (y-x)*(y-x)*.7f) * 5) * (Mathf.Abs(x-y*.8f) < .04f ? .3f : 1);
                case ElementType.Earth: return Mathf.Clamp01((.65f - Mathf.Max(Mathf.Abs(x)*.9f, Mathf.Abs(y)) - .15f*Mathf.Abs(x+y)) * 25);
                case ElementType.Lightning: return Mathf.Abs(y) < .8f ? Mathf.Clamp01((.13f - Mathf.Abs(x - .3f*y + (y>0 ? .12f : -.12f))) * 45) : 0;
                case ElementType.Wind: return Mathf.Clamp01((.09f - Mathf.Abs(r - (.35f + .15f * Mathf.Sin(a*2+r*5)))) * 40);
                case ElementType.Darkness: return Mathf.Clamp01((.085f - Mathf.Abs(r-.5f)) * 30) + Mathf.Clamp01((.12f-Mathf.Min(Mathf.Abs(x),Mathf.Abs(y))) * 12) * Mathf.Clamp01((.65f-r)*5);
                default: return Mathf.Clamp01((.08f-Mathf.Abs(r-.5f))*40);
            }
        }
        private static GameObject MakeParticles(ElementType type, string label, Material material, int mode)
        {
            var go = new GameObject(type + " " + label);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.duration = mode == 2 ? .8f : 2; main.loop = mode != 2;
            main.startLifetime = mode == 2 ? new ParticleSystem.MinMaxCurve(.35f,.85f) : new ParticleSystem.MinMaxCurve(.3f,.85f);
            main.startSpeed = mode == 2 ? new ParticleSystem.MinMaxCurve(.6f,2.8f) : new ParticleSystem.MinMaxCurve(.03f,.3f);
            main.startSize = type == ElementType.Lightning ? new ParticleSystem.MinMaxCurve(.1f,.22f) : new ParticleSystem.MinMaxCurve(.035f,.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(Colors[(int)type], Color.Lerp(Colors[(int)type],Color.white,.4f));
            main.maxParticles = mode == 2 ? 70 : 45; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);
            var emission = ps.emission; emission.rateOverTime = mode == 2 ? 0 : type == ElementType.Lightning ? 18 : 28;
            if (mode == 2) emission.SetBursts(new[] { new ParticleSystem.Burst(0, 36) });
            var shape = ps.shape; shape.enabled = true;
            shape.shapeType = mode == 0 ? ParticleSystemShapeType.Box : ParticleSystemShapeType.Sphere;
            shape.scale = mode == 0 ? new Vector3(.7f,.04f,.04f) : Vector3.one;
            shape.radius = mode == 1 ? .24f : .12f;
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = type == ElementType.Fire ? .4f : type == ElementType.Water ? -.15f : .05f;
            if (type == ElementType.Wind || type == ElementType.Darkness || mode == 1) { velocity.orbitalZ = type == ElementType.Darkness ? -2 : 2; velocity.radial = .05f; }
            var noise = ps.noise; noise.enabled = type != ElementType.Earth; noise.strength = type == ElementType.Lightning ? .22f : .07f; noise.frequency = type == ElementType.Lightning ? 4 : 1;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0,1,1,0));
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Colors[(int)type],1) }, new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.12f), new GradientAlphaKey(0,1) }); color.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            // A soft, luminous core gives both weapons a visible elemental glow without editing weapon materials.
            var glow = new GameObject("Energy Glow"); glow.transform.SetParent(go.transform, false);
            var glowPs = glow.AddComponent<ParticleSystem>(); glowPs.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var gm = glowPs.main; gm.duration=1; gm.loop=mode!=2; gm.startLifetime=.3f; gm.startSpeed=0; gm.startSize=mode==1?.35f:.2f;
            gm.startColor=new Color(Colors[(int)type].r,Colors[(int)type].g,Colors[(int)type].b,.18f); gm.maxParticles=8;
            var ge=glowPs.emission; ge.rateOverTime=mode==2?0:12; if(mode==2) ge.SetBursts(new[]{new ParticleSystem.Burst(0,4)});
            var gs=glowPs.shape; gs.shapeType=ParticleSystemShapeType.Sphere; gs.radius=.04f;
            glowPs.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/VFX/{type} {label}.prefab");
            UnityEngine.Object.DestroyImmediate(go); return prefab;
        }
    }
}
