# Chargement du niveau

JOUER après la saisie du nom ferme le volet orange, puis instancie le prefab
`Assets/Menu/Loading/Chargement.prefab`. Son Canvas persiste pendant le
chargement asynchrone de SceneBase et disparaît après un fondu de 0,8 seconde.

Tous les éléments visuels sont éditables dans ce prefab : illustration,
barre crème fournie, remplissage orange, texte et pourcentage. Le script
ne construit pas le design. L'illustration utilise le PNG fourni avec des
coordonnées UV qui excluent la barre déjà dessinée. Les messages reprennent
les quatre états fournis, puis annoncent la fin du chargement.

Le contrôleur anime les changements de texte avec un rebond élastique et
fait osciller le pourcentage. La barre avance sans reculer. Elle combine
la progression de Unity, la préparation réelle des quartiers et une durée
minimale de cinq secondes. Un ordinateur plus lent peut prendre davantage
de temps : le fondu attend toujours la fin de la préparation.

`ProceduralCity.Prewarm` complète une zone de 5 × 5 quartiers (25 quartiers,
640 × 640 mètres), à partir des neuf quartiers initiaux. Il réutilise les
étapes du streaming, leur seed, les collisions, les commerces, les adresses
et l'indexation de la minimap. Budget souple : 6 ms par frame ; une opération
Unity indivisible peut dépasser ce budget. Le streaming habituel reprend
ensuite. Le chargement de la scène et les neuf quartiers créés dans Awake
peuvent encore provoquer une courte pause ponctuelle de l'animation.

La physique, le véhicule et le chrono sont en pause jusqu'à la fin du fondu.
Le nom saisi reste celui de la partie. En cas d'échec de préparation, un
bouton permet de revenir au menu au lieu de démarrer dans une ville incomplète.

Paramètres Inspector : `minimumSeconds`, `fadeSeconds`, `prewarmRadius`,
`generationBudgetMs`, messages et références UI. Aucun changement au fichier
SceneBase n'est nécessaire. La scène du menu ajoute uniquement la référence
du prefab dans le composant de saisie du nom.

Outil de fabrication : `Tools/Validation/AuthorLoadingScreen.cs`, utilisé
dans une copie isolée. Tests : `Tools/Validation/LoadingScreenChecks.cs`.
Ils vérifient le trajet réel depuis le nom, la progression monotone, les
25 quartiers prêts, les commerces/adresses, l'immobilité du véhicule, le
chrono, le fondu, la reprise du jeu et le nom transmis.

Validation Unity 6000.4.0f1 : `LOADING_CHECKS_DONE failures=0`, environ
6,08 secondes fondu compris, sans nouvelle erreur runtime. Rendu contrôlé
en 1920 × 1080 et 1024 × 768 ; capture dans `Docs/Previews/loading-screen.png`.
