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

Le classement conserve un seul record par nom : les huit meilleurs joueurs
locaux sont affichés, tandis que les records hors du top huit sont aussi
conservés dans PlayerPrefs (`Crousty.Leaderboard.v1`). Le nom saisi avant
START est fixé au début de la partie. Le score réel de `DeliveryManager`
est enregistré à la fin d'une tournée réussie et à la fin de la partie.
Une meilleure partie remonte le joueur dans le classement ; une partie
moins bonne conserve son record. Les variations de casse et les espaces
superflus ne créent pas de doublon. Les anciens enregistrements multiples
sont regroupés en gardant le plus élevé.

La dernière ligne jouée est orange si elle se trouve dans les rangs 4 à 8.
Le statut en bas distingue le score de cette partie et le record personnel.
Les parties anciennes sans nom gardent « Toi ». Il n'y a pas de
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

Barème conservé dans SceneBase : 100 points par livraison, 500 points pour
terminer une tournée, puis 10 points par seconde restante entière. Le total
se cumule sur toute la partie et reste identique entre le HUD, les résultats
et l'enregistrement du classement. Le record lu par `DeliveryManager` est
celui du joueur de la partie, plutôt que le record global d'un autre nom.

`ScoreAndNameChecks` vérifie les noms, les doublons historiques, les égalités,
les records hors top huit, les parties moins bonnes et une vraie tournée
avec arrivée des projectiles. Il contrôle ensuite le total sur les résultats,
le nom figé pendant la partie et les noms/scores des podiums après retour au
menu. Les préférences du joueur sont restaurées après les tests.
