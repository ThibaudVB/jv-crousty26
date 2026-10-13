# Menu classement

Le panel est enregistré dans `Assets/Scenes/CroustyMainMenu.unity` :
`Leaderboard / Cadre classement 848x480`. Les images, textes, podiums,
colonnes et positions sont des objets UI éditables dans la hiérarchie.
Le fond bleu couvre tout le Canvas ; le cadre garde les proportions de la maquette.

Les sept PNG fournis sont dans `Assets/Menu/Leaderboard`. Les animations
se modifient dans `Assets/Menu/Leaderboard/Animations` avec les fenêtres
Animation et Animator : arrivée élastique, flottement, décalage des lignes,
et animations des libellés du menu principal. Le retour conserve son survol
orange et son rebond au clic. Les clips fonctionnent avec le temps en pause.

`CroustyLeaderboard` remplit uniquement les emplacements existants. Aucun
objet graphique de classement n'est construit en jeu. `AuthorLeaderboard.cs`
est un outil de fabrication hors Assets, exécuté dans la copie isolée ; il
n'est ni compilé ni exécuté par le jeu. Ne pas le relancer sur un menu retouché
à la main : il remplace le contenu du panel de classement.

À chaque fin de partie, `PoliceGameOverManager` enregistre le score réel de
`DeliveryManager`. Les huit meilleures parties locales sont conservées dans
PlayerPrefs (`Crousty.Leaderboard.v1`), avec le nom « Toi ». La dernière partie
qualifiée est orange si elle se trouve dans les lignes 4 à 8. Il n'y a pas de
service de classement en ligne ni de joueurs fictifs. Les podiums et lignes
affichent des emplacements vides avant la première partie.

Validation Unity 6000.4.0f1 dans une copie isolée : tri, limite de huit,
persistance, égalités, données invalides, état vide, orange, navigation,
réouverture et onze animations du classement. Rendus vérifiés à 1696×960,
1280×960 et 2520×1080. `LeaderboardChecks` termine avec `failures=0`.
Les deux messages « No texture data is available to upload. » sont également
reproduits dans le menu original par `MenuBaselineProbe` ; aucune nouvelle
erreur Unity n'a été constatée.

`Previews/leaderboard.png` montre des données de démonstration injectées
uniquement pendant le test, sans les enregistrer dans le classement du joueur.
SceneBase et la police RetroBlendy du projet n'ont pas été modifiées par cette intégration.
