using UnityEngine;
using UnityEngine.AI;

namespace SoulsLike.Enemies
{
    [RequireComponent(typeof(NavMeshAgent)), DisallowMultipleComponent]
    public sealed class EnemyNavMeshMotor : MonoBehaviour
    {
        public NavMeshAgent Agent { get; private set; }
        public bool Ready => Agent != null && Agent.enabled && Agent.isOnNavMesh;
        private float nextPath;
        private Vector3[] resetCorners;
        private float resetLength, resetProgress;
        private Vector3 attackMoveDirection;
        private float attackMoveDistance, attackMoveProgress;
        private void Awake() => Agent = GetComponent<NavMeshAgent>();
        public void Configure(EnemyProfile profile)
        {
            if (Agent == null) Agent = GetComponent<NavMeshAgent>();
            Agent.updateRotation = false; Agent.speed = profile.movementSpeed;
            Agent.acceleration = profile.acceleration; Agent.autoBraking = true;
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
            if (progress <= resetProgress || resetCorners.Length < 2) return;
            resetProgress = Mathf.Max(resetProgress, progress);
            float remaining = Mathf.Clamp01(progress) * resetLength;
            Vector3 destination = resetCorners[resetCorners.Length - 1];
            for (int i = 1; i < resetCorners.Length; i++)
            {
                float length = Vector3.Distance(resetCorners[i - 1], resetCorners[i]);
                if (remaining <= length)
                {
                    destination = Vector3.Lerp(resetCorners[i - 1], resetCorners[i], length > .0001f ? remaining / length : 1);
                    break;
                }
                remaining -= length;
            }
            // Absolute animation progress avoids dropping travel when the agent's
            // Transform synchronization occurs later than this Update.
            Vector3 delta = destination - Agent.nextPosition;
            if (delta.sqrMagnitude < .000001f) return;
            Face(-delta, rotationSpeed);
            Agent.Move(delta);
        }
        public void CancelResetPosition() { resetCorners = null; resetProgress = resetLength = 0; Stop(); }
        public void BeginAttackMovement(EnemyAttackDefinition attack, EnemyTarget target)
        {
            CancelAttackMovement();
            if (!Ready || attack.movement == EnemyAttackMotion.AnimationOnly) return;
            Vector3 toward = target != null ? target.GroundPosition - transform.position : transform.forward;
            toward = Vector3.ProjectOnPlane(toward, Vector3.up);
            attackMoveDirection = attack.movementDirection == EnemyAttackDirection.Backward ? -transform.forward :
                attack.movementDirection == EnemyAttackDirection.TowardTarget ? toward.normalized :
                attack.movementDirection == EnemyAttackDirection.AwayFromTarget ? -toward.normalized : transform.forward;
            attackMoveDirection = Vector3.ProjectOnPlane(attackMoveDirection, Vector3.up).normalized;
            attackMoveDistance = attack.movementDistance;
            if (attack.movementDirection == EnemyAttackDirection.TowardTarget)
                attackMoveDistance = Mathf.Min(attackMoveDistance, Mathf.Max(0, toward.magnitude - attack.targetStoppingDistance));
            // A dash/jump cannot cut through a NavMesh boundary or across a gap.
            if (Agent.Raycast(transform.position + attackMoveDirection * attackMoveDistance, out var hit))
                attackMoveDistance = Mathf.Min(attackMoveDistance, Mathf.Max(0, Vector3.Distance(transform.position, hit.position) - .05f));
        }
        public void TickAttackMovement(EnemyAttackDefinition attack, float time)
        {
            if (!Ready || attack.movement == EnemyAttackMotion.AnimationOnly || attack.animation == null) return;
            float frame = time * attack.animation.length * attack.animation.frameRate;
            float progress = Mathf.InverseLerp(attack.movementStartFrame, attack.movementEndFrame, frame);
            if (attack.movement == EnemyAttackMotion.Jump) progress = Mathf.SmoothStep(0, 1, progress);
            float distance = Mathf.Max(0, progress - attackMoveProgress) * attackMoveDistance;
            attackMoveProgress = Mathf.Max(attackMoveProgress, progress);
            if (distance > 0) Agent.Move(attackMoveDirection * distance);
        }
        public void CancelAttackMovement() { attackMoveDistance = attackMoveProgress = 0; attackMoveDirection = Vector3.zero; }
        private void OnDisable() => Stop();
    }
}
