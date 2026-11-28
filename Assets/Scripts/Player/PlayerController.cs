using System.Collections;
using UnityEngine;

namespace KernelKnight.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement (Spec: MaxSpeed=7, Accel=80, Decel=100)")]
        [SerializeField] private float maxMoveSpeed = 7f;
        [SerializeField] private float acceleration = 80f;
        [SerializeField] private float deceleration = 100f;
        [Range(0f, 1f)]
        [SerializeField] private float airControl = 1.0f; // 1.0 = equivalent to ground directional control

        [Header("Jump & Gravity")]
        [SerializeField] private float jumpForce = 14f;
        [SerializeField] private float fallMultiplier = 2.5f;
        [SerializeField] private float lowJumpMultiplier = 2.0f;
        [SerializeField] private float maxFallSpeed = 19f;
        [SerializeField] private float apexVelocityThreshold = 2.0f;
        [SerializeField] private float apexGravityMultiplier = 0.65f;
        [SerializeField] private float coyoteTime = 0.15f;
        [SerializeField] private float jumpBufferTime = 0.15f;

        [Header("Dash (Packet-Hop)")]
        [SerializeField] private float dashSpeed = 22f;
        [SerializeField] private float dashDuration = 0.28f;
        [SerializeField] private float dashCooldown = 0.60f;
        [SerializeField] private bool isInvulnerableDuringDash = false;

        [Header("Ground Detection")]
        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);
        [SerializeField] private LayerMask groundLayer = ~0;

        // Components
        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private PlayerInputHandler inputHandler;
        private PlayerHealthController healthController;
        private Collider2D bodyCollider;

        // State Properties
        public bool IsGrounded { get; private set; }
        public bool IsDashing { get; private set; }
        public bool IsInvulnerableDuringDash => isInvulnerableDuringDash;
        public bool IsMovementLocked => knockbackTimer > 0f || isMovementHalted;
        public int FacingDirection { get; private set; } = 1; // 1 = right, -1 = left

        private bool canDash = true;
        private bool hasAirDashed = false;
        private float coyoteCounter;
        private float jumpBufferCounter;
        private float knockbackTimer;
        private bool isMovementHalted;
        private Coroutine activeDashRoutine;

        // Animator Hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsDashingHash = Animator.StringToHash("IsDashing");
        private static readonly int IsLookingUpHash = Animator.StringToHash("IsLookingUp");

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            inputHandler = GetComponent<PlayerInputHandler>();
            healthController = GetComponent<PlayerHealthController>();
            bodyCollider = GetComponent<Collider2D>();

            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.freezeRotation = true;

            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (playerLayer != -1)
            {
                gameObject.layer = playerLayer;
                if (enemyLayer != -1)
                {
                    // Allow Bit to pass through enemies freely without physical blocking (Metroidvania design)
                    Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);
                }
            }

            if (groundCheckPoint == null)
            {
                var existing = transform.Find("GroundCheck");
                if (existing != null) groundCheckPoint = existing;
                else
                {
                    var checkGo = new GameObject("GroundCheck");
                    checkGo.transform.SetParent(transform);
                    checkGo.transform.localPosition = new Vector3(0, 0.05f, 0);
                    groundCheckPoint = checkGo.transform;
                }
            }

            if (groundLayer.value == 0) groundLayer = ~LayerMask.GetMask("Ignore Raycast", "Player");
        }

        private void Update()
        {
            if (healthController != null && healthController.IsDead) return;
            if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();
            if (inputHandler == null) return;

            if (knockbackTimer > 0f)
            {
                knockbackTimer -= Time.deltaTime;
            }

            if (IsDashing) return;

            CheckGround();

            // Coyote Time logic
            if (IsGrounded)
            {
                coyoteCounter = coyoteTime;
                hasAirDashed = false; // Reset air dash upon landing
            }
            else
            {
                coyoteCounter -= Time.deltaTime;
            }

            if (IsMovementLocked)
            {
                UpdateAnimator();
                return;
            }

            // Jump Buffer logic
            if (inputHandler.JumpPressed)
            {
                jumpBufferCounter = jumpBufferTime;
            }
            else
            {
                jumpBufferCounter -= Time.deltaTime;
            }

            // Execute Jump
            if (jumpBufferCounter > 0f && coyoteCounter > 0f)
            {
                ExecuteJump();
            }

            // Dash — Packet-Hop (once per airtime, replenished on ground or Pogo)
            if (inputHandler.DashPressed && canDash && (IsGrounded || !hasAirDashed))
            {
                if (activeDashRoutine != null) StopCoroutine(activeDashRoutine);
                activeDashRoutine = StartCoroutine(PerformDashRoutine());
            }

            // Facing Direction & Sprite Flip
            float hInput = inputHandler.MoveInput.x;
            if (hInput > 0.05f)
            {
                FacingDirection = 1;
                if (spriteRenderer != null) spriteRenderer.flipX = false;
            }
            else if (hInput < -0.05f)
            {
                FacingDirection = -1;
                if (spriteRenderer != null) spriteRenderer.flipX = true;
            }

            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (healthController != null && healthController.IsDead)
            {
                if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                return;
            }
            if (inputHandler == null || rb == null) return;
            if (IsDashing) return;

            ApplyHorizontalMovement();
            ApplyJumpGravityModifiers();

            // Clamp terminal fall velocity
            if (rb.linearVelocity.y < -maxFallSpeed)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
            }
        }

        private void CheckGround()
        {
            if (groundCheckPoint != null)
            {
                LayerMask mask = (groundLayer.value != 0) ? groundLayer : (LayerMask)~0;
                Collider2D[] hits = Physics2D.OverlapBoxAll(groundCheckPoint.position, groundCheckSize, 0f, mask);
                IsGrounded = false;
                foreach (var hit in hits)
                {
                    if (hit != null && hit.gameObject != gameObject && !hit.transform.IsChildOf(transform) && !hit.isTrigger)
                    {
                        IsGrounded = true;
                        break;
                    }
                }
            }
        }

        private void ExecuteJump()
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }

        private void ApplyHorizontalMovement()
        {
            if (knockbackTimer > 0f) return;
            if (isMovementHalted)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                return;
            }

            float targetSpeed = inputHandler.MoveInput.x * maxMoveSpeed;

            // Determine if accelerating or decelerating
            bool isStopping = Mathf.Abs(targetSpeed) < 0.01f;
            bool isReversing = (Mathf.Sign(targetSpeed) != Mathf.Sign(rb.linearVelocity.x)) && !isStopping && Mathf.Abs(rb.linearVelocity.x) > 0.01f;

            float rate = isStopping ? deceleration : (isReversing ? deceleration * 1.25f : acceleration);
            if (!IsGrounded) rate *= airControl;

            float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, rate * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        }

        private void ApplyJumpGravityModifiers()
        {
            // Apex hang time for responsive Pogo & Packet-Hop timing
            if (!IsGrounded && Mathf.Abs(rb.linearVelocity.y) < apexVelocityThreshold)
            {
                rb.gravityScale = apexGravityMultiplier;
            }
            // Fall faster for punchy platformer responsiveness
            else if (rb.linearVelocity.y < 0)
            {
                rb.gravityScale = fallMultiplier;
            }
            // Variable jump height: lower jump if jump button is released early
            else if (rb.linearVelocity.y > 0 && !inputHandler.JumpHeld)
            {
                rb.gravityScale = lowJumpMultiplier;
            }
            else
            {
                rb.gravityScale = 1f;
            }
        }

        public void ApplyKnockback(Vector2 velocity, float stunDuration = 0.18f)
        {
            if (rb == null) return;
            knockbackTimer = Mathf.Max(knockbackTimer, stunDuration);
            rb.linearVelocity = velocity;
        }

        public void SetMovementHalted(bool halted)
        {
            isMovementHalted = halted;
            if (halted && rb != null && !IsDashing)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        private IEnumerator PerformDashRoutine()
        {
            canDash = false;
            IsDashing = true;
            if (!IsGrounded) hasAirDashed = true;

            float originalGravity = rb.gravityScale;
            rb.gravityScale = 0f;

            // Dash direction follows movement input; if none, follows facing direction
            float dashDir = (Mathf.Abs(inputHandler.MoveInput.x) > 0.1f)
                ? Mathf.Sign(inputHandler.MoveInput.x)
                : FacingDirection;

            rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);

            if (animator != null) animator.SetBool(IsDashingHash, true);

            yield return new WaitForSeconds(dashDuration);

            rb.gravityScale = originalGravity;
            IsDashing = false;
            if (animator != null) animator.SetBool(IsDashingHash, false);

            yield return new WaitForSeconds(dashCooldown);
            canDash = true;
            activeDashRoutine = null;
        }

        public void OnPlayerDeath()
        {
            if (activeDashRoutine != null)
            {
                StopCoroutine(activeDashRoutine);
                activeDashRoutine = null;
            }

            IsDashing = false;
            canDash = false;
            if (animator != null) animator.SetBool(IsDashingHash, false);

            if (rb != null)
            {
                rb.gravityScale = 1f;
                rb.linearVelocity = new Vector2(0f, Mathf.Min(rb.linearVelocity.y, 0f));
            }
        }

        public void OnPogoSuccessful()
        {
            // Replenish coyote jump and air dash like in Hollow Knight!
            coyoteCounter = coyoteTime;
            hasAirDashed = false;
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            animator.SetFloat(SpeedHash, Mathf.Abs(inputHandler.MoveInput.x));
            animator.SetFloat(VerticalVelocityHash, rb.linearVelocity.y);
            animator.SetBool(IsGroundedHash, IsGrounded);
            animator.SetBool(IsLookingUpHash, inputHandler.IsLookingUp);
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheckPoint != null)
            {
                Gizmos.color = IsGrounded ? Color.green : Color.red;
                Gizmos.DrawWireCube(groundCheckPoint.position, groundCheckSize);
            }
        }
    }
}
