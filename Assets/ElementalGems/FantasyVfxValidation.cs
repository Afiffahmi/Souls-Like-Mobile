#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ElementalGems
{
    /// <summary>Editor-only visual contract checks. Temporary objects are never saved.</summary>
    public sealed class FantasyVfxValidation : MonoBehaviour
    {
        public static string Report="Not run";
        private GemManager manager;
        private ElementType original;
        private GameObject testRoot;
        private readonly List<string> passes=new List<string>();
        public static string Begin()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required.");
            Report="Running";new GameObject("Temporary VFX checks").AddComponent<FantasyVfxValidation>();return Report;
        }
        private void Start(){manager=FindAnyObjectByType<GemManager>();original=manager.EquippedElement;StartCoroutine(Guard(CheckVisuals()));}
        private void Check(bool condition,string label){if(!condition)throw new Exception(label);passes.Add(label);}
        private IEnumerator Guard(IEnumerator tests)
        {
            while(true)
            {
                bool more;object result=null;
                try{more=tests.MoveNext();if(more)result=tests.Current;}
                catch(Exception e){Report="FAIL: "+e.Message+"\nPassed: "+string.Join("\n",passes);break;}
                if(!more){Report="PASS: "+passes.Count+" VFX checks\n"+string.Join("\n",passes);break;}
                yield return result;
            }
            manager.Equip(original,false);if(testRoot!=null)Destroy(testRoot);Destroy(gameObject);
        }
        private IEnumerator CheckVisuals()
        {
            var effects=manager.GetComponent<GemWeaponEffects>();
            var player=manager.GetComponent<PlayerStateManager>();
            Check(effects.styles.Length==7,"Seven visual styles assigned");
            foreach(var style in effects.styles)
            {
                manager.Equip(style.element,false);yield return null;
                var active=manager.GetComponentsInChildren<GemVfxTuning>();
                Check(active.Length==1&&active[0].style.element==style.element,style.element+": only the active weapon aura is enabled");
                Check(active[0].GetComponentsInChildren<ParticleSystem>().Length+active[0].GetComponentsInChildren<GemVfxRibbons>().Length>=4,style.element+": distinct particle and ribbon layers present");
            }
            manager.Equip(ElementType.Normal,false);yield return null;
            Check(manager.GetComponentsInChildren<GemVfxTuning>().Length==0,"Normal removes all weapon auras");
            manager.Equip(ElementType.Lightning,false);yield return null;
            var lines=manager.GetComponentsInChildren<LineRenderer>();
            Check(lines.Length>=4&&lines[0].positionCount>=8,"Lightning uses visible multi-segment bolt geometry");
            var point=lines[0].GetPosition(3);yield return new WaitForSeconds(.12f);
            Check((point-lines[0].GetPosition(3)).sqrMagnitude>.000001f,"Lightning geometry animates");
            var bow=manager.GetComponent<PlayerBowVisuals>();
            var activeAura=manager.GetComponentInChildren<GemVfxTuning>();
            if(player.CombatMode==PlayerCombatMode.Bow)Check(Vector3.Distance(activeAura.transform.lossyScale,Vector3.one)<.1f,"Bow FBX scale is compensated");
            testRoot=new GameObject("Temporary VFX projectile test");testRoot.transform.position=new Vector3(3000,300,3000);
            var shooter=manager.GetComponent<PlayerBowShooter>();
            var arrow=Instantiate(shooter.arrowPrefab,testRoot.transform.position,Quaternion.identity);arrow.transform.SetParent(testRoot.transform,true);
            var payload=arrow.GetComponent<GemArrowPayload>()??arrow.gameObject.AddComponent<GemArrowPayload>();
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(testRoot.transform,false);wall.transform.localPosition=Vector3.forward*1.8f;
            Physics.SyncTransforms();payload.Initialize(manager.Capture(),manager,0,0);arrow.Launch(manager.transform,Vector3.forward,2,5,~0);
            yield return null;yield return null;
            Check(arrow.GetComponentInChildren<GemVfxTuning>()?.style.element==ElementType.Lightning,"Flying arrow creates Lightning arrowhead VFX");
            Check(arrow.GetComponentInChildren<TrailRenderer>()!=null,"Flying arrow has an elemental trail");
            manager.Equip(ElementType.Water,false);yield return null;
            Check(arrow.GetComponentInChildren<GemVfxTuning>()?.style.element==ElementType.Lightning,"Gem switch preserves the flying arrow's original VFX");
            yield return new WaitForSeconds(.8f);
            Check(!arrow.IsFlying&&arrow.GetComponentInChildren<GemVfxTuning>()==null,"Real projectile impact removes the flying arrowhead aura");
            var impacts=FindObjectsByType<GemVfxTuning>(FindObjectsInactive.Exclude);
            bool impact=false;foreach(var fx in impacts)if(fx.name.Contains("Lightning Impact")&&fx.GetComponentsInChildren<ParticleSystem>().Length>=4){impact=true;Destroy(fx.gameObject);}
            Check(impact,"Original-element impact prefab is spawned through the existing impact path");
            foreach(var style in effects.styles)
            {
                var go=Instantiate(style.impact,testRoot.transform.position,Quaternion.identity);go.transform.SetParent(testRoot.transform,true);
                yield return null;
                int count=0;foreach(var ps in go.GetComponentsInChildren<ParticleSystem>())count+=ps.particleCount;
                Check(count>0,style.element+": impact emits actual particles");Destroy(go);
            }
        }
    }
}
#endif
