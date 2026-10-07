using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerStateManager
{
    // PlayerInput Send Messages callbacks. Polling in Update also handles sprint
    // release correctly with the project's existing Press interaction.
    private void OnMove(InputValue value) => SetMoveInput(value.Get<Vector2>());
    private void OnSprint(InputValue value) => SetSprintInput(value.isPressed);

    public void SetMoveInput(Vector2 input)
    {
        InputVector = Vector2.ClampMagnitude(input, 1f);
        MoveVector = new Vector3(InputVector.x, 0f, InputVector.y);
    }

    public void SetSprintInput(bool pressed) => SprintHeld = pressed;
}
