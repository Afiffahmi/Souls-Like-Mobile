using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class Roll : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Rigidbody _rigidbody;
    public bool canMove = true;
    public bool canRoll = true;
    public PlayerController playerController;
    bool isPressed = false;
    [SerializeField] private float _rollSpeed;
    public bool buttonPressed = false;
    [SerializeField] private Animator _animator;
    private Quaternion lastRotation;

    void Start()
    {
        lastRotation = transform.rotation;
    }

    void Update()
    {
        if (isPressed && canMove && canRoll)
        {
            _animator.ResetTrigger("Idle");
            _animator.ResetTrigger("Runnih");
            StartCoroutine(StartRoll());
        }
    }

    public void button()
    {
        buttonPressed = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        lastRotation = playerController.transform.rotation;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
    }

    private IEnumerator StartRoll()
    {
        canRoll = false;
        playerController.SetCanMove(false);

        // Get the forward direction of the player
        Vector3 rollDirection = lastRotation * Vector3.forward;
        _rigidbody.velocity = rollDirection * _rollSpeed;
        _animator.SetTrigger("Rolling");
        _animator.ResetTrigger("Idle");
        _animator.ResetTrigger("Running");
        yield return new WaitForSeconds(0.8f);
        _animator.ResetTrigger("Rolling");
        StartCoroutine(WaitRoll());
        
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        
        
    }

    private IEnumerator WaitRoll()
    {
        
        yield return new WaitForSeconds(1.3f);
        playerController.SetCanMove(true);
        canRoll = true;
    }
}
