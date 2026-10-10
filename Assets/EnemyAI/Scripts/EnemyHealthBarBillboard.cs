using UnityEngine;

namespace SoulsLike.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(Canvas), typeof(HealthBar))]
    public sealed class EnemyHealthBarBillboard : MonoBehaviour
    {
        public Enemy health;
        public Vector3 worldOffset = new Vector3(0, 3.15f, 0);
        private Canvas canvas;
        private HealthBar view;
        private Camera facingCamera;
        private float nextCameraLookup;

        private void Awake()
        {
            canvas = GetComponent<Canvas>(); view = GetComponent<HealthBar>();
            if (health == null) health = GetComponentInParent<Enemy>();
        }
        private void OnEnable()
        {
            if (health != null) health.OnDeath += HideOnDeath;
        }
        private void Start()
        {
            if (health == null) return;
            view.SetMaxHealth(health.MaxHealth); view.SetHealth(health.CurrentHealth);
            canvas.enabled = !health.IsDead;
        }
        private void OnDisable()
        {
            if (health != null) health.OnDeath -= HideOnDeath;
        }
        private void HideOnDeath()
        {
            view.SetHealth(0);
            canvas.enabled = false;
        }
        private void LateUpdate()
        {
            if (health == null || !canvas.enabled) return;
            if ((facingCamera == null || !facingCamera.isActiveAndEnabled) && Time.unscaledTime >= nextCameraLookup)
            {
                facingCamera = Camera.main;
                canvas.worldCamera = facingCamera;
                nextCameraLookup = Time.unscaledTime + .5f;
            }
            transform.position = health.transform.position + worldOffset;
            if (facingCamera != null) transform.rotation = facingCamera.transform.rotation;
        }
    }
}
