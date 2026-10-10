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
        private void OnDisable() => Stop();
    }
}
