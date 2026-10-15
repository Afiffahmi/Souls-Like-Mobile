using UnityEngine;
using UnityEngine.AI;

namespace SoulsLike.Enemies
{
    [RequireComponent(typeof(NavMeshAgent)), DisallowMultipleComponent]
    public sealed class EnemyNavMeshMotor : MonoBehaviour
    {
        public NavMeshAgent Agent { get; private set; }
        public bool Ready => Agent != null && Agent.enabled && Agent.isOnNavMesh;
        public Vector3 NavigationPosition => Ready ? Agent.nextPosition : transform.position;
        private float nextPath;
        private Vector3[] resetCorners;
        private float resetLength, resetProgress;
        private int resetNextCorner, attackNextCorner;
        private Vector3[] attackCorners;
        private Vector3 attackMoveStart;
        private Vector3 attackMoveDirection;
        private float attackMoveDistance, attackMoveProgress;
        public bool IsApproachingAttack { get; private set; }
        private void Awake() => Agent = GetComponent<NavMeshAgent>();
        public void Configure(EnemyProfile profile)
        {
            if (Agent == null) Agent = GetComponent<NavMeshAgent>();
            Agent.updateRotation = false; Agent.speed = profile.movementSpeed;
            Agent.acceleration = profile.acceleration; Agent.autoBraking = true;
        }
        public Vector3 ResolveHomePosition(Vector3 spawnPosition)
        {
            // Scene roots can sit above/below the baked surface before the first
            // agent synchronization. Store a reachable home, not that visual height.
            if (Agent != null && NavMesh.SamplePosition(spawnPosition, out var hit, 2f,
                new NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = Agent.areaMask }))
                return hit.position;
            return spawnPosition;
        }
        public void Stop()
        {
            if (!Ready) return;
            Agent.ResetPath();
            // ResetPath can clear isStopped; apply the stop after clearing the route.
            Agent.isStopped = true;
            Agent.velocity = Vector3.zero;
            nextPath = 0;
        }
        public void MoveTo(Vector3 destination, float stoppingDistance, EnemyProfile profile, float speedMultiplier)
        {
            if (!Ready) return;
            Agent.speed = profile.movementSpeed * Mathf.Clamp01(speedMultiplier);
            Agent.stoppingDistance = Mathf.Max(.05f, stoppingDistance);
            Agent.isStopped = speedMultiplier <= 0;
            if (Time.time >= nextPath)
            {
                nextPath = Time.time + profile.repathInterval;
                if (NavMesh.SamplePosition(destination, out var hit, 2, new NavMeshQueryFilter {agentTypeID = Agent.agentTypeID, areaMask = Agent.areaMask})) Agent.SetDestination(hit.position);
                else Agent.ResetPath();
            }
            Face(Agent.desiredVelocity, profile.rotationSpeed);
        }
        public void Face(Vector3 direction, float degrees)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degrees * Time.deltaTime);
        }
        public bool PrepareResetPosition(Vector3 home)
        {
            if (!Ready) return false;
            var path = new NavMeshPath();
            if (!Agent.CalculatePath(home, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            resetCorners = path.corners; resetLength = resetProgress = 0;
            resetNextCorner = 1;
            for (int i = 1; i < resetCorners.Length; i++) resetLength += Vector3.Distance(resetCorners[i - 1], resetCorners[i]);
            Stop();
            // Face away from travel so the animation reads as a backward jump.
            if (resetCorners.Length > 1)
            {
                var away = Vector3.ProjectOnPlane(transform.position - resetCorners[1], Vector3.up);
                if (away.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(away);
            }
            return true;
        }
        public void TickResetPosition(float progress, float rotationSpeed)
        {
            if (!Ready || resetCorners == null) return;
            if (resetCorners.Length < 2) return;
            resetProgress = Mathf.Max(resetProgress, progress);
            Vector3 before = Agent.nextPosition;
            MoveAlongPath(resetCorners, resetProgress * resetLength, ref resetNextCorner);
            Face(before - Agent.nextPosition, rotationSpeed);
        }
        public void CancelResetPosition() { resetCorners = null; resetProgress = resetLength = 0; Stop(); }
        public void BeginAttackMovement(EnemyAttackDefinition attack, EnemyTarget target)
        {
            CancelAttackMovement();
            if (!Ready || attack.movement == EnemyAttackMotion.AnimationOnly) return;
            IsApproachingAttack = attack.HasApproachPhase;
            if (IsApproachingAttack) return;
            CaptureAttackMovement(attack, target);
        }
        public bool TickAttackApproach(EnemyAttackDefinition attack, float time, EnemyTarget target, EnemyProfile profile, float speedMultiplier)
        {
            if (!IsApproachingAttack) return false;
            float frame = time * attack.animation.length * attack.animation.frameRate;
            if (frame < attack.movementStartFrame)
            {
                if (target != null && Vector3.ProjectOnPlane(target.GroundPosition - NavigationPosition, Vector3.up).magnitude > attack.targetStoppingDistance)
                    MoveTo(target.GroundPosition, attack.targetStoppingDistance, profile, speedMultiplier);
                else Stop();
                return true;
            }
            // Live pursuit ends here. Neither the heading nor distance re-reads the target afterward.
            IsApproachingAttack = false;
            Stop();
            CaptureAttackMovement(attack, target);
            if (attack.lockFacingAtStart && attackMoveDirection.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.LookRotation(attackMoveDirection);
            return false;
        }
        private void CaptureAttackMovement(EnemyAttackDefinition attack, EnemyTarget target)
        {
            if (!Ready) return;
            attackMoveStart = Agent.nextPosition;
            Vector3 toward = target != null ? target.GroundPosition - attackMoveStart : transform.forward;
            toward = Vector3.ProjectOnPlane(toward, Vector3.up);
            attackMoveDirection = attack.movementDirection == EnemyAttackDirection.Backward ? -transform.forward :
                attack.movementDirection == EnemyAttackDirection.TowardTarget ? toward.normalized :
                attack.movementDirection == EnemyAttackDirection.AwayFromTarget ? -toward.normalized : transform.forward;
            attackMoveDirection = Vector3.ProjectOnPlane(attackMoveDirection, Vector3.up).normalized;
            if (attackMoveDirection.sqrMagnitude < .0001f) attackMoveDirection = transform.forward;
            attackMoveDistance = attack.movementDistance;
            if (attack.movementDirection == EnemyAttackDirection.TowardTarget && !attack.fixedMovementDistance)
                attackMoveDistance = Mathf.Min(attackMoveDistance, Mathf.Max(0, toward.magnitude - attack.targetStoppingDistance));
            Vector3 destination = attackMoveStart + attackMoveDirection * attackMoveDistance;
            if (attack.movement == EnemyAttackMotion.Jump)
            {
                // Use the baked route rather than silently shortening a jump to zero
                // at the first edge. Never teleport through an obstruction or gap.
                var filter = new NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = Agent.areaMask };
                if (target != null && attack.movementDirection == EnemyAttackDirection.TowardTarget)
                    destination.y = target.GroundPosition.y;
                var path = new NavMeshPath();
                if (NavMesh.SamplePosition(destination, out var landing, 2f, filter) &&
                    Agent.CalculatePath(landing.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    attackCorners = path.corners;
                    attackNextCorner = 1;
                    float length = 0;
                    for (int i = 1; i < attackCorners.Length; i++) length += Vector3.Distance(attackCorners[i - 1], attackCorners[i]);
                    attackMoveDistance = Mathf.Min(Mathf.Max(0, attack.movementDistance), length);
                    return;
                }
            }
            // Dashes and unreachable jumps stop safely before a boundary.
            if (Agent.Raycast(destination, out var hit))
                attackMoveDistance = Mathf.Min(attackMoveDistance, Mathf.Max(0, Vector3.Distance(attackMoveStart, hit.position) - .05f));
        }
        public void TickAttackMovement(EnemyAttackDefinition attack, float time)
        {
            if (!Ready || attack.movement == EnemyAttackMotion.AnimationOnly || attack.animation == null) return;
            float frame = time * attack.animation.length * attack.animation.frameRate;
            float progress = Mathf.InverseLerp(attack.movementStartFrame, attack.movementEndFrame, frame);
            if (attack.movement == EnemyAttackMotion.Jump) progress = Mathf.SmoothStep(0, 1, progress);
            attackMoveProgress = Mathf.Max(attackMoveProgress, progress);
            if (attackCorners != null)
                MoveAlongPath(attackCorners, attackMoveProgress * attackMoveDistance, ref attackNextCorner);
            else if (attackMoveDistance > 0)
                Agent.Move(attackMoveStart + attackMoveDirection * (attackMoveProgress * attackMoveDistance) - Agent.nextPosition);
        }
        private void MoveAlongPath(Vector3[] corners, float distance, ref int nextCorner)
        {
            // Visit every crossed corner in order, even if one animation tick covers
            // several segments. Moving directly to the final point cuts across walls.
            float remaining = Mathf.Max(0, distance);
            for (int i = 1; i < corners.Length; i++)
            {
                float length = Vector3.Distance(corners[i - 1], corners[i]);
                bool reachedCorner = remaining >= length;
                if (i >= nextCorner)
                {
                    Vector3 destination = reachedCorner ? corners[i] : Vector3.Lerp(corners[i - 1], corners[i], length > .0001f ? remaining / length : 1);
                    Agent.Move(destination - Agent.nextPosition);
                    if (!reachedCorner || (Agent.nextPosition - destination).sqrMagnitude > .0025f) return;
                    nextCorner = i + 1;
                }
                if (!reachedCorner) return;
                remaining -= length;
            }
        }
        public void CancelAttackMovement() { IsApproachingAttack = false; attackCorners = null; attackNextCorner = 1; attackMoveDistance = attackMoveProgress = 0; attackMoveDirection = Vector3.zero; }
        private void OnDisable() => Stop();
    }
}
