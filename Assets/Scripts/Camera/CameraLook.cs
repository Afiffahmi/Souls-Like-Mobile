using UnityEngine;


[RequireComponent(typeof(Unity.Cinemachine.CinemachineFreeLook))]
public class CameraLook : MonoBehaviour
{
    [SerializeField] private float lookSpeed = 1;
    private Unity.Cinemachine.CinemachineFreeLook cinemachine;
    private Player playerInput;


    private void Awake()
    {
        playerInput = new Player();
        cinemachine = GetComponent<Unity.Cinemachine.CinemachineFreeLook>();
    }
    private void OnEnable()
    {
        playerInput.Enable();
    }

    private void OnDisable()
    {
        playerInput.Disable();
    }


    // Update is called once per frame
    void Update()
    {
        Vector2 delta = playerInput.PlayerMain.Look.ReadValue<Vector2>();
        cinemachine.m_XAxis.Value += delta.x * 200 *  lookSpeed * Time.deltaTime;
    }
}
