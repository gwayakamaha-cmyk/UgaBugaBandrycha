using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Game-over overlay with a restart button that reloads the current scene.</summary>
public class GameOverScreen : MonoBehaviour
{
    public GameObject panel;
    public Button restartButton;
    [Tooltip("Hidden when the game-over panel appears (e.g. the stamina HUD).")]
    public GameObject hud;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
    }

    public void Show()
    {
        if (hud != null) hud.SetActive(false);
        if (panel != null) panel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
