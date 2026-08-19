using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseMenuButtons : MonoBehaviour
{
    public void OnClick_Continue()
    {
        MenuController.Instance.CloseAll();
    }

    public void OnClick_Settings()
    {
        MenuController.Instance.OpenSettings();
    }

    public void OnClick_Quit()
    {
        EndDialog.Instance.ShowEndDialog();
    }
}
