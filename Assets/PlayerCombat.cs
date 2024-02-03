using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;


public class PlayerCombat : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public List<AttackSO> combo;
    float lastClickedTime;
    float lastComboEnd;
    int comboCounter = 0;
    public Animator anim;
    public bool buttonPressed = false;
    bool isPressed = false;
    public PlayerController playerController;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
     void Update()
    {

        if (isPressed)
        {
            Attack();
        }
        ExitAttack();
        
    }

    void Attack()
    {
        if (Time.time - lastComboEnd > 0.2f && comboCounter < combo.Count)
        {
            CancelInvoke("EndCombo");
            if (Time.time - lastClickedTime >= 1.15f)
            {
                anim.runtimeAnimatorController = combo[comboCounter].animatorOV;
                anim.Play("Attack", 0, 0);
                comboCounter++;
                lastClickedTime = Time.time;

                if (comboCounter >= combo.Count)
                {
                    comboCounter = 0;
                }

                // Disable movement during attack
                if (playerController != null)
                {
                    playerController.SetCanMove(false);
                }
            }
        }
    }

    void ExitAttack()
    {
        if(anim.GetCurrentAnimatorStateInfo(0).normalizedTime > 0.9f && anim.GetCurrentAnimatorStateInfo(0).IsTag("Attack"))
        {
            Invoke("EndCombo", 1);
        }

    }
    void EndCombo()
    {
        comboCounter = 0;
        lastComboEnd = Time.time;

        if (playerController != null)
        {
            playerController.SetCanMove(true);
        }
    }

    public void button()
    {
        buttonPressed = true; 
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
    }
}
