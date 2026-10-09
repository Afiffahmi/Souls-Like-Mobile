using UnityEngine;

namespace DoubleL
{
    public class Move_8_way : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;

        CharacterController myCC = null;
        Animator myAnimator = null;
        Vector3 moveDir = Vector3.zero;

        void Awake()
        {
            myCC = GetComponentInChildren<CharacterController>();
            myAnimator = GetComponentInChildren<Animator>();
        }

        void Update()
        {
            float xAxis = Input.GetAxis("Horizontal");
            float zAxis = Input.GetAxis("Vertical");
            
            myAnimator.SetFloat("moveX", xAxis);
            
            myAnimator.SetFloat("moveZ", zAxis);
            
            myAnimator.SetBool("move", (xAxis != 0f || zAxis != 0f));

            
            moveDir = (xAxis * transform.right + zAxis * transform.forward) * Time.deltaTime * moveSpeed;
            
            myCC.Move(moveDir);
        }
    }
}
