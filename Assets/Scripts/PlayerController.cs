using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private FixedJoystick _joystick;
    [SerializeField] private float _moveSpeed, _rotationSpeed, _rollSpeed;
    [SerializeField] private Animator _animator;
    
    public ParticleSystem footstepParticleSystem;
    
    private bool canMove = true;
    private Vector3 lastEmit;
    public float delta = 1;
    public float gap = 0.5f;
    private int dir = 1;

    private void Start()
    {
        lastEmit = transform.position;
    }

    private void FixedUpdate()
    {
        if (canMove)
        {
            Vector3 moveDirection = new Vector3(_joystick.Horizontal, 0f, _joystick.Vertical).normalized;

            if (moveDirection != Vector3.zero)
            {
                float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _rotationSpeed, 0.1f);

                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                _animator.SetTrigger("Running");
                _animator.ResetTrigger("Idle");

                EmitFootprint();
            }
            else
            {
                _animator.SetTrigger("Idle");
                _animator.ResetTrigger("Running");
            }

            _rigidbody.velocity = moveDirection * _moveSpeed;
        }
    }

    private void EmitFootprint()
    {
        if (Vector3.Distance(lastEmit, transform.position) > delta)
        {
            var pos = transform.position + (transform.right * gap * dir);
            dir *= -1;
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = pos;
            ep.rotation = transform.rotation.eulerAngles.y;
            footstepParticleSystem.Emit(ep, 1);
            lastEmit = transform.position;
        }
    }

    public void SetCanMove(bool canMoveValue)
    {
        canMove = canMoveValue;
    }
}
