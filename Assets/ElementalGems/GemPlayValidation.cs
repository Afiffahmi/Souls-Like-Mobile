using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalGems.Editor
{
    public sealed class GemPlayValidation : MonoBehaviour
    {
        public static string Report="Not run";
        private readonly List<GameObject> temporary=new List<GameObject>();
        private readonly List<string> passed=new List<string>();
        private GemManager manager;
        private ElementType original;
        private bool persist;
        private int originalHealth;
        public static string Begin()
        {
            if(!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            Report="Running";
            new GameObject("Gem Validation (Temporary)").AddComponent<GemPlayValidation>();return Report;
        }
        private void Check(bool ok,string label) { if(!ok) throw new Exception(label);passed.Add(label); }
        private void Start()
        {
            manager=FindFirstObjectByType<GemManager>();original=manager.EquippedElement;persist=manager.persistSelection;manager.persistSelection=false;
            originalHealth=manager.GetComponent<PlayerOverall>().currentHealth;
            StartCoroutine(Guard(Tests()));
        }
        private IEnumerator Guard(IEnumerator routine)
        {
            while(true)
            {
                bool more;object current=null;
                try { more=routine.MoveNext();if(more)current=routine.Current; }
                catch(Exception e){Report="FAIL: "+e.Message+"\nPassed: "+string.Join("; ",passed);break;}
                if(!more){Report="PASS: "+passed.Count+" Play Mode checks\n"+string.Join("\n",passed);break;}
                yield return current;
            }
            manager.Equip(original,false);manager.persistSelection=persist;manager.GetComponent<PlayerOverall>().currentHealth=originalHealth;
            foreach(var go in temporary)if(go!=null)Destroy(go);Destroy(gameObject);
        }
        private Enemy Target(ElementType element,Vector3 position)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);temporary.Add(go);go.name="Gem Test Enemy";go.transform.position=position;
            var enemy=go.AddComponent<Enemy>();go.AddComponent<ElementalEnemy>().element=element;return enemy;
        }
        private IEnumerator Tests()
        {
            var point=new Vector3(2000,100,2000);
            var playerHealth=manager.GetComponent<PlayerOverall>();
            var target=Target(ElementType.Nature,point);
            manager.Equip(ElementType.Fire,false);var snapshot=manager.Capture();
            int direct=ElementalDamage.Hit(snapshot,18,manager,target,point,Vector3.forward);
            Check(direct==45&&target.GetComponent<ElementalEnemy>().HasStatus(StatusKind.Burn),"Fire damage and burn use existing Enemy health");
            yield return new WaitForSeconds(1.1f);
            Check(target.CurrentHealth<55,"Burn ticks over time");
            var arrowTarget=Target(ElementType.Nature,point+Vector3.right*4);
            var shooter=manager.GetComponent<PlayerBowShooter>();
            var arrow=Instantiate(shooter.arrowPrefab,arrowTarget.transform.position-Vector3.forward*2,Quaternion.identity);temporary.Add(arrow.gameObject);
            var payload=arrow.gameObject.AddComponent<GemArrowPayload>();payload.Initialize(snapshot,manager,18,2);
            Physics.SyncTransforms();arrow.Launch(manager.transform,Vector3.forward,15,3,~0);
            manager.Equip(ElementType.Water,false);
            yield return new WaitForSeconds(.2f);
            Check(payload.Element==ElementType.Fire&&!arrow.IsFlying,"In-flight arrow retains Fire after equipping Water");
            Check(arrowTarget.CurrentHealth<=55&&arrowTarget.CurrentHealth>=53,"Swept arrow collision applies the same elemental damage as melee");
            Check(arrowTarget.GetComponent<ElementalEnemy>().HasStatus(StatusKind.Burn),"Arrow applies original burn, not the newly equipped slow");
            int once=arrowTarget.CurrentHealth;payload.Impact(arrowTarget.GetComponent<Collider>(),arrowTarget.transform.position,Vector3.up,false);
            Check(once==arrowTarget.CurrentHealth,"Duplicate impact cannot damage twice");
            var water=Target(ElementType.Fire,point+Vector3.right*8);var waterElement=water.GetComponent<ElementalEnemy>();
            ElementalDamage.Hit(manager.Capture(),10,manager,water,water.transform.position,Vector3.forward);
            Check(waterElement.MovementMultiplier<1,"Water slows");var old=water.transform.position;
            yield return new WaitForSeconds(.2f);Check(Vector3.Distance(old,water.transform.position)>.01f,"Water knockback moves target");
            manager.Equip(ElementType.Nature,false);playerHealth.currentHealth=50;
            var nature=Target(ElementType.Earth,point+Vector3.right*12);
            ElementalDamage.Hit(manager.Capture(),18,manager,nature,nature.transform.position,Vector3.forward);
            var status=nature.GetComponent<ElementalEnemy>();
            Check(status.IsRooted&&status.HasStatus(StatusKind.Poison)&&playerHealth.currentHealth>50,"Nature roots, poisons and heals player");
            status.ClearStatuses();Check(status.MovementMultiplier==1&&!status.IsRooted,"Status clear restores movement");
            manager.Equip(ElementType.Earth,false);playerHealth.currentHealth=100;playerHealth.TakeDamage(40);
            Check(playerHealth.currentHealth==70,"Earth armor integrates with existing player damage");
            var earth=Target(ElementType.Lightning,point+Vector3.right*16);ElementalDamage.Hit(manager.Capture(),10,manager,earth,earth.transform.position,Vector3.forward);
            Check(earth.GetComponent<ElementalEnemy>().IsStunned,"Earth stagger interrupts");
            yield return new WaitForSeconds(.5f);Check(!earth.GetComponent<ElementalEnemy>().IsStunned,"Stagger expires");
            manager.Equip(ElementType.Lightning,false);Check(manager.MovementScale>1,"Lightning grants speed");
            var lightning=manager.Capture();for(int i=0;i<lightning.statuses.Length;i++){var s=lightning.statuses[i];s.chance=1;lightning.statuses[i]=s;}
            earth.GetComponent<ElementalEnemy>().Apply(lightning,Vector3.zero);Check(earth.GetComponent<ElementalEnemy>().HasStatus(StatusKind.Stun),"Lightning stun supported");
            manager.Equip(ElementType.Wind,false);var wind=manager.Capture();manager.RegisterHit(wind,10);
            Check(manager.Capture().damageScale>wind.damageScale&&wind.projectileSpeed>1&&manager.MovementScale>1,"Wind combo, projectile and mobility bonuses");
            manager.Equip(ElementType.Darkness,false);var dark=manager.Capture();
            Check(ElementalDamage.Calculate(dark,20,ElementType.Normal,1,.2f)==2*ElementalDamage.Calculate(dark,20,ElementType.Normal),"Darkness executes low-health targets");
            playerHealth.currentHealth=40;var darkness=Target(ElementType.Normal,point+Vector3.right*20);ElementalDamage.Hit(dark,20,manager,darkness,darkness.transform.position,Vector3.forward);
            Check(playerHealth.currentHealth>40&&darkness.GetComponent<ElementalEnemy>().HasStatus(StatusKind.Void),"Darkness life steal and void damage");
            manager.Equip(ElementType.Normal,false);Check(manager.MovementScale==1&&manager.Capture().statuses.Length==0&&manager.ReduceIncomingDamage(40)==40,"Normal removes all gem bonuses");
            var ui=FindFirstObjectByType<GemSelectionUI>();ui.openButton.onClick.Invoke();
            var fireCard=Array.Find(ui.cards,c=>c.gem.element==ElementType.Fire);fireCard.button.onClick.Invoke();ui.equipButton.onClick.Invoke();
            Check(ui.panel.activeSelf&&manager.EquippedElement==ElementType.Fire,"Gem UI selection and Equip button work");ui.closeButton.onClick.Invoke();Check(!ui.panel.activeSelf,"Gem UI closes");
            // Test real save/reload, restoring the user's existing preference afterward.
            const string key="ElementalGems.Equipped.v1";bool had=PlayerPrefs.HasKey(key);int saved=PlayerPrefs.GetInt(key);
            try
            {
                manager.persistSelection=true;manager.Equip(ElementType.Water);
                var reload=new GameObject("Gem save reload test");temporary.Add(reload);reload.SetActive(false);
                var restored=reload.AddComponent<GemManager>();restored.gems=manager.gems;reload.SetActive(true);
                Check(restored.EquippedElement==ElementType.Water,"Equipped gem survives manager restart via PlayerPrefs");
            }
            finally {manager.persistSelection=false;if(had)PlayerPrefs.SetInt(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
        }
    }
}
