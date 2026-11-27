using System;
using System.Collections;
using UnityEngine;
using KernelKnight.Combat;

namespace KernelKnight.Player
{
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerHealthController : MonoBehaviour, IDamageable
    {
        [Header("Integrity (Mask / HP Nodes)")]
        [SerializeField] private int maxIntegrity = 5;
        [SerializeField] private int currentIntegrity = 5;
        [SerializeField] private float invulnerabilityDuration = 1.2f;
        [SerializeField] private float hitStopDuration = 0.08f;
        [SerializeField] private float hitKnockbackHorizontal = 7.5f;
        [SerializeField] private float hitKnockbackVertical = 4.5f;
        [SerializeField] private float hitStunDuration = 0.22f;
        [SerializeField] private float hurtAnimDuration = 0.35f;

        [Header("Buffer Energy & Patching (Heal)")]
        [SerializeField] private float maxBuffer = 100f;
        [SerializeField] private float currentBuffer = 0f;
        [SerializeField] private float bufferGainPerHit = 15f;
        [SerializeField] private float healCost = 33f;
        [SerializeField] private float healChannelDuration = 0.85f;

        [Header("Debug")]
        [SerializeField] private bool showDebugHUD = true;

        // Public Properties
        public int MaxIntegrity => maxIntegrity;
        public int CurrentIntegrity => currentIntegrity;
        public float MaxBuffer => maxBuffer;
        public float CurrentBuffer => currentBuffer;
        public bool IsInvulnerable => isInvulnerable;
        public bool IsHealing => isHealing;
        public bool IsDead { get; private set; }
        public float HealProgress => (healChannelDuration > 0f) ? Mathf.Clamp01(healTimer / healChannelDuration) : 0f;

        // Events for HUD / Audio / VFX
        public event Action<int, int> OnHealthChanged;
        public event Action<float, float> OnBufferChanged;
        public event Action OnPlayerDied;

        // Components
        private PlayerController playerController;
        private PlayerCombatController combatController;
        private PlayerInputHandler inputHandler;
        private SpriteRenderer spriteRenderer;
        private Animator animator;

        private bool isInvulnerable;
        private bool isHealing;
        private float healTimer;
        private Coroutine iFrameRoutine;
        private Coroutine hurtAnimRoutine;

        private static readonly int IsHealingHash = Animator.StringToHash("IsHealing");
        private static readonly int HealStateHash = Animator.StringToHash("Heal");
        private static readonly int HurtTrigger = Animator.StringToHash("Hurt");
        private static readonly int IsHurtingHash = Animator.StringToHash("IsHurting");
        private static readonly int HurtStateHash = Animator.StringToHash("Hurt");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
        private static readonly int DeathTrigger = Animator.StringToHash("Death");
        private static readonly int DeathStateHash = Animator.StringToHash("Death");

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            combatController = GetComponent<PlayerCombatController>();
            inputHandler = GetComponent<PlayerInputHandler>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();

            currentIntegrity = Mathf.Clamp(currentIntegrity, 1, maxIntegrity);
            currentBuffer = Mathf.Clamp(currentBuffer, 0f, maxBuffer);
        }

        private void OnEnable()
        {
            if (combatController != null)
            {
                combatController.OnAttackHitEnemy += HandleAttackHitEnemy;
            }
        }

        private void OnDisable()
        {
            if (combatController != null)
            {
                combatController.OnAttackHitEnemy -= HandleAttackHitEnemy;
            }
            StopHealing();
        }

        private void Update()
        {
            if (IsDead || inputHandler == null || playerController == null) return;

#if ENABLE_INPUT_SYSTEM
            // Debug shortcut [K]: test damage (down to 0 / Death) + buffer gain quickly in SampleScene
            if (showDebugHUD && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.kKey.wasPressedThisFrame)
            {
                if (iFrameRoutine != null)
                {
                    StopCoroutine(iFrameRoutine);
                    iFrameRoutine = null;
                }
                isInvulnerable = false;
                if (spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a = 1f;
                    spriteRenderer.color = c;
                }

                TakeDamage(1f, Vector2.zero, 0f);
                if (!IsDead)
                {
                    currentBuffer = Mathf.Min(maxBuffer, currentBuffer + 35f);
                    OnBufferChanged?.Invoke(currentBuffer, maxBuffer);
                }
            }
#endif

            HandlePatchingInput();
        }

        private void HandleAttackHitEnemy()
        {
            if (IsDead || currentBuffer >= maxBuffer) return;

            currentBuffer = Mathf.Min(maxBuffer, currentBuffer + bufferGainPerHit);
            OnBufferChanged?.Invoke(currentBuffer, maxBuffer);
        }

        private void HandlePatchingInput()
        {
            bool canChannel = playerController.IsGrounded &&
                              !playerController.IsDashing &&
                              (combatController == null || (!combatController.IsAttacking && combatController.CurrentParryState == PlayerCombatController.ParryState.Inactive)) &&
                              currentIntegrity < maxIntegrity &&
                              currentBuffer >= healCost &&
                              inputHandler.HealHeld;

            if (canChannel)
            {
                if (!isHealing)
                {
                    isHealing = true;
                    healTimer = 0f;
                    playerController.SetMovementHalted(true);
                    if (animator != null)
                    {
                        animator.SetBool(IsHealingHash, true);
                        if (animator.HasState(0, HealStateHash))
                        {
                            animator.Play(HealStateHash, 0, 0f);
                        }
                    }
                }

                healTimer += Time.deltaTime;
                if (healTimer >= healChannelDuration)
                {
                    CompletePatch();
                }
            }
            else if (isHealing)
            {
                StopHealing();
            }
        }

        private void CompletePatch()
        {
            currentBuffer = Mathf.Max(0f, currentBuffer - healCost);
            currentIntegrity = Mathf.Min(maxIntegrity, currentIntegrity + 1);
            healTimer = 0f;

            OnBufferChanged?.Invoke(currentBuffer, maxBuffer);
            OnHealthChanged?.Invoke(currentIntegrity, maxIntegrity);

            Debug.Log($"<color=green><b>[PATCHING COMPLETE]</b> Integrity: {currentIntegrity}/{maxIntegrity} | Buffer: {currentBuffer:F0}/{maxBuffer:F0}</color>");

            if (currentIntegrity >= maxIntegrity || currentBuffer < healCost || !inputHandler.HealHeld)
            {
                StopHealing();
            }
        }

        private void StopHealing()
        {
            if (!isHealing) return;

            isHealing = false;
            healTimer = 0f;
            if (playerController != null) playerController.SetMovementHalted(false);
            if (animator != null) animator.SetBool(IsHealingHash, false);
        }

        public void TakeDamage(float damage, Vector2 knockbackDirection, float knockbackForce)
        {
            if (IsDead || isInvulnerable) return;
            if (playerController != null && playerController.IsDashing && playerController.IsInvulnerableDuringDash) return;

            // Cancel heal channeling on hit
            StopHealing();

            // Each enemy hit deducts 1 Integrity node (Hollow Knight Mask style)
            currentIntegrity = Mathf.Max(0, currentIntegrity - 1);
            OnHealthChanged?.Invoke(currentIntegrity, maxIntegrity);

            Debug.Log($"<color=red><b>[BIT DAMAGED]</b> Integrity: {currentIntegrity}/{maxIntegrity}</color>");

            if (currentIntegrity <= 0)
            {
                HandleDeath();
                return;
            }

            TriggerHurtAnimation();

            if (combatController != null)
            {
                combatController.TriggerHitStop(hitStopDuration);
            }

            if (playerController != null)
            {
                float dirX = Mathf.Abs(knockbackDirection.x) > 0.01f
                    ? Mathf.Sign(knockbackDirection.x)
                    : -playerController.FacingDirection;
                float hForce = (knockbackForce > 0f) ? knockbackForce : hitKnockbackHorizontal;
                playerController.ApplyKnockback(new Vector2(dirX * hForce, hitKnockbackVertical), hitStunDuration);
            }

            if (iFrameRoutine != null) StopCoroutine(iFrameRoutine);
            iFrameRoutine = StartCoroutine(InvulnerabilityRoutine());
        }

        private void HandleDeath()
        {
            IsDead = true;

            if (iFrameRoutine != null)
            {
                StopCoroutine(iFrameRoutine);
                iFrameRoutine = null;
            }
            if (hurtAnimRoutine != null)
            {
                StopCoroutine(hurtAnimRoutine);
                hurtAnimRoutine = null;
            }
            isInvulnerable = false;

            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = 1f;
                spriteRenderer.color = c;
            }

            if (playerController != null)
            {
                playerController.OnPlayerDeath();
            }

            if (animator != null)
            {
                animator.ResetTrigger(HurtTrigger);
                foreach (var param in animator.parameters)
                {
                    if (param.nameHash == IsHurtingHash && param.type == AnimatorControllerParameterType.Bool)
                    {
                        animator.SetBool(IsHurtingHash, false);
                    }
                    else if (param.nameHash == IsDeadHash && param.type == AnimatorControllerParameterType.Bool)
                    {
                        animator.SetBool(IsDeadHash, true);
                    }
                    else if (param.nameHash == DeathTrigger && param.type == AnimatorControllerParameterType.Trigger)
                    {
                        animator.SetTrigger(DeathTrigger);
                    }
                }
                if (animator.HasState(0, DeathStateHash))
                {
                    animator.Play(DeathStateHash, 0, 0f);
                }
            }

            OnPlayerDied?.Invoke();
            Debug.Log("<color=red><b>[BIT DEFEATED]</b> Integrity reached 0!</color>");
        }

        private void TriggerHurtAnimation()
        {
            if (animator == null) return;
            if (hurtAnimRoutine != null) StopCoroutine(hurtAnimRoutine);
            hurtAnimRoutine = StartCoroutine(HurtAnimationRoutine());
        }

        private IEnumerator HurtAnimationRoutine()
        {
            try
            {
                foreach (var param in animator.parameters)
                {
                    if (param.nameHash == IsHurtingHash && param.type == AnimatorControllerParameterType.Bool)
                    {
                        animator.SetBool(IsHurtingHash, true);
                        break;
                    }
                }

                animator.ResetTrigger(HurtTrigger);
                if (animator.HasState(0, HurtStateHash))
                {
                    animator.Play(HurtStateHash, 0, 0f);
                }

                yield return new WaitForSeconds(hurtAnimDuration);
            }
            finally
            {
                if (animator != null)
                {
                    foreach (var param in animator.parameters)
                    {
                        if (param.nameHash == IsHurtingHash && param.type == AnimatorControllerParameterType.Bool)
                        {
                            animator.SetBool(IsHurtingHash, false);
                            break;
                        }
                    }
                }
                hurtAnimRoutine = null;
            }
        }

        /// <summary>
        /// Public API for Room / Save-Node systems to restore saved player state.
        /// </summary>
        public void SetState(int integrity, float buffer)
        {
            StopHealing();
            currentIntegrity = Mathf.Clamp(integrity, 0, maxIntegrity);
            currentBuffer = Mathf.Clamp(buffer, 0f, maxBuffer);
            IsDead = currentIntegrity <= 0;

            OnHealthChanged?.Invoke(currentIntegrity, maxIntegrity);
            OnBufferChanged?.Invoke(currentBuffer, maxBuffer);
        }

        /// <summary>
        /// Public API for Save-Node / Bench rest to fully restore Integrity.
        /// </summary>
        public void RestoreFullHealth()
        {
            StopHealing();
            currentIntegrity = maxIntegrity;
            IsDead = false;
            OnHealthChanged?.Invoke(currentIntegrity, maxIntegrity);
        }

        /// <summary>
        /// Public API for environmental hazards (spikes/void). Returns true if damage was applied.
        /// </summary>
        public bool TakeHazardDamage(int damage = 1)
        {
            if (IsDead || isInvulnerable) return false;

            StopHealing();
            int loss = Mathf.Max(1, damage);
            currentIntegrity = Mathf.Max(0, currentIntegrity - loss);
            OnHealthChanged?.Invoke(currentIntegrity, maxIntegrity);

            if (currentIntegrity <= 0)
            {
                HandleDeath();
                return true;
            }

            TriggerHurtAnimation();
            if (combatController != null) combatController.TriggerHitStop(hitStopDuration);

            if (iFrameRoutine != null) StopCoroutine(iFrameRoutine);
            iFrameRoutine = StartCoroutine(InvulnerabilityRoutine());
            return true;
        }

        private IEnumerator InvulnerabilityRoutine()
        {
            isInvulnerable = true;
            float elapsed = 0f;
            const float blinkInterval = 0.08f;

            try
            {
                while (elapsed < invulnerabilityDuration)
                {
                    if (spriteRenderer != null)
                    {
                        Color c = spriteRenderer.color;
                        c.a = (c.a > 0.5f) ? 0.3f : 1f;
                        spriteRenderer.color = c;
                    }
                    yield return new WaitForSeconds(blinkInterval);
                    elapsed += blinkInterval;
                }
            }
            finally
            {
                isInvulnerable = false;
                if (spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a = 1f;
                    spriteRenderer.color = c;
                }
                iFrameRoutine = null;
            }
        }

        private void OnGUI()
        {
            if (!showDebugHUD) return;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };

            GUILayout.BeginArea(new Rect(Screen.width - 342, 12, 330, 105), boxStyle);
            GUILayout.Label($"<b>Integrity:</b> {currentIntegrity} / {maxIntegrity}");

            string healReadyTag = (currentBuffer >= healCost && currentIntegrity < maxIntegrity)
                ? " <color=#66FF88><b>[HEAL READY - Hold E/Q]</b></color>"
                : "";
            GUILayout.Label($"<b>Buffer Energy:</b> {currentBuffer:F0} / {maxBuffer:F0}{healReadyTag}");

            if (isHealing)
            {
                GUILayout.Label($"<color=#66FF88><b>Heal Progress:</b> {Mathf.RoundToInt(HealProgress * 100f)}%</color>");
            }
            else if (IsDead)
            {
                GUILayout.Label("<color=red><b>[DEFEATED]</b></color>");
            }
            else
            {
                GUILayout.Label("<size=11><color=#CCCCCC>[K] Test Damage +35 Buffer</color></size>");
            }
            GUILayout.EndArea();
        }
    }
}
