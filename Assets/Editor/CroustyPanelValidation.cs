using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CroustyPanelValidation
{
    [MenuItem("Tools/Crousty/Vérifier les panels")]
    public static void Validate()
    {
        foreach (string path in new[] { "Assets/Scenes/CroustyMainMenu.unity", "Assets/SceneBase.unity",
            "Assets/Jojo/SceneJojo.unity", "Assets/Scenes/Scenes Antoine.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                int pauseCount = 0;
                bool hasEventSystem = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<EventSystem>(true) != null) hasEventSystem = true;
                    foreach (var pause in root.GetComponentsInChildren<CroustyPauseMenu>(true))
                    {
                        CheckPause(pause);
                        pauseCount++;
                    }
                    foreach (var menu in root.GetComponentsInChildren<CroustyMenu>(true))
                    {
                        Check(menu, "mainPanel", "optionsPanel", "sensitivitySlider", "sensitivityLabel", "volumeSlider", "volumeLabel", "fullscreenLabel");
                        Require(menu.categoryPanels.Length == 4, "Quatre panels d'options attendus.");
                        if (menu.pauseMenu == null)
                            Require(menu.GetComponentInChildren<EventSystem>(true) != null, "EventSystem du menu absent.");
                        bool mainWasActive = menu.mainPanel.activeSelf;
                        int count = menu.GetComponentsInChildren<Transform>(true).Length;
                        menu.OpenOptions();
                        for (int i = 0; i < 4; i++)
                        {
                            menu.SelectCategory(i);
                            for (int j = 0; j < 4; j++) Require(menu.categoryPanels[j].activeSelf == (i == j), "Panel d'options incorrect.");
                        }
                        menu.SelectCategory(0);
                        menu.CloseOptions();
                        Require(menu.GetComponentsInChildren<Transform>(true).Length == count, "La navigation a créé des objets.");
                        var buttonRoot = menu.pauseMenu != null ? menu.optionsPanel.transform : menu.transform;
                        foreach (var button in buttonRoot.GetComponentsInChildren<Button>(true))
                            Require(button.onClick.GetPersistentEventCount() > 0 &&
                                (button.onClick.GetPersistentTarget(0) == menu ||
                                 menu.nameEntry != null && button.onClick.GetPersistentTarget(0) == menu.nameEntry),
                                "Bouton non relié : " + button.name);
                        menu.mainPanel.SetActive(mainWasActive);
                    }
                    foreach (var hud in root.GetComponentsInChildren<CroustyHUD>(true)) CheckHud(hud);
                    foreach (var map in root.GetComponentsInChildren<BusMinimap>(true)) Check(map, "mapCanvas", "maskRect", "mapImage", "busIcon", "shopIcon", "policeIcon", "gpsLabel", "routeGraphic", "northRect", "deliveryIconTemplate", "circleSprite", "arrowSprite");
                    foreach (var pet in root.GetComponentsInChildren<BusPetController>(true)) Check(pet, "hud", "label");
                    foreach (var fall in root.GetComponentsInChildren<BusFallSpeedEffect>(true)) Check(fall, "canvas", "viewport", "group");
                    foreach (var session in root.GetComponentsInChildren<SceneBaseSession>(true)) Check(session, "hudCanvas", "passengerText");
                    foreach (var bonuses in root.GetComponentsInChildren<CroustyBonusHUD>(true))
                    {
                        var serialized = new SerializedObject(bonuses);
                        foreach (string card in new[] { "gps", "pet", "rice", "flight" })
                        {
                            var entry = serialized.FindProperty(card);
                            foreach (string field in new[] { "artwork", "nameText", "countText" })
                                Require(entry.FindPropertyRelative(field).objectReferenceValue != null,
                                    "Carte incomplète : " + card + "." + field);
                        }
                    }
                }
                if (path == "Assets/SceneBase.unity" || path == "Assets/Jojo/SceneJojo.unity")
                {
                    Require(pauseCount == 1, path + " : un menu pause attendu.");
                    Require(hasEventSystem, path + " : EventSystem absent.");
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        Debug.Log("[Crousty] Validation OK : références des HUD, événements des boutons et navigation des quatre panels sans création d'objets.");
    }

    [MenuItem("Tools/Crousty/Tester les panels en Play")]
    public static void SmokeTest()
    {
        Require(EditorApplication.isPlaying, "Lancer le menu en mode Play avant le test.");
        var menu = UnityEngine.Object.FindAnyObjectByType<CroustyMenu>();
        Require(menu != null, "Le menu principal doit être chargé.");
        FindHomeButton(menu, "RÉGLAGES").onClick.Invoke();
        double ready = EditorApplication.timeSinceStartup + .45;
        int stage = 0;
        EditorApplication.CallbackFunction verify = null;
        verify = () =>
        {
            if (EditorApplication.timeSinceStartup < ready) return;
            if (!EditorApplication.isPlaying) { EditorApplication.update -= verify; return; }
            if (stage == 0)
            {
                Require(menu.IsOptionsOpen && menu.optionsPanel.activeSelf && !menu.mainPanel.activeSelf, "Ouverture des options incorrecte.");
                menu.tabs[0].GetComponent<Button>().onClick.Invoke();
                stage = 1; ready = EditorApplication.timeSinceStartup + .45; return;
            }
            if (stage <= 4)
            {
                for (int i = 0; i < 4; i++) Require(menu.categoryPanels[i].activeSelf == (i == stage - 1), "Onglet non fonctionnel.");
                if (stage < 4) menu.tabs[stage].GetComponent<Button>().onClick.Invoke();
                else menu.optionsPanel.transform.Find("RETOUR").GetComponent<Button>().onClick.Invoke();
                stage++; ready = EditorApplication.timeSinceStartup + .45; return;
            }
            if (stage == 5)
            {
                Require(!menu.IsOptionsOpen && menu.mainPanel.activeSelf && !menu.optionsPanel.activeSelf, "Retour incorrect.");
                FindHomeButton(menu, "JOUER").onClick.Invoke();
                stage = 6; ready = EditorApplication.timeSinceStartup + 1; return;
            }
            if (stage == 6 && menu != null && menu.IsNameEntryOpen)
            {
                menu.nameEntry.nameInput.text = "Testeur";
                menu.nameEntry.ConfirmPlay();
                stage = 7; ready = EditorApplication.timeSinceStartup + 2; return;
            }
            if (stage == 7 && SceneManager.GetActiveScene().name != "SceneBase") return;
            EditorApplication.update -= verify;
            Require(SceneManager.GetActiveScene().name == "SceneBase", "Le bouton Jouer n'a pas chargé SceneBase.");
            var map = UnityEngine.Object.FindAnyObjectByType<BusMinimap>();
            var hud = UnityEngine.Object.FindAnyObjectByType<CroustyHUD>();
            Require(map != null && map.enabled && hud != null && hud.enabled, "HUD absent ou désactivé.");
            Check(map, "mapCanvas", "mapImage");
            Require(map.GetComponentInChildren<RawImage>(true).texture != null, "Texture de mini-carte absente.");
            CheckHud(hud);
            var bonuses = UnityEngine.Object.FindAnyObjectByType<CroustyBonusHUD>();
            Require(bonuses != null, "Cartes d'atouts absentes.");
            bonuses.Refresh();
            foreach (var pet in UnityEngine.Object.FindObjectsByType<BusPetController>()) Check(pet, "hud", "label");
            foreach (var fall in UnityEngine.Object.FindObjectsByType<BusFallSpeedEffect>()) Check(fall, "canvas", "group");
            EditorApplication.isPaused = true;
            Debug.Log("[Crousty] Test Play OK : Options, quatre onglets, Retour, Jouer, mini-carte et HUD chargés.");
        };
        EditorApplication.update += verify;
    }

    private static void Check(UnityEngine.Object target, params string[] fields)
    {
        var serialized = new SerializedObject(target);
        foreach (string field in fields)
            Require(serialized.FindProperty(field)?.objectReferenceValue != null, target.name + " : référence absente " + field);
    }
    private static Button FindHomeButton(CroustyMenu menu, string name)
    {
        foreach (var button in menu.mainPanel.GetComponentsInChildren<Button>(true))
            if (button.name == name) return button;
        throw new InvalidOperationException("Bouton du menu introuvable : " + name);
    }
    private static void CheckHud(CroustyHUD hud)
    {
        Check(hud, "canvas", "waypointTemplate", "deliveryValue", "objectiveValue", "speedValue", "timeValue", "timerValue", "scoreValue", "scoreGain");
        var serialized = new SerializedObject(hud);
        var canvas = (Canvas)serialized.FindProperty("canvas").objectReferenceValue;
        foreach (string card in new[] { "Carte score", "Carte livraisons", "Carte objectif", "Compteur de vitesse", "Time of day", "Delivery countdown" })
            Require(canvas.transform.Find(card) != null, hud.name + " : panel absent dans le Canvas : " + card);
        Require(canvas.transform.Find("Compteur") == null, hud.name + " : ancien compteur encore présent.");
        var speedFill = (Image)serialized.FindProperty("speedFill").objectReferenceValue;
        if (speedFill != null)
            Require(speedFill.sprite != null && speedFill.type == Image.Type.Filled,
                hud.name + " : jauge de vitesse non remplissable.");
    }
    private static void CheckPause(CroustyPauseMenu pause)
    {
        var serialized = new SerializedObject(pause);
        var panel = (GameObject)serialized.FindProperty("pausePanel").objectReferenceValue;
        Require(panel != null && !panel.activeSelf, pause.name + " : panel pause absent ou ouvert par défaut.");
        var canvas = pause.GetComponentInChildren<Canvas>(true);
        Require(canvas != null && panel.transform.IsChildOf(canvas.transform), pause.name + " : Canvas pause absent.");
        var card = panel.transform.Find("Fond du menu");
        Require(card != null, pause.name + " : fond du menu absent.");
        var resume = card.Find("Bouton reprendre")?.GetComponent<Button>();
        Require(resume != null && resume.onClick.GetPersistentEventCount() == 1 &&
            resume.onClick.GetPersistentTarget(0) == pause, pause.name + " : bouton Reprendre non relié.");
        foreach (var entry in new[] { ("Bouton réglages", "OpenSettings"), ("Bouton menu principal", "ReturnToMainMenu") })
        {
            var button = card.Find(entry.Item1)?.GetComponent<Button>();
            Require(button != null && button.onClick.GetPersistentEventCount() == 1 &&
                button.onClick.GetPersistentTarget(0) == pause && button.onClick.GetPersistentMethodName(0) == entry.Item2,
                pause.name + " : bouton non relié : " + entry.Item1);
        }
        var options = (CroustyMenu)serialized.FindProperty("optionsMenu").objectReferenceValue;
        Require(options != null && options.pauseMenu == pause && options.mainPanel == panel &&
            options.optionsPanel != null && !options.optionsPanel.activeSelf,
            pause.name + " : réglages du menu pause incomplets.");
        var optionsCanvas = options.optionsPanel.GetComponent<Canvas>();
        var optionsScaler = options.optionsPanel.GetComponent<CanvasScaler>();
        Require(optionsCanvas != null && optionsCanvas.isRootCanvas && optionsScaler != null &&
            optionsScaler.referenceResolution == new Vector2(848, 480) &&
            optionsScaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand &&
            options.optionsPanel.transform.Find("Contenu réglages pause") != null,
            pause.name + " : Canvas des options non adapté à leur résolution.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [MenuItem("Tools/Crousty/Ouvrir SceneBase")]
    public static void OpenGameplay()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/SceneBase.unity");
    }

    [MenuItem("Tools/Crousty/Ouvrir le menu principal")]
    public static void OpenMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/CroustyMainMenu.unity");
    }
}
