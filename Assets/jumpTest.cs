using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerJump : MonoBehaviour
{
    public float jumpForce = 5f;
    private CharacterController controller;
    [SerializeField] private float groundRayDistance = 0.2f;
    public bool groundedPlayer = true;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q) && groundedPlayer)
        {
            Jump();
        }
    }

    void Jump()
    {
        // Calculate jump velocity and apply it to the controller
        Vector3 jumpVelocity = Vector3.up * jumpForce;
        controller.Move(jumpVelocity * Time.deltaTime);

        groundedPlayer = false;
    }

    void FixedUpdate()
    {

        groundedPlayer = Physics.Raycast(transform.position, Vector3.down, groundRayDistance);
    }
}
