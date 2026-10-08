using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerOverall : MonoBehaviour
{

	public int maxHealth = 100;
	public int currentHealth;

	public HealthBar healthBar;

    // Start is called before the first frame update
    void Start()
    {
		currentHealth = maxHealth;
		if (healthBar != null) healthBar.SetMaxHealth(maxHealth);
    }

    // Update is called once per frame
    void Update()
    {
		
    }

	public void Heal(int amount)
    {
        if (currentHealth <= 0) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0, amount));
        if (healthBar != null) healthBar.SetHealth(currentHealth);
    }

    public void TakeDamage(int damage)
	{
		var gems = GetComponent<ElementalGems.GemManager>();
        damage = gems != null ? gems.ReduceIncomingDamage(damage) : Mathf.Max(0, damage);
        currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);

		if (healthBar != null) healthBar.SetHealth(currentHealth);
	}
}