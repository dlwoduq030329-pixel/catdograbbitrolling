using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum MenuState
{
    None,
    Pause,
    Settings
}

public class MenuController : MonoBehaviour
{
    public static MenuController Instance;

    [Header("Roots")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject settingsRoot;

    public MenuState CurrentState { get; private set; } = MenuState.None;

    public GraphicsSettingsRuntime runtime;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            HandleEsc();
    }

    void HandleEsc()
    {
        switch (CurrentState)
        {
            case MenuState.None:
                OpenPause();
                break;

            case MenuState.Pause:
                CloseAll();
                break;

            case MenuState.Settings:
                OpenPause();
                break;
        }
    }

    public void OpenPause()
    {
        Time.timeScale = 0f;
        pauseMenuRoot.SetActive(true);
        settingsRoot.SetActive(false);
        CurrentState = MenuState.Pause;
    }

    public void OpenSettings()
    {
        pauseMenuRoot.SetActive(false);
        settingsRoot.SetActive(true);
        CurrentState = MenuState.Settings;
    }

    public void CloseAll()
    {
        Time.timeScale = 1f;
        pauseMenuRoot.SetActive(false);
        settingsRoot.SetActive(false);
        CurrentState = MenuState.None;
    }

    public void test()
    {
        SceneManager.LoadScene("main");
    }
}
