using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KernelKnight.Combat;

namespace KernelKnight.Player
{
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerCombatController : MonoBehaviour
    {
        [Header("Cleanse-Needle (Basic Attack)")]
        [SerializeField] private float attackDamage = 20f;
        [SerializeField] private float attackCooldown = 0.15f;
        [SerializeField] private float attackActiveDuration = 0.10f;
        [SerializeField] private Vector2 forwardHitboxSize = new Vector2(1.6f, 1.0f);
        [SerializeField] private Vector2 forwardHitboxOffset = new Vector2(0.9f, 0.95f);
        [SerializeField] private Vector2 upHitboxSize = new Vector2(1.2f, 1.5f);
        [SerializeField] private Vector2 upHitboxOffset = new Vector2(0f, 1.8f);
        [SerializeField] private Vector2 downHitboxSize = new Vector2(1.2f, 1.0f);
        [SerializeField] private Vector2 downHitboxOffset = new Vector2(0f, 0.2f);
        [SerializeField] private LayerMask targetLayers;

        [Header("Pogo Slash")]
        [SerializeField] private float pogoDamage = 25f;
        [SerializeField] private float pogoBounceForce = 13.5f;
        [SerializeField] private Vector2 pogoHitboxSize = new Vector2(1.2f, 1.0f);
        [SerializeField] private Vector2 pogoHitboxOffset = new Vector2(0f, -0.2f);
        [SerializeField] private LayerMask pogoLayers;

        [Header("Parry System (Keyboard: F | Gamepad: LB)")]
        [SerializeField] private float parryStartupDuration = 0.02f;
        [SerializeField] private float parryActiveDuration = 0.18f;
        [SerializeField] private float parryRecoveryDuration = 0.06f;
        [SerializeField] private float parryCooldown = 0.12f;

        [Header("Counterattack Window")]
        [SerializeField] private float counterattackWindowDuration = 1.2f;
        [SerializeField] private float counterattackDamage = 50f;
        [SerializeField] private float counterattackKnockback = 8f;

        [Header("Debug")]
        [SerializeField] private bool showDebugHUD = false;

        // Components
        private PlayerController playerController;
        private PlayerInputHandler inputHandler;
        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private Animator animator;

        // Combat States
        public enum ParryState { Inactive, Startup, Active, Recovery }
        public ParryState CurrentParryState { get; private set; } = ParryState.Inactive;

        public bool IsCounterattackWindowOpen { get; private set; }
        public float CounterattackTimeRemaining { get; private set; }

        public enum AttackDirection { Forward, Up, Down, Pogo }
        public AttackDirection LastAttackDirection { get; private set; }

        private bool isAttacking;
        private float lastAttackTime;
        private float lastParryTime;
        private float attackBufferCounter;
        private bool hasPogoBouncedInCurrentAttack;
        private readonly HashSet<Collider2D> alreadyHitColliders = new HashSet<Collider2D>();
        private Coroutine activeAttackRoutine;
        private Coroutine activeParryRoutine;

        // Animator Hashes
        private static readonly int SlashTrigger = Animator.StringToHash("Slash");
        private static readonly int PogoTrigger = Animator.StringToHash("Pogo");
        private static readonly int ParryTrigger = Animator.StringToHash("Parry");
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
        private static readonly int IsParryingHash = Animator.StringToHash("IsParrying");

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            inputHandler = GetComponent<PlayerInputHandler>();
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();

            if (targetLayers.value == 0) targetLayers = ~LayerMask.GetMask("Ignore Raycast", "Player");
            if (pogoLayers.value == 0) pogoLayers = ~LayerMask.GetMask("Ignore Raycast", "Player");
        }

        private void Update()
        {
            if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();
            if (playerController == null) playerController = GetComponent<PlayerController>();

            UpdateTimers();
            HandleCombatInput();
        }

        private void UpdateTimers()
        {
            if (attackBufferCounter > 0f)
            {
                attackBufferCounter -= Time.deltaTime;
            }

            if (IsCounterattackWindowOpen)
            {
                CounterattackTimeRemaining -= Time.deltaTime;
                if (CounterattackTimeRemaining <= 0f)
                {
                    CloseCounterattackWindow();
                }
            }
        }

        private void HandleCombatInput()
        {
            if (inputHandler == null || playerController == null) return;

            // 1. Parry Input (F on KBM, LB on Gamepad) - Instant & Spammable
            if (inputHandler.ParryPressed && CanParry())
            {
                StartParry();
                return;
            }

            // 2. Attack Input (LMB on KBM, X on Gamepad, J / X / Z on Keyboard) - Buffered & Spammable
            if (inputHandler.AttackPressed)
            {
                attackBufferCounter = 0.14f;
            }

            if (attackBufferCounter > 0f && CanAttack())
            {
                attackBufferCounter = 0f;
                bool isDownInput = inputHandler.IsHoldingDown || (inputHandler.MoveInput.y < -0.4f);

                if (IsCounterattackWindowOpen)
                {
                    ExecuteCounterattack();
                }
                else if (!playerController.IsGrounded && isDownInput)
                {
                    ExecutePogoSlash();
                }
                else
                {
                    ExecuteBasicAttack();
                }
            }
        }

        private bool CanAttack()
        {
            return (playerController == null || !playerController.IsDashing) &&
                   CurrentParryState != ParryState.Active &&
                   Time.time >= lastAttackTime + attackCooldown;
        }

        private bool CanParry()
        {
            return (playerController == null || !playerController.IsDashing) &&
                   Time.time >= lastParryTime + parryCooldown;
        }

        #region Basic Attack (Cleanse-Needle)
        private void ExecuteBasicAttack()
        {
            lastAttackTime = Time.time;

            if (inputHandler.IsLookingUp)
            {
                LastAttackDirection = AttackDirection.Up;
            }
            else if (playerController.IsGrounded && inputHandler.IsHoldingDown)
            {
                LastAttackDirection = AttackDirection.Down;
            }
            else
            {
                LastAttackDirection = AttackDirection.Forward;
            }

            if (animator != null)
            {
                animator.ResetTrigger(SlashTrigger);
                animator.SetBool(IsAttackingHash, true);
                animator.Play("Slash", 0, 0f);
            }

            if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
            activeAttackRoutine = StartCoroutine(PerformAttackHitboxRoutine(attackDamage, false));
        }

        private void ExecutePogoSlash()
        {
            lastAttackTime = Time.time;
            LastAttackDirection = AttackDirection.Pogo;
            hasPogoBouncedInCurrentAttack = false;

            if (animator != null)
            {
                animator.ResetTrigger(PogoTrigger);
                animator.SetBool(IsAttackingHash, true);
                animator.Play("Pogo", 0, 0f);
            }

            if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
            activeAttackRoutine = StartCoroutine(PerformPogoHitboxRoutine());
        }

        private void ExecuteCounterattack()
        {
            Debug.Log("<color=yellow><b>[COUNTERATTACK]</b> Executing powerful Counterattack following parry!</color>");
            CloseCounterattackWindow();

            lastAttackTime = Time.time;
            LastAttackDirection = AttackDirection.Forward;

            if (animator != null)
            {
                animator.ResetTrigger(SlashTrigger);
                animator.SetBool(IsAttackingHash, true);
                animator.Play("Slash", 0, 0f);
            }

            if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
            activeAttackRoutine = StartCoroutine(PerformAttackHitboxRoutine(counterattackDamage, true));
        }

        private IEnumerator PerformAttackHitboxRoutine(float damage, bool isCounter)
        {
            isAttacking = true;
            alreadyHitColliders.Clear();

            try
            {
                float elapsed = 0f;
                while (elapsed < attackActiveDuration)
                {
                    GetAttackHitbox(out Vector2 center, out Vector2 size);
                    Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, targetLayers);

                    foreach (var hit in hits)
                    {
                        if (hit != null && hit.gameObject != gameObject && !hit.transform.IsChildOf(transform) && !alreadyHitColliders.Contains(hit))
                        {
                            alreadyHitColliders.Add(hit);

                            var damageable = hit.GetComponentInParent<IDamageable>();
                            if (damageable != null)
                            {
                                Vector2 kbDir = isCounter ? new Vector2(playerController.FacingDirection, 0.2f) : new Vector2(playerController.FacingDirection, 0f);
                                float kbForce = isCounter ? counterattackKnockback : 3.5f;
                                damageable.TakeDamage(damage, kbDir, kbForce);

                                StartCoroutine(HitStop(isCounter ? 0.08f : 0.04f));
                            }
                        }
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            finally
            {
                isAttacking = false;
                if (animator != null) animator.SetBool(IsAttackingHash, false);
            }
        }

        private IEnumerator PerformPogoHitboxRoutine()
        {
            isAttacking = true;
            alreadyHitColliders.Clear();

            try
            {
                float elapsed = 0f;
                while (elapsed < attackActiveDuration)
                {
                    Vector2 center = (Vector2)transform.position + pogoHitboxOffset;
                    Collider2D[] hits = Physics2D.OverlapBoxAll(center, pogoHitboxSize, 0f, pogoLayers);

                    foreach (var hit in hits)
                    {
                        if (hit != null && hit.gameObject != gameObject && !hit.transform.IsChildOf(transform) && !alreadyHitColliders.Contains(hit))
                        {
                            alreadyHitColliders.Add(hit);

                            bool pogoSuccess = false;
                            var pogoable = hit.GetComponentInParent<IPogoable>();
                            if (pogoable != null)
                            {
                                pogoSuccess = pogoable.OnPogoHit(center);
                            }

                            var damageable = hit.GetComponentInParent<IDamageable>();
                            if (damageable != null)
                            {
                                damageable.TakeDamage(pogoDamage, Vector2.down, 0f);
                                pogoSuccess = true;
                            }

                            // Trigger pogo bounce launch
                            if (pogoSuccess && !hasPogoBouncedInCurrentAttack)
                            {
                                hasPogoBouncedInCurrentAttack = true;
                                rb.linearVelocity = new Vector2(rb.linearVelocity.x, pogoBounceForce);
                                playerController.OnPogoSuccessful();

                                StartCoroutine(HitStop(0.06f));
                                Debug.Log($"<color=cyan><b>[POGO SUCCESS]</b> Bounced off {hit.name} with force {pogoBounceForce}!</color>");
                                break;
                            }
                        }
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            finally
            {
                isAttacking = false;
                if (animator != null) animator.SetBool(IsAttackingHash, false);
            }
        }

        private void GetAttackHitbox(out Vector2 center, out Vector2 size)
        {
            Vector2 origin = transform.position;
            int dir = playerController.FacingDirection;

            switch (LastAttackDirection)
            {
                case AttackDirection.Up:
                    center = origin + upHitboxOffset;
                    size = upHitboxSize;
                    break;
                case AttackDirection.Down:
                    center = origin + downHitboxOffset;
                    size = downHitboxSize;
                    break;
                case AttackDirection.Forward:
                default:
                    center = origin + new Vector2(forwardHitboxOffset.x * dir, forwardHitboxOffset.y);
                    size = forwardHitboxSize;
                    break;
            }
        }
        #endregion

        #region Parry System
        private void StartParry()
        {
            lastParryTime = Time.time;
            if (activeParryRoutine != null) StopCoroutine(activeParryRoutine);
            activeParryRoutine = StartCoroutine(ParryRoutine());
        }

        private IEnumerator ParryRoutine()
        {
            if (animator != null)
            {
                animator.ResetTrigger(ParryTrigger);
                animator.SetBool(IsParryingHash, true);
                animator.Play("Parry", 0, 0f);
            }

            Color origColor = (spriteRenderer != null) ? Color.white : Color.white;

            try
            {
                // 1. Startup State (instant guard stance)
                CurrentParryState = ParryState.Startup;
                if (spriteRenderer != null) spriteRenderer.color = new Color(0.6f, 0.9f, 1f, 1f);
                yield return new WaitForSeconds(parryStartupDuration);

                // 2. Active Window (Radiant cyan parry flash & active parry detection)
                CurrentParryState = ParryState.Active;
                if (spriteRenderer != null) spriteRenderer.color = new Color(0.15f, 1.0f, 1.0f, 1f);
                yield return new WaitForSeconds(parryActiveDuration);

                // 3. Recovery State (brief whiff recovery)
                CurrentParryState = ParryState.Recovery;
                if (spriteRenderer != null) spriteRenderer.color = new Color(0.85f, 0.85f, 0.95f, 1f);
                yield return new WaitForSeconds(parryRecoveryDuration);
            }
            finally
            {
                CurrentParryState = ParryState.Inactive;
                if (spriteRenderer != null) spriteRenderer.color = origColor;
                if (animator != null) animator.SetBool(IsParryingHash, false);
            }
        }

        /// <summary>
        /// Called by incoming enemy attack or check. Returns true if parry succeeded.
        /// </summary>
        public bool CheckParrySuccess(GameObject attacker)
        {
            if (CurrentParryState == ParryState.Active)
            {
                OnParrySuccess(attacker);
                return true;
            }
            return false;
        }

        private void OnParrySuccess(GameObject attacker)
        {
            Debug.Log("<color=cyan><b>[PARRY SUCCESS]</b> Bit parried attack from " + attacker.name + "!</color>");

            // Cancel parry recovery
            if (activeParryRoutine != null) StopCoroutine(activeParryRoutine);
            CurrentParryState = ParryState.Inactive;
            if (animator != null) animator.SetBool(IsParryingHash, false);
            if (spriteRenderer != null) spriteRenderer.color = Color.white;

            // Notify attacker to stagger/interrupt
            var parryable = attacker.GetComponentInParent<IParryable>();
            parryable?.OnParried(gameObject);

            // Open Counterattack Window
            OpenCounterattackWindow();

            // Hit stop & feedback
            StartCoroutine(HitStop(0.1f));
        }

        private void OpenCounterattackWindow()
        {
            IsCounterattackWindowOpen = true;
            CounterattackTimeRemaining = counterattackWindowDuration;
            Debug.Log($"<color=gold><b>[COUNTERATTACK WINDOW OPEN]</b> Window open for {counterattackWindowDuration}s. Press Attack to strike!</color>");
        }

        private void CloseCounterattackWindow()
        {
            IsCounterattackWindowOpen = false;
            CounterattackTimeRemaining = 0f;
        }

        private IEnumerator HitStop(float duration)
        {
            float origScale = Time.timeScale;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = origScale;
        }
        #endregion

        #region Debug Visualization
        private void OnDrawGizmos()
        {
            Vector2 origin = transform.position;
            int dir = (playerController != null) ? playerController.FacingDirection : 1;

            // 1. Attack Hitbox Visualization
            if (isAttacking)
            {
                Gizmos.color = (LastAttackDirection == AttackDirection.Pogo) ? Color.cyan : (IsCounterattackWindowOpen ? Color.yellow : Color.red);
                GetAttackHitbox(out Vector2 c, out Vector2 s);
                Gizmos.DrawWireCube(c, s);
            }

            // 2. Pogo Range
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireCube(origin + pogoHitboxOffset, pogoHitboxSize);

            // 3. Parry State Ring
            if (CurrentParryState != ParryState.Inactive)
            {
                Gizmos.color = CurrentParryState switch
                {
                    ParryState.Startup => Color.yellow,
                    ParryState.Active => Color.cyan,
                    ParryState.Recovery => Color.gray,
                    _ => Color.white
                };
                Gizmos.DrawWireSphere(origin + new Vector2(0, 0.95f), 1.2f);
            }
        }

        private void OnGUI()
        {
            if (!showDebugHUD || inputHandler == null || playerController == null) return;

            // On-screen testing HUD for Combat & Input
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft
            };

            GUILayout.BeginArea(new Rect(10, 10, 360, 220), boxStyle);
            GUILayout.Label("<b>=== BIT CORE COMBAT & INPUT HUD ===</b>");
            GUILayout.Label($"Device: <b>{inputHandler.CurrentDevice}</b>");
            GUILayout.Label($"MoveInput: ({inputHandler.MoveInput.x:F2}, {inputHandler.MoveInput.y:F2})");
            GUILayout.Label($"Grounded: <b>{playerController.IsGrounded}</b> | Dashing: <b>{playerController.IsDashing}</b>");

            // Parry State Display
            string parryColor = CurrentParryState switch
            {
                ParryState.Active => "<color=cyan><b>ACTIVE (PARRYING!)</b></color>",
                ParryState.Startup => "<color=yellow>STARTUP</color>",
                ParryState.Recovery => "<color=red>RECOVERY (Vulnerable)</color>",
                _ => "INACTIVE"
            };
            GUILayout.Label($"Parry State (F / LB): {parryColor}");

            // Counterattack Window
            if (IsCounterattackWindowOpen)
            {
                GUILayout.Label($"<color=yellow><b>>>> COUNTERATTACK READY: {CounterattackTimeRemaining:F2}s <<<</b>\n(Press LMB / Gamepad X to Strike!)</color>");
            }
            else
            {
                GUILayout.Label("Counterattack Window: Closed");
            }

            GUILayout.Label("<size=10>Controls: A/D/LeftStick: Move | Space/A: Jump | LMB/X: Attack\nW/S: Up/Down Attack | Shift/B: Dash | F/LB: Parry\nAirborne S+LMB/Down+X: Pogo Slash | T: Trigger Dummy Attack</size>");
            GUILayout.EndArea();
        }
        #endregion
    }
}
