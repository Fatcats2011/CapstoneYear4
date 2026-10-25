using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreditsMenu : SingletonMonobehaviour<CreditsMenu>, IMouseMenu
{
    // The mouse (MouseMenus): a right click leaves, as B does
    IReadOnlyList<RectTransform> IMouseMenu.MouseRows => null;
    int IMouseMenu.MouseSelected => -1;
    bool IMouseMenu.MouseRowsSpanTheScreen => false;
    Camera IMouseMenu.MouseCamera => null;
    void IMouseMenu.MouseSelect(int row) { }
    void IMouseMenu.MouseClick(int row, Vector2 point, Camera camera) { }
    void IMouseMenu.MouseBack() { ExitMenu(); }

    [SerializeField] GameObject returnButton;
    [SerializeField] MainMenu menu;
    [SerializeField] Animator animator;
    bool finished = false;

    public void Update()
    {
        // Calls when the credits finished
        if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0.9 && animator.GetCurrentAnimatorStateInfo(0).IsName("Credit_Anim") && finished == false)
        {
            finished = true;
            returnButton.SetActive(true);
        }
    }

    public void BeginCredits()
    {
        returnButton.SetActive(false);
        finished = false;
        animator.SetTrigger(HashReference._scrollCreditsTrigger);
    }

    ///<summary>
    /// Exits to the main menu
    ///</summary>
    public void ExitMenu()
    {
        animator.SetTrigger(HashReference._resetTrigger);
        menu.SwapToMainMenu();
    }
}
