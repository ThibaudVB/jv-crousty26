# Modifier l’interface dans Unity

Les menus et les HUD sont maintenant enregistrés dans les scènes. Les composants de jeu actualisent les valeurs et la visibilité, sans reconstruire leur mise en page au lancement.

## Menu principal et options

Ouvrir `Assets/Scenes/CroustyMainMenu.unity`, puis développer :

- `Crousty Main Menu > Crousty Menu Canvas > Accueil`
- `Crousty Main Menu > Crousty Menu Canvas > Réglages > Contenu des options`

Les quatre enfants du contenu sont `Commandes`, `Audio`, `Vidéo` et `Crédits`. Pour travailler sur les options hors Play, désactiver `Accueil`, activer `Réglages` et l’onglet souhaité. Les positions, tailles, textes et couleurs sont modifiables dans les composants habituels. Les actions des boutons et curseurs sont enregistrées dans leurs événements Inspector. Le lancement du jeu affiche toujours l’accueil.

### Maquette à deux menus — 12 octobre 2026

Dans `Accueil > Cadre maquette 848x480 > Fenêtre des deux menus`, `Menu principal` contient le logo, Start Game et Leaderboard ; `Menu supplémentaire` contient Settings, Credits et Leave Game. Les bandes sont crème au repos et orange au survol ou à la sélection. L'indication E apparaît sur la commande active : E active en priorité le bouton survolé, puis celui sélectionné au clavier. Le survol seul ne lance jamais le jeu.

L'onglet `Voir plus et Voir moins` utilise désormais `Assets/Menu/MainMenuTicket.png`, copie inchangée du long ticket fourni avec ses deux libellés imprimés. Il s'agit d'une seule image, conservée dans son ratio d'origine, sans rotation, fondu ni texte superposé. Une fenêtre de masquage couvre tout l'écran : au départ, elle laisse dépasser l'extrémité Voir Plus en bas ; pendant la transition, le ticket entier défile vers le haut et sa partie centrale traverse l'écran ; à l'arrivée, seule l'extrémité Voir Moins dépasse du haut. La distance dépend de la hauteur réelle du ticket et de l'écran, avec 88 unités visibles à chaque extrémité. Le survol n'étire pas le ticket.

`CroustyMenuPages` fait glisser les deux menus en même temps, en 0,55 seconde, sans déplacer le van ni la route. Les menus parcourent exactement la même distance que le ticket (hauteur de l'écran + hauteur du ticket − deux extrémités visibles), plutôt que la seule hauteur du menu. L'espace entre les boutons et le papier reste ainsi constant à l'aller, au retour ou lors d'une inversion. La transition utilise le temps réel et bloque les boutons des pages pendant le mouvement. Échap retourne au premier menu ; depuis les réglages ou le classement, il ferme d'abord le panneau ouvert. La migration actualise le menu déjà chargé avec Undo, sans recharger ni sauvegarder automatiquement les modifications en mémoire.

Le menu reste placé dans la scène : aucune interface n'est reconstruite en Play. Les options existantes et les menus pause conservent leur fonctionnement. Settings ouvre Commandes, Credits ouvre directement l'onglet Crédits, et le retour restitue le menu supplémentaire. Start Game charge SceneBase. Le panneau Leaderboard présente actuellement un état vide, car aucun système de classement ou de sauvegarde des scores n'existe dans le projet.

Validation du ticket continu sur une copie du projet sous Unity 6000.4.0f1 : 32 contrôles réussis, notamment unicité de l'image, ratio préservé, déplacement ascendant, maintien de Voir Plus après redimensionnement, arrivée de Voir Moins en haut, absence de rotation/fondu, survol, E, transition aller/retour, inversion rapide, temps de jeu suspendu, réglages, crédits, classement et lancement effectif de BASE. Aucun recouvrement entre le papier et les éléments visibles des menus sur 4 371 images de transition contrôlées, y compris au retour et lors d'une inversion. Les rendus avant, pendant et après la transition ont été inspectés, en 16:9 et 4:3. Seule la scène CroustyMainMenu est modifiée ; aucune autre scène n'est réenregistrée.

## Interface en jeu

Dans `Assets/SceneBase.unity`, rechercher les objets suivants dans la hiérarchie :

| Objet | Contenu |
| --- | --- |
| `Crousty HUD` | Compteur de livraisons, passagers, quatre cartes d’atouts et modèle de repère |
| `Minimap Crousty` | Cadre, carte dynamique, GPS, icônes et modèle de livraison |
| `PET - HUD` | Charges et boost PET, sous le bus |
| `Bus Fighter - vitesse de chute` | CanvasGroup et seize traînées, sous le bus |
| `GameHUD` / `Canvas` | Éléments déjà placés, dont compteur de vol et Game Over |

Les modèles de repères sont inactifs : les activer temporairement pour modifier leur aspect, puis les désactiver. Le jeu duplique ces modèles selon les objectifs. Le contenu de la mini-carte et la position des repères restent calculés pendant le jeu. Pour prévisualiser les traînées, augmenter temporairement l’alpha du CanvasGroup ; l’effet gère cet alpha pendant la chute.

Les HUD concernés ont aussi été placés dans `SceneJojo`, `Scenes Antoine` et le prefab `CroustyBus`. Les sprites de repères sont de vrais assets dans `Assets/UI`.

## Vérifications

`Tools > Crousty > Vérifier les panels` contrôle les références, les événements des boutons et la navigation. `Tester les panels en Play`, lancé depuis le menu principal en Play, parcourt les onglets, retourne à l’accueil et lance SceneBase, puis met l’éditeur en pause pour inspecter les HUD.

Les commandes de conversion servent à migrer une ancienne scène : elles ne sont pas nécessaires pour utiliser les panels déjà enregistrés et ne reconstruisent pas ceux qui existent.

## Cartes d’atouts

Dans `Crousty HUD > Atouts`, chaque carte a un `Artwork`, un `Count` posé sur l’hexagone orange et un `Name` posé sur la bande noire. Les quatre visuels sont dans `Assets/UI/Bonuses`. On peut déplacer et redimensionner ces objets dans l’éditeur.

Le composant `CroustyBonusHUD` met à jour les compteurs GPS, PET et FLIGHT. Une carte sans charge prend une teinte grise. RICE affiche `x3` jusqu’à ce que sa mécanique soit codée ; `SetRiceCharges` permet de la raccorder plus tard. Les noms anglais et français sont éditables dans l’Inspector, et `SetLanguage` change leur affichage. GPS consomme une charge à chaque sélection réussie, PET et VOL à chaque activation.
