using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private FixedJoystick _joystick;
    [SerializeField] private float _movespeed, rotationSpeed;
    [SerializeField] private Animator _animator;

    private void FixedUpdate()
    {
        Vector3 moveDirection = new Vector3(_joystick.Horizontal, 0f, _joystick.Vertical).normalized;

        if (moveDirection != Vector3.zero)
        {
            float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationSpeed, 0.1f);

            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            _animator.SetTrigger("Running");
            _animator.ResetTrigger("Idle");
        }
        else
        {
            _animator.SetTrigger("Idle");
            _animator.ResetTrigger("Running");
        }

        _rigidbody.velocity = moveDirection * _movespeed;
    }
}
