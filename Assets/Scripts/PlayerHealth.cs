using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 100;

    public static PlayerHealth Instance { get; private set; }

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    public event Action<int, int> HealthChanged;
    public event Action Died;

    private void Awake()
    {
        Instance = this;
        ResetHealth();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static bool TryDamage(int amount, UnityEngine.Object source = null)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[PlayerHealth] No hay PlayerHealth activo para recibir dano.", source);
            return false;
        }

        Instance.TakeDamage(amount);
        return true;
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDead)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0)
        {
            Died?.Invoke();
        }
    }
}
