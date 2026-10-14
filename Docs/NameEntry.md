# Nom avant la partie

START ouvre le panel `Nom du joueur` dans `Assets/Scenes/CroustyMainMenu.unity`.
Le champ YOUR NAME reprend le bandeau orange et le fond vidéo existant.
JOUER ou Entrée confirme le nom et lance SceneBase. BACK, Échap ou B à la
manette revient au menu avant confirmation. Le bouton JOUER est désactivé
tant que le nom est vide.

Le nom est limité à 20 caractères. Les espaces extérieurs et répétés sont
nettoyés ; les accents sont conservés. Il est enregistré dans PlayerPrefs
(`Crousty.PlayerName`), prérempli la prochaine fois et utilisé par
`PoliceGameOverManager` pour enregistrer la partie dans le classement.
Les parties lancées depuis une ancienne scène sans saisie gardent « Toi ».

Les objets graphiques sont éditables dans la hiérarchie du panel. Les trois
clips Arrivée, Prêt et Départ et le controller se trouvent dans
`Assets/Menu/NameEntry/Animations`. Le départ comprime puis étire le
formulaire et ferme un volet orange avec « LET'S GO! ». Une fois le volet
fermé, le titre reste visible 0,7 seconde, puis le panel de chargement prépare SceneBase et sa ville procédurale,
puis révèle le jeu par un fondu (voir `Docs/LoadingScreen.md`).
Les clics répétés et le retour sont bloqués pendant le départ.

`Tools/Validation/AuthorNameEntry.cs` est l'outil de fabrication utilisé
uniquement dans la copie isolée. Le jeu ne construit pas le panel par code.
`NameEntryChecks.cs` vérifie START, les champs vides, accents, limite de
longueur, retour, nom mémorisé, Entrée, départ animé, chargement réel de
SceneBase et enregistrement réel du nom en fin de partie. Les préférences
du joueur sont restaurées après le test. Rendus vérifiés en 1696×960,
1280×960 et 2520×1080. Unity 6000.4.0f1 :
`NAME_ENTRY_CHECKS_DONE failures=0`, sans nouvelle erreur Unity.

Les références de l'ancien menu et SceneBase sont préservées. Le contrôle
des panels existant a été adapté au nouvel écran intermédiaire.
