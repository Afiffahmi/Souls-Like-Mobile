using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Enemy : MonoBehaviour
{
    public event Action OnDeath;
    public event Action<int> OnDamaged;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;
    [SerializeField] private HealthBar healthBar;

    [Header("Lock-On")]
    [Tooltip("If empty, auto-finds all LockOnTarget components on this GameObject and children.")]
    [SerializeField] private List<LockOnTarget> lockOnTargets = new List<LockOnTarget>();

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    public bool IsDead => currentHealth <= 0;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;

    private void Awake()
    {
        if (lockOnTargets.Count == 0)
        {
            lockOnTargets.AddRange(GetComponentsInChildren<LockOnTarget>(includeInactive: true));
        }

        if (currentHealth <= 0)
        {
            currentHealth = maxHealth;
        }
    }

    private void Start()
    {
        if (healthBar != null)
        {
            healthBar.SetMaxHealth(maxHealth);
            healthBar.SetHealth(currentHealth);
        }
    }

    public void TakeDamage(int damage)
    {
        if (IsDead) return;
        if (damage < 0) damage = 0;

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        if (debugLog) Debug.Log($"[Enemy] {name} took {damage} damage. HP: {currentHealth}/{maxHealth}");

        OnDamaged?.Invoke(damage);

        if (healthBar != null)
        {
            healthBar.SetHealth(currentHealth);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead) return;
        if (amount < 0) amount = 0;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

        if (debugLog) Debug.Log($"[Enemy] {name} healed {amount}. HP: {currentHealth}/{maxHealth}");

        if (healthBar != null)
        {
            healthBar.SetHealth(currentHealth);
        }
    }

    public void ForceKill()
    {
        if (IsDead) return;
        currentHealth = 0;
        Die();
    }

    private void Die()
    {
        if (debugLog) Debug.Log($"[Enemy] {name} died. Broadcasting OnDeath.");

        foreach (LockOnTarget target in lockOnTargets)
        {
            if (target != null)
            {
                target.SetTargetable(false);
            }
        }

        OnDeath?.Invoke();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }
}
