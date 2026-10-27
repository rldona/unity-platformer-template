using Platformer.Controls;
using Platformer.Levels;
using UnityEngine;
using UnityEngine.Events;

namespace Platformer.Player
{
    /// <summary>
    /// Platformer controller on top of a dynamic Rigidbody2D: run and jump with coyote time,
    /// jump buffer and jump cut. Stands on one-way platforms (down + jump drops through them)
    /// and rides moving platforms. Gravity is applied manually (gravityScale = 0). Input is read
    /// in Update and consumed in FixedUpdate. All distances are in Unity units (1 u = 16 px).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        public enum State { Idle, Run, Jump, Fall }

        // Movement within this speed of zero counts as standing still.
        private const float VelocityEpsilon = 0.01f;
        // Upward speed above which a jump still counts as rising. Solver push-out on landing
        // stays well below it, so it can never hide the ground.
        private const float RisingSpeed = 0.5f;
        // A one-way platform only holds the body if its surface is at most this far above the feet.
        private const float OneWayTolerance = 0.02f;

        [Header("Movement")]
        [Tooltip("Maximum horizontal speed (u/s). 6.25 u/s = 100 px/s.")]
        [SerializeField, Min(0f)] private float moveSpeed = 6.25f;
        [Tooltip("Acceleration towards target speed while grounded (u/s²).")]
        [SerializeField, Min(0f)] private float groundAcceleration = 56f;
        [Tooltip("Deceleration when stopping or turning while grounded (u/s²).")]
        [SerializeField, Min(0f)] private float groundDeceleration = 78f;
        [Tooltip("Fraction of ground acceleration/deceleration available in the air.")]
        [SerializeField, Range(0f, 1f)] private float airControl = 0.6f;
        [Tooltip("Stick deflection below this value is ignored. Movement is digital above it.")]
        [SerializeField, Range(0f, 1f)] private float moveDeadZone = 0.35f;

        [Header("Jump")]
        [Tooltip("Apex height of a full jump (u). Initial velocity is derived from gravity.")]
        [SerializeField, Min(0f)] private float jumpHeight = 3.5f;
        [Tooltip("Downward acceleration (u/s²).")]
        [SerializeField, Min(0f)] private float gravity = 56f;
        [Tooltip("Terminal falling speed (u/s).")]
        [SerializeField, Min(0f)] private float maxFallSpeed = 16f;
        [Tooltip("Time after leaving a ledge during which a jump is still allowed (s).")]
        [SerializeField, Min(0f)] private float coyoteTime = 0.10f;
        [Tooltip("Time a jump press is remembered before landing (s).")]
        [SerializeField, Min(0f)] private float jumpBuffer = 0.12f;
        [Tooltip("Vertical velocity multiplier applied once when jump is released while rising.")]
        [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.4f;

        [Header("Platforms")]
        [Tooltip("How long collisions with a one-way platform stay off after dropping through it (s).")]
        [SerializeField, Min(0f)] private float dropThroughTime = 0.25f;

        [Header("Collision")]
        [Tooltip("Layers treated as solid ground and walls (one-way and moving platforms included).")]
        [SerializeField] private LayerMask groundMask;
        [Tooltip("How far below the feet the ground probe reaches (u).")]
        [SerializeField, Min(0.001f)] private float groundProbeDistance = 0.06f;
        [Tooltip("How far beside the body the wall probe reaches (u).")]
        [SerializeField, Min(0.001f)] private float wallProbeDistance = 0.04f;
        [Tooltip("Horizontal inset of the ground probe (u). Keep it tiny: the body is a box, so any wider\n" +
                 "inset leaves a strip at ledge edges where the body is supported but not 'grounded'.\n" +
                 "Walls are rejected by the normal test, not by the inset.")]
        [SerializeField, Min(0f)] private float groundProbeInset = 0.01f;
        [Tooltip("Surfaces whose normal.y is above this are ground; below, they are walls.")]
        [SerializeField, Range(0f, 1f)] private float minGroundNormalY = 0.7f;

        [Header("Visuals")]
        [Tooltip("Sprite flipped with flipX. Art faces right.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Events")]
        [Tooltip("Raised whenever the state changes. Hook animations here.")]
        [SerializeField] private UnityEvent<State> onStateChanged = new UnityEvent<State>();
        [SerializeField] private UnityEvent onJumped = new UnityEvent();
        [SerializeField] private UnityEvent onLanded = new UnityEvent();
        [SerializeField] private UnityEvent onDroppedThrough = new UnityEvent();

        private Rigidbody2D body;
        private BoxCollider2D bodyCollider;
        private PlayerInputActions input;
        private ContactFilter2D groundFilter;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[4];
        private readonly ContactPoint2D[] contacts = new ContactPoint2D[8];

        // Input sampled in Update, consumed in FixedUpdate.
        private float moveInput;
        private float verticalInput;
        private bool jumpHeld;

        // While locked (level transitions, respawns) input is ignored and the body walks on its own.
        private bool controlLocked;
        private float lockedMove;

        private float coyoteTimer;
        private float jumpBufferTimer;
        private bool isJumping;
        private bool jumpCutApplied;
        private float groundGap;
        private int facing = 1;

        // What the body stands on, and the platform velocity added to it last step.
        private Collider2D groundCollider;
        private Vector2 carryVelocity;
        private float appliedCarryX;

        // One-way platform the body is dropping through, and for how much longer.
        private Collider2D droppedPlatform;
        private float dropTimer;

        public State CurrentState { get; private set; } = State.Idle;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public bool IsGrounded { get; private set; }
        /// <summary>What detected the ground on the last physics step: probe, contact, both or none (debug).</summary>
        public string GroundSource { get; private set; } = "none";
        /// <summary>Velocity of the moving platform the body stands on (zero otherwise).</summary>
        public Vector2 CarryVelocity => carryVelocity;
        public float CoyoteTimeRemaining => coyoteTimer;
        public float JumpBufferRemaining => jumpBufferTimer;
        public int Facing => facing;
        public bool IsControlLocked => controlLocked;
        public BoxCollider2D BodyCollider => bodyCollider;
        public float GroundProbeDistance => groundProbeDistance;
        public UnityEvent<State> OnStateChanged => onStateChanged;
        public UnityEvent OnJumped => onJumped;
        public UnityEvent OnLanded => onLanded;
        public UnityEvent OnDroppedThrough => onDroppedThrough;

        /// <summary>
        /// Initial jump speed so that the apex reaches jumpHeight. The continuous answer is
        /// sqrt(2 g h); with a fixed step of dt the discrete apex is v²/2g + v·dt/2, so we
        /// solve that instead to land exactly on jumpHeight.
        /// </summary>
        public float JumpVelocity
        {
            get
            {
                var halfStep = gravity * Time.fixedDeltaTime * 0.5f;
                return Mathf.Sqrt(halfStep * halfStep + 2f * gravity * jumpHeight) - halfStep;
            }
        }

        private void Reset()
        {
            // Sensible defaults when the component is first added in the editor.
            groundMask = LayerMask.GetMask("Ground");
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            var rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 12x32 px box anchored to the feet (transform origin = feet).
            var box = GetComponent<BoxCollider2D>();
            box.size = new Vector2(0.75f, 2f);
            box.offset = new Vector2(0f, 1f);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<BoxCollider2D>();
            body.gravityScale = 0f;

            // Zero friction keeps the body from sticking to walls while pushing into them.
            if (bodyCollider.sharedMaterial == null)
                bodyCollider.sharedMaterial = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };

            groundFilter = new ContactFilter2D { useTriggers = false };
            groundFilter.SetLayerMask(groundMask);

            input = new PlayerInputActions();
        }

        private void OnEnable() => input.Gameplay.Enable();

        private void OnDisable() => input.Gameplay.Disable();

        private void OnDestroy() => input?.Dispose();

        private void Update()
        {
            if (controlLocked)
            {
                moveInput = lockedMove;
                verticalInput = 0f;
                jumpHeld = false;
                return;
            }

            var move = input.Gameplay.Move.ReadValue<Vector2>();
            moveInput = Mathf.Abs(move.x) >= moveDeadZone ? Mathf.Sign(move.x) : 0f;
            verticalInput = Mathf.Abs(move.y) >= moveDeadZone ? Mathf.Sign(move.y) : 0f;

            if (input.Gameplay.Jump.WasPressedThisFrame())
                jumpBufferTimer = jumpBuffer;
            jumpHeld = input.Gameplay.Jump.IsPressed();
        }

        private void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;

            UpdateGrounded(body.linearVelocity.y);
            UpdateDropThrough(dt);

            // Work with the body's own speed; the platform's share is added back at the end.
            var velocity = body.linearVelocity;
            velocity.x -= appliedCarryX;

            var jumped = TryJump(ref velocity, dt);
            velocity.x = ComputeHorizontalVelocity(velocity.x, dt);
            if (!jumped)
                velocity.y = ComputeVerticalVelocity(velocity.y, dt);

            velocity.x += carryVelocity.x;
            appliedCarryX = carryVelocity.x;
            body.linearVelocity = velocity;

            UpdateFacing();
            SetState(EvaluateState(velocity));
        }

        /// <summary>
        /// Takes input away from the player. The character keeps walking in autoMove
        /// (-1 left, 0 stand, 1 right); jumps are ignored until UnlockControl.
        /// </summary>
        public void LockControl(float autoMove)
        {
            controlLocked = true;
            lockedMove = Mathf.Clamp(Mathf.Round(autoMove), -1f, 1f);
            jumpBufferTimer = 0f;
        }

        public void UnlockControl() => controlLocked = false;

        /// <summary>Turns the character to face left (-1) or right (1) without moving it.</summary>
        public void Face(int direction)
        {
            if (direction == 0)
                return;
            facing = direction > 0 ? 1 : -1;
            if (spriteRenderer != null)
                spriteRenderer.flipX = facing < 0;
        }

        /// <summary>Teleports the player and clears all motion and timers.</summary>
        public void Respawn(Vector2 position)
        {
            RestoreDroppedPlatform();
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            isJumping = false;
            groundCollider = null;
            carryVelocity = Vector2.zero;
            appliedCarryX = 0f;
            IsGrounded = false;
            SetState(State.Fall);
        }

        private void UpdateGrounded(float verticalVelocity)
        {
            var wasGrounded = IsGrounded;

            // Past the apex the jump is over, wherever the body is. Without this, a landing
            // that left a tiny upward speed could keep 'rising' true and ignore the floor forever.
            if (isJumping && verticalVelocity <= 0f)
                isJumping = false;

            // While rising from a jump the probe may still touch the floor; ignore it.
            var risingFromJump = isJumping && verticalVelocity > RisingSpeed;

            // Two independent sources: the probe below the feet, and the body's actual physics
            // contacts with an upward normal. Resting on the ground always yields a contact,
            // so a probe miss (ledge corner, sub-pixel gap) can no longer leave the body 'airborne'.
            var byProbe = ProbeGround(out groundGap, out var probed);
            var byContact = TouchingGround(out var touched);
            if (!byProbe)
                groundGap = 0f;
            GroundSource = byProbe ? (byContact ? "probe+contact" : "probe") : (byContact ? "contact" : "none");
            IsGrounded = !risingFromJump && (byProbe || byContact);

            groundCollider = IsGrounded ? (probed != null ? probed : touched) : null;
            carryVelocity = Vector2.zero;
            if (groundCollider != null && groundCollider.attachedRigidbody != null
                && groundCollider.attachedRigidbody.TryGetComponent<MovingPlatform>(out var platform))
                carryVelocity = platform.Velocity;

            if (IsGrounded)
            {
                coyoteTimer = coyoteTime;
                isJumping = false;
                if (!wasGrounded)
                    onLanded.Invoke();
            }
            else
            {
                coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);
            }
        }

        private void UpdateDropThrough(float dt)
        {
            if (droppedPlatform == null)
                return;
            dropTimer -= dt;
            if (dropTimer <= 0f)
                RestoreDroppedPlatform();
        }

        private void RestoreDroppedPlatform()
        {
            if (droppedPlatform != null)
                Physics2D.IgnoreCollision(bodyCollider, droppedPlatform, false);
            droppedPlatform = null;
            dropTimer = 0f;
        }

        /// <summary>
        /// Consumes a buffered jump press: down + jump on a one-way platform drops through it,
        /// otherwise a ground (or coyote) jump. Returns true if a jump set the vertical speed.
        /// </summary>
        private bool TryJump(ref Vector2 velocity, float dt)
        {
            if (jumpBufferTimer <= 0f)
                return false;

            if (verticalInput < 0f && IsGrounded && IsOneWay(groundCollider))
            {
                jumpBufferTimer = 0f;
                droppedPlatform = groundCollider;
                dropTimer = dropThroughTime;
                Physics2D.IgnoreCollision(bodyCollider, droppedPlatform, true);
                IsGrounded = false;
                coyoteTimer = 0f;
                groundCollider = null;
                carryVelocity = Vector2.zero;
                onDroppedThrough.Invoke();
                return false;
            }

            if (coyoteTimer > 0f)
            {
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                isJumping = true;
                jumpCutApplied = false;
                IsGrounded = false;
                velocity.y = JumpVelocity + Mathf.Max(0f, carryVelocity.y);
                onJumped.Invoke();
                return true;
            }

            // Nothing available yet: keep the press buffered for the landing.
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);
            return false;
        }

        private float ComputeHorizontalVelocity(float vx, float dt)
        {
            var target = moveInput * moveSpeed;
            if (target != 0f && IsTouchingWall((int)Mathf.Sign(target)))
                target = 0f;

            var speedingUp = target != 0f && (Mathf.Abs(vx) < VelocityEpsilon || Mathf.Sign(target) == Mathf.Sign(vx));
            var rate = speedingUp ? groundAcceleration : groundDeceleration;
            if (!IsGrounded)
                rate *= airControl;

            return Mathf.MoveTowards(vx, target, rate * dt);
        }

        private float ComputeVerticalVelocity(float vy, float dt)
        {
            if (IsGrounded)
            {
                // Follow the platform and close the probe gap exactly, so the body rests on
                // the surface instead of hovering above it.
                return carryVelocity.y - Mathf.Max(0f, groundGap) / dt;
            }

            if (isJumping && !jumpCutApplied && !jumpHeld && vy > 0f)
            {
                vy *= jumpCutMultiplier;
                jumpCutApplied = true;
            }

            vy -= gravity * dt;
            return Mathf.Max(vy, -maxFallSpeed);
        }

        private void UpdateFacing()
        {
            if (moveInput == 0f)
                return;
            Face(moveInput > 0f ? 1 : -1);
        }

        private State EvaluateState(Vector2 velocity)
        {
            if (IsGrounded)
                return Mathf.Abs(velocity.x - carryVelocity.x) > VelocityEpsilon ? State.Run : State.Idle;
            return velocity.y > 0f ? State.Jump : State.Fall;
        }

        private void SetState(State next)
        {
            if (next == CurrentState)
                return;
            CurrentState = next;
            onStateChanged.Invoke(next);
        }

        private static bool IsOneWay(Collider2D collider) => collider != null && collider.usedByEffector;

        /// <summary>
        /// Casts a box as wide as the body (minus a tiny inset) down from just above the feet.
        /// Wall faces touched by its sides are rejected by the normal test. A one-way platform
        /// only counts when its surface is at or below the feet, never while passing through it.
        /// </summary>
        private bool ProbeGround(out float gap, out Collider2D ground)
        {
            GetGroundProbe(out var origin, out var size, out var skin);
            var count = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundFilter, hits, groundProbeDistance + skin);
            gap = float.MaxValue;
            ground = null;
            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.normal.y < minGroundNormalY || hit.collider == droppedPlatform)
                    continue;
                var hitGap = hit.distance - skin;
                if (IsOneWay(hit.collider) && hitGap < -OneWayTolerance)
                    continue;
                if (hitGap < gap)
                {
                    gap = hitGap;
                    ground = hit.collider;
                }
            }
            return ground != null;
        }

        /// <summary>True when the body has an active physics contact with ground whose normal points up.</summary>
        private bool TouchingGround(out Collider2D ground)
        {
            ground = null;
            if (bodyCollider == null)
                return false;
            var count = bodyCollider.GetContacts(groundFilter, contacts);
            for (var i = 0; i < count; i++)
            {
                // Contacts switched off by a one-way platform's effector do not hold the body.
                if (!contacts[i].enabled || contacts[i].collider == droppedPlatform)
                    continue;
                if (contacts[i].normal.y >= minGroundNormalY)
                {
                    ground = contacts[i].collider;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Casts a thin box sideways from the body's flank; only near-vertical, solid faces count.</summary>
        private bool IsTouchingWall(int direction)
        {
            var bounds = GetBodyBounds();
            var size = new Vector2(0.02f, bounds.size.y * 0.6f);
            var origin = new Vector2(bounds.center.x + direction * (bounds.extents.x - size.x), bounds.center.y);
            var count = Physics2D.BoxCast(origin, size, 0f, Vector2.right * direction, groundFilter, hits, wallProbeDistance + size.x);
            for (var i = 0; i < count; i++)
            {
                // The sides of one-way platforms never block.
                if (IsOneWay(hits[i].collider))
                    continue;
                if (Mathf.Abs(hits[i].normal.x) > minGroundNormalY)
                    return true;
            }
            return false;
        }

        /// <summary>Ground probe geometry, shared with debug gizmos.</summary>
        public void GetGroundProbe(out Vector2 origin, out Vector2 size, out float skin)
        {
            var bounds = GetBodyBounds();
            skin = 0.05f;
            size = new Vector2(Mathf.Max(0.01f, bounds.size.x - 2f * groundProbeInset), 0.02f);
            origin = new Vector2(bounds.center.x, bounds.min.y + skin + size.y * 0.5f);
        }

        /// <summary>Body bounds from collider settings, independent of the physics broadphase.</summary>
        private Bounds GetBodyBounds()
        {
            var col = bodyCollider != null ? bodyCollider : GetComponent<BoxCollider2D>();
            var position = body != null ? body.position : (Vector2)transform.position;
            return new Bounds(position + col.offset, col.size);
        }
    }
}
