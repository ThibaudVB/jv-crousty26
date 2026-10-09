using UnityEngine;
using UnityEngine.SceneManagement;

namespace V1
{
    // Écran de défaite quand la police arrête le bus, et bouton pour recommencer.
    public class PoliceGameOverManager : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject gameOverPanel;

        [Header("Bus")]
        [SerializeField] private BusController busController;

        public bool IsGameOver { get; private set; }

        private void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            if (busController == null)
                busController = FindAnyObjectByType<BusController>();

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);

                // L'écran de défaite passe devant le HUD
                Canvas canvas = gameOverPanel.GetComponentInParent<Canvas>(true);
                if (canvas != null)
                    canvas.rootCanvas.sortingOrder = 200;
            }
        }

        public void TriggerPoliceGameOver()
        {
            if (IsGameOver)
                return;

            IsGameOver = true;
            if (busController != null)
                busController.enabled = false;
            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
            // Coupe tous les sons (klaxon, crissement...)
            AudioListener.pause = true;
            Debug.Log("Game Over : la police a immobilise le bus.", this);
        }

        public void RestartLevel()
        {
            Scene scene = SceneManager.GetActiveScene();
            Time.timeScale = 1f;
            AudioListener.pause = false;

            if (!string.IsNullOrEmpty(scene.path) && Application.CanStreamedLevelBeLoaded(scene.path))
            {
                SceneManager.LoadScene(scene.path);
                return;
            }

#if UNITY_EDITOR
            // Dans l'éditeur, on peut recharger une scène même si elle n'est pas dans les scènes de build
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Debug.LogError("Impossible de recommencer : ajouter la scene actuelle aux scenes de build.", this);
#endif
        }
    }
}
