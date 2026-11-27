using System.Collections;
using UnityEngine;

namespace KernelKnight.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public class DummyEnemy : MonoBehaviour, IDamageable, IPogoable, IParryable
    {
        [Header("Stats")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        [Header("Attack Behavior (For Parry Testing)")]
        [SerializeField] private bool autoAttack = true;
        [SerializeField] private float attackInterval = 3.5f;
        [SerializeField] private float telegraphDuration = 0.8f;
        [SerializeField] private float attackActiveDuration = 0.3f;
        [SerializeField] private float staggerDuration = 1.5f;
        [SerializeField] private float attackRange = 2.0f;
        [SerializeField] private float attackDamage = 15f;

        [Header("Visual Feedback")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private MeshRenderer meshRenderer;

        public enum EnemyState { Idle, Telegraphing, Attacking, Staggered }
        public EnemyState CurrentState { get; private set; } = EnemyState.Idle;

        private Color originalColor = Color.white;
        private Coroutine attackRoutine;
        private Rigidbody2D rb;

        private void Awake()
        {
            currentHealth = maxHealth;
            rb = GetComponent<Rigidbody2D>();

            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            if (spriteRenderer != null) originalColor = spriteRenderer.color;
            else if (meshRenderer != null && meshRenderer.sharedMaterial != null) originalColor = meshRenderer.sharedMaterial.color;
        }

        private void Start()
        {
            if (autoAttack)
            {
                attackRoutine = StartCoroutine(AutoAttackLoop());
            }
        }

        private void Update()
        {
            // Debug key to trigger an attack manually on demand for parry timing testing
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.tKey.wasPressedThisFrame)
            {
                TriggerManualAttack();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.T))
            {
                TriggerManualAttack();
            }
#endif
        }

        public void TriggerManualAttack()
        {
            if (CurrentState == EnemyState.Idle)
            {
                if (attackRoutine != null) StopCoroutine(attackRoutine);
                attackRoutine = StartCoroutine(ExecuteAttackPattern());
            }
        }

        private IEnumerator AutoAttackLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(attackInterval);
                if (CurrentState == EnemyState.Idle)
                {
                    yield return StartCoroutine(ExecuteAttackPattern());
                }
            }
        }

        private IEnumerator ExecuteAttackPattern()
        {
            // 1. Telegraph
            CurrentState = EnemyState.Telegraphing;
            SetVisualColor(new Color(1f, 0.7f, 0.1f)); // Orange warning
            yield return new WaitForSeconds(telegraphDuration);

            // 2. Active Attack Window
            CurrentState = EnemyState.Attacking;
            SetVisualColor(new Color(1f, 0.1f, 0.1f)); // Bright Red Danger

            // Check overlap with Bit
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player") || hit.name == "Bit")
                {
                    var playerCombat = hit.GetComponent<Player.PlayerCombatController>();
                    if (playerCombat != null)
                    {
                        // Check if Bit successfully parries this attack
                        if (playerCombat.CheckParrySuccess(gameObject))
                        {
                            yield break; // Attack was parried!
                        }
                    }

                    // Parry missed or inactive -> apply damage & knockback to Bit
                    var playerDamageable = hit.GetComponent<IDamageable>();
                    if (playerDamageable != null)
                    {
                        Vector2 kbDir = (hit.transform.position - transform.position).normalized;
                        if (Mathf.Abs(kbDir.x) < 0.05f) kbDir.x = (hit.transform.position.x >= transform.position.x) ? 1f : -1f;
                        playerDamageable.TakeDamage(attackDamage, kbDir, 7.5f);
                        break;
                    }
                }
            }

            yield return new WaitForSeconds(attackActiveDuration);

            // 3. Return to Idle
            CurrentState = EnemyState.Idle;
            SetVisualColor(originalColor);
        }

        public void OnParried(GameObject parrier)
        {
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            StartCoroutine(StaggerRoutine());
        }

        private IEnumerator StaggerRoutine()
        {
            CurrentState = EnemyState.Staggered;
            Debug.Log($"<color=cyan><b>[ENEMY]</b> {gameObject.name} is STAGGERED from Parry!</color>");
            SetVisualColor(new Color(0.2f, 0.8f, 1f)); // Cyan Stagger

            yield return new WaitForSeconds(staggerDuration);

            CurrentState = EnemyState.Idle;
            SetVisualColor(originalColor);

            if (autoAttack)
            {
                attackRoutine = StartCoroutine(AutoAttackLoop());
            }
        }

        public void TakeDamage(float damage, Vector2 knockbackDirection, float knockbackForce)
        {
            currentHealth -= damage;
            Debug.Log($"<color=yellow><b>[DAMAGE]</b> {gameObject.name} took {damage} dmg (HP: {currentHealth}/{maxHealth})</color>");

            StartCoroutine(DamageFlashRoutine());

            if (rb != null && knockbackForce > 0f)
            {
                rb.linearVelocity = knockbackDirection.normalized * knockbackForce;
            }

            if (currentHealth <= 0)
            {
                currentHealth = maxHealth; // Reset for continuous testing in Test Arena
                Debug.Log($"<color=red><b>[RESPAWN]</b> Dummy Target HP reset for further testing.</color>");
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            SetVisualColor(Color.white);
            yield return new WaitForSeconds(0.08f);
            SetVisualColor(CurrentState == EnemyState.Staggered ? new Color(0.2f, 0.8f, 1f) : originalColor);
        }

        public bool OnPogoHit(Vector2 hitPoint)
        {
            StartCoroutine(PogoSquishRoutine());
            return true;
        }

        private IEnumerator PogoSquishRoutine()
        {
            Vector3 origScale = transform.localScale;
            transform.localScale = new Vector3(origScale.x * 1.3f, origScale.y * 0.7f, origScale.z);
            yield return new WaitForSeconds(0.12f);
            transform.localScale = origScale;
        }

        private void SetVisualColor(Color c)
        {
            if (spriteRenderer != null) spriteRenderer.color = c;
            else if (meshRenderer != null && meshRenderer.material != null) meshRenderer.material.color = c;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = CurrentState switch
            {
                EnemyState.Telegraphing => new Color(1f, 0.7f, 0f, 0.5f),
                EnemyState.Attacking => new Color(1f, 0f, 0f, 0.7f),
                EnemyState.Staggered => new Color(0f, 0.8f, 1f, 0.6f),
                _ => new Color(1f, 1f, 1f, 0.2f)
            };

            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
