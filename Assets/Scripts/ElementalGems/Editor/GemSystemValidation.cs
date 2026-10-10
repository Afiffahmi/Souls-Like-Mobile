using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ElementalGems.Editor
{
    public static class GemSystemValidation
    {
        public static string RunAssetChecks()
        {
            int checks=0;
            void Check(bool ok,string message) { if(!ok) throw new Exception(message); checks++; }
            var gems=AssetDatabase.FindAssets("t:GemDefinition",new[]{GemSystemSetup.Root}).Select(id=>AssetDatabase.LoadAssetAtPath<GemDefinition>(AssetDatabase.GUIDToAssetPath(id))).ToArray();
            Check(gems.Length==8,"Eight gem definitions required.");
            var strong=new[]{ElementType.Normal,ElementType.Nature,ElementType.Fire,ElementType.Earth,ElementType.Lightning,ElementType.Water,ElementType.Normal,ElementType.Normal};
            var weak=new[]{ElementType.Normal,ElementType.Water,ElementType.Lightning,ElementType.Fire,ElementType.Nature,ElementType.Earth,ElementType.Normal,ElementType.Normal};
            foreach(var gem in gems)
            {
                Check(gem.icon!=null,gem.name+" icon missing.");
                foreach(ElementType defender in Enum.GetValues(typeof(ElementType)))
                {
                    int e=(int)gem.element;
                    float expected=e>0&&e<6 ? defender==strong[e]?2:defender==weak[e]?.5f:1 : 1;
                    Check(Mathf.Approximately(gem.MultiplierAgainst(defender),expected),$"Wrong matchup {gem.element} to {defender}");
                }
                if(gem.element==ElementType.Normal) continue;
                Check(gem.swordAura!=null&&gem.bowAura!=null&&gem.impact!=null&&gem.trailMaterial!=null,gem.name+" VFX incomplete");
                foreach(var prefab in new[]{gem.swordAura,gem.bowAura,gem.impact})
                    Check(prefab.GetComponentsInChildren<ParticleSystem>().Length>0&&prefab.GetComponentsInChildren<ParticleSystemRenderer>().All(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.isSupported),prefab.name+" particles/material invalid");
            }
            var fire=gems.Single(g=>g.element==ElementType.Fire);
            Check(ElementalDamage.Calculate(new GemAttack(fire),20,ElementType.Nature)==50,"Fire strong damage");
            Check(ElementalDamage.Calculate(new GemAttack(fire),20,ElementType.Water)==12,"Fire weak rounding");
            Check(ElementalDamage.Calculate(new GemAttack(fire),20,ElementType.Wind,0)==0,"Immunity must zero damage");
            var copy=UnityEngine.Object.Instantiate(fire); var captured=new GemAttack(copy); copy.damageScale=99;copy.matchups[0]=new ElementMatchup{defender=ElementType.Nature,multiplier=99};copy.statuses[0]=default;
            Check(captured.damageScale==1.25f&&captured.MultiplierAgainst(ElementType.Nature)==2&&captured.statuses[0].kind==StatusKind.Burn&&captured.statuses[0].duration>0,"Attack snapshot mutated");UnityEngine.Object.DestroyImmediate(copy);
            var dark=UnityEngine.Object.Instantiate(gems.Single(g=>g.element==ElementType.Darkness));
            dark.matchups=new[]{new ElementMatchup{defender=ElementType.Fire,multiplier=2},new ElementMatchup{defender=ElementType.Water,multiplier=.5f}};
            Check(dark.MultiplierAgainst(ElementType.Fire)==2&&dark.MultiplierAgainst(ElementType.Water)==.5f,"Darkness configuration ignored");UnityEngine.Object.DestroyImmediate(dark);
            var manager=UnityEngine.Object.FindFirstObjectByType<GemManager>();
            Check(manager!=null&&manager.gems.Length==8,"Scene manager missing");
            Check(UnityEngine.Object.FindObjectsByType<GemManager>(FindObjectsSortMode.None).Length==1,"More than one shared manager");
            var ui=UnityEngine.Object.FindFirstObjectByType<GemSelectionUI>();
            Check(ui!=null&&ui.manager==manager&&ui.cards.Length==8&&(ui.compact ? ui.previousButton!=null&&ui.nextButton!=null&&ui.equippedIcon!=null : ui.equipButton!=null),"UI bindings incomplete");
            Check(manager.GetComponent<GemSwordCombat>().blade!=null&&manager.GetComponent<GemWeaponEffects>().sword!=null,"Sword bindings missing");
            Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length==1,"Expected one EventSystem");
            return $"PASS: {checks} asset, matchup, snapshot, UI, and scene integration checks.";
        }
    }
}
