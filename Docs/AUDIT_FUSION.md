# Audit des fonctionnalités et préparation de la fusion

Date : 9 octobre 2026. Projet : jv-crousty26, Unity 6000.4.0f1.

## Périmètre et méthode

Inventaire des 36 scènes et 65 fichiers C# présents dans Assets, des prefabs utilisés par les scènes de jeu et des réglages de build/navigation/entrées. Lecture des scripts de gameplay et des références sérialisées des scènes. Les exemples importés ont été identifiés et séparés du gameplay du projet.

La conduite du Crousty Bus, la poursuite policière et les plots/lampadaires sont exclus des fonctionnalités à importer. Ils restent des contraintes de compatibilité de la fusion.

Cet audit concerne les fichiers sauvegardés sur disque. Il ne constitue pas une validation en mode Play de toutes les scènes et ne couvre pas les modifications non enregistrées dans l'éditeur. Aucune fusion ni modification du gameplay n'a été effectuée pour cet audit.

## Répartition des scènes

| Scène | Contenu propre à conserver ou importer | Place dans la fusion |
| --- | --- | --- |
| Assets/SceneBase.unity | Ville, bus actuel, caméra, systèmes déjà réalisés, un arrêt et un passager relié à cet arrêt, compteur de passagers, interface de défaite/recommencer | Base de la scène de jeu finale |
| Assets/Jojo/SceneJojo.unity | Magasin Crousty, DeliveryManager, CroustyHUD, six DeliveryPoint | Source de la tournée de livraison et de son interface |
| Assets/Scenes/Scenes Antoine.unity | Quatre véhicules IA, cinq TrafficRoad de quatre points chacun, un carrefour à deux sorties, navigation, collisions/explosions de voitures ; arrêt/passager également présents | Source de la circulation et des explosions ; réutiliser l'arrêt déjà présent dans SceneBase |
| Assets/Scenes/SampleScene.unity | Caméra, lumière et Global Volume ; aucun script de gameplay du projet | Scène de base URP ; aucun gameplay à fusionner |
| 31 scènes TextMesh Pro | Exemples de texte, mise en page, interactions et animations | Exemples techniques, à conserver comme références si utiles |
| Cinematic Explosions FREE (Demo) | Présentation des explosions, modes jour/nuit, réglages de performance | Bibliothèque d'effets et démonstration ; aucun cycle jour/nuit intégré au jeu |

Le build comprend actuellement SceneBase et SampleScene. SceneJojo et Scenes Antoine ne sont pas incluses. Aucun parcours de jeu reliant ces trois scènes n'a été trouvé : les systèmes sont répartis entre des scènes de travail.

## Fonctionnalités présentes hors du périmètre déjà réalisé

### 1. Tournée de livraison Crousty

Sources : Assets/Jojo/Scripts/DeliveryManager.cs, CroustyShop.cs et DeliveryPoint.cs.

- Un magasin recharge automatiquement les commandes lorsque le joueur entre dans son rayon de 8 mètres.
- Le gestionnaire choisit des adresses au hasard parmi les DeliveryPoint disponibles.
- Les paramètres demandent 10 commandes, mais SceneJojo contient seulement six adresses : le code réduit donc la tournée à six commandes et affiche un avertissement.
- Dans le rayon de livraison de 10 mètres, la touche E livre à l'adresse éligible la plus proche.
- Un projectile suit une trajectoire en cloche pendant 0,8 seconde. La livraison est garantie par cette animation ; il ne s'agit pas d'un lancer physique avec échec possible.
- Le stock restant, les livraisons effectuées et les projectiles en vol sont comptés.
- Une adresse passe par les états inactive, en attente, en cours et livrée. Les doublons sont empêchés pendant le lancer.
- Après la dernière livraison et l'arrivée du dernier projectile, le joueur peut revenir au magasin pour repartir en tournée.

Dans la scène, player, shop et deliveryPoints sont laissés vides volontairement : le script les recherche au démarrage. Le projectile n'est pas assigné, donc un cube orange est généré. Les dropTarget ne sont pas assignés : la livraison arrive sur le Transform de l'adresse.

À transférer : le GameObject DeliveryManager avec ses deux composants DeliveryManager/CroustyHUD, le magasin, Point1 à Point6 et leurs scripts de support. Assigner explicitement le bus principal au gestionnaire pendant la fusion.

À compléter : choisir six ou dix commandes, positionner les adresses et les cibles de dépôt dans la ville finale, remplacer éventuellement le cube par un visuel de repas. Aucune récompense, économie ou condition de victoire globale n'est implémentée dans cette boucle.

### 2. Interface et guidage de la livraison

Sources : CroustyHUD.cs, JojoVisuals.cs et BeaconPulse.cs.

- HUD créé automatiquement au démarrage : stock de Crousty et consignes contextuelles.
- Repères visibles à travers les bâtiments, distance aux objectifs et flèches au bord de l'écran.
- Magasin marqué en jaune et adresses en attente en vert.
- Faisceaux lumineux générés sans collider, halo pulsant et éclairage au sol.
- Marqueurs masqués lorsqu'une adresse est en cours de livraison ou déjà livrée.

À fusionner avec l'interface existante : le HUD génère son propre Canvas avec sortingOrder = 100. Il faut définir les priorités des panneaux et masquer/neutraliser le HUD et les actions de livraison pendant une défaite ou une pause. DeliveryManager.Update ne vérifie pas actuellement l'état de fin de partie ni Time.timeScale.

### 3. Circulation de véhicules IA

Sources : Assets/Ia System/Trafficcarnavmesh.cs, TrafficRoad.cs et TrafficIntersection.cs.

- NavMeshAgent déplace les véhicules ; leur Rigidbody reste cinématique.
- Routes à sens unique constituées de points ordonnés dans la hiérarchie.
- Choix aléatoire d'une sortie au carrefour et évitement du demi-tour lorsque plusieurs choix existent.
- Freinage progressif devant un obstacle détecté par raycast, arrêt proche et reprise de vitesse.
- Relance de la destination après un blocage hors freinage.
- Quatre agents sont configurés dans Scenes Antoine à 60–70 km/h. Les objets porteurs sont nommés Cube, Cube (1), Cube (2), Cube (3).

Le réseau sérialisé est :

```text
Route -> Carrefour
  -> Route (1) -> Route (4) -> Route (3) -> aucune suite
  -> Route (2) -------------> Route (3) -> aucune suite
```

Route (3) n'a ni nextRoad ni endIntersection. TrafficCarNavMesh arrête son trajet et désactive le composant lorsqu'il atteint cette fin. Pour un trafic continu, il faut boucler les parcours ou définir explicitement un comportement de sortie/remplacement des véhicules.

Autres points à revoir : rayon NavMeshAgent sérialisé à 10,92 sur les quatre véhicules, taille des colliders, apparence des véhicules et distances de freinage. Les feux de circulation de la ville sont des éléments de décor : aucun contrôleur de cycle de feux connecté au trafic n'a été trouvé.

### 4. Navigation et préparation des routes

Source : Assets/Scripts/Map/NavMeshAutoBaker.cs.

- Bake au lancement, exécuté avant le reste grâce à DefaultExecutionOrder(-1000).
- Collecte des objets du layer Roads et création/configuration d'une NavMeshSurface.
- Événement onBakeFinished disponible, sans listener dans Scenes Antoine.
- Layer Roads = 3 ; area NavMesh Road = 3. Ces deux notions sont distinctes.
- Scenes Antoine contient 27 GameObjects au layer Roads, six NavMeshSurface et des NavMeshModifier qui attribuent l'area Road aux géométries concernées.
- Les surfaces sérialisées ont un NavMeshData nul. Un asset NavMesh-Route (5) existe dans Assets/Scenes/Scenes Antoine, mais aucune référence à son GUID n'a été trouvée dans les scènes/prefabs/scripts examinés.
- SceneBase et SceneJojo n'ont aucun GameObject directement sérialisé au layer Roads.

À transférer : les réglages des géométries routières et leurs modifiers, le réseau de points, le gestionnaire de bake et les agents. Copier seulement NavMeshManager ou les voitures serait insuffisant. Préférer un bake clairement configuré sur la ville finale plutôt qu'importer sans examen les six surfaces de test. Vérifier en Play que les agents sont sur l'area Road et que tous les chemins sont valides.

### 5. Explosions de voitures percutées

Sources : Assets/Scripts/BusCarCrash.cs et Assets/Scripts/explosion/ExplosionFramePlayer.cs.

- BusCarCrash identifie les véhicules portant TrafficCarNavMesh.
- À partir de 60 km/h pour le bus, il instancie l'explosion, désactive les colliders de la voiture et détruit cette voiture.
- En dessous du seuil, il laisse la collision physique ordinaire.
- Scenes Antoine assigne Assets/Scripts/explosion/GameObject 1.prefab et une durée de vie de cinq secondes.
- Le paramètre speedKeptAfterImpact de cette scène vaut 0,9 : l'explosion conserve 90 % de l'élan du bus.
- ExplosionFramePlayer lit les enfants d'un objet comme les images successives de l'animation, avec options de boucle et de destruction.

Présent dans Scenes Antoine, absent du bus de SceneBase et du prefab CroustyBus. À ajouter au bus final avec le prefab d'effet. Tester avec le trafic intégré et décider si la perte de 10 % à l'explosion est souhaitée ; la suppression de résistance des plots/lampadaires ne modifie pas ce système.

Les prefabs du pack Cinematic Explosions sont également disponibles. Leur démo, son interface et son alternance jour/nuit n'ont pas à être importées avec les effets.

### 6. Arrêts et embarquement des passagers

Sources : Assets/Zone de Livraison/BusStopZone.cs, Assets/Man/BusStopPassenger.cs et Assets/Scripts/BusPassengers.cs.

Le dossier Zone de Livraison gère actuellement un arrêt de bus et l'embarquement de passagers.

- Détection du bus dans une zone trigger.
- Bus considéré arrêté sous 1 km/h, pendant 0,5 seconde, avec contrôle optionnel du centre dans la zone.
- Couleur rouge/verte suivant l'état de l'arrêt.
- Passagers envoyés vers la porte à 0,5 seconde d'intervalle.
- Ils marchent, accélèrent après cinq secondes, désactivent leurs colliders pendant l'approche et disparaissent après embarquement.
- passengerCount est incrémenté ; le compteur est actuellement seulement écrit dans la console.

SceneBase possède déjà le prefab Zone de livraison et un prefab Man relié à waitingPassengers. Scenes Antoine contient également ce montage : ne pas importer un doublon.

Limites concrètes :

1. Aucun objet nommé BusDoor trouvé dans les scènes/prefabs du projet ; le code utilise donc le pivot du bus comme destination de secours.
2. boarding passe à true et n'est jamais réinitialisé ; la liste de passagers est vidée. L'arrêt fonctionne une seule fois.
3. L'embarquement déjà lancé n'est pas annulé quand le bus repart.
4. OnTriggerExit réinitialise le bus détecté dès qu'un de ses colliders sort ; vérifier ce comportement avec le bus actuel et ses multiples colliders.
5. Aucun débarquement, renouvellement des passagers, limite de capacité, paiement ou affichage HUD du compteur n'est implémenté.
6. Ce système n'est pas relié aux commandes de DeliveryManager.

À intégrer : conserver l'arrêt existant, ajouter un point de porte explicite, clarifier le cycle des passagers et connecter le compteur à l'interface si cette mécanique reste un objectif du jeu.

### 7. Interface de commandes disponible mais non branchée

Asset : Assets/Sprites/Canvas.prefab.

Visuels Accelerator, Brake, Left, Right et un Slider Gear. Aucun script de gameplay propre au projet dans ce prefab. Le Slider a une liste d'événements vide et le prefab n'est instancié dans aucune des trois scènes de gameplay.

À considérer comme une base graphique. Il ne constitue pas un système de conduite tactile fonctionnel. Les commandes gameplay actuelles lisent directement clavier/souris ; InputSystem_Actions.inputactions fournit des actions génériques mais aucun branchement de ces actions aux scripts de gameplay examinés n'a été trouvé. L'EventSystem de SceneBase utilise bien un module UI Input System.

## Scripts et éléments à garder en commun

- Garder le bus actuel et sa caméra ; les scènes de travail contiennent d'autres montages du bus.
- BusCameraFollow est commun aux trois scènes de gameplay : ne pas importer une deuxième caméra.
- Conserver les systèmes déjà réalisés sans importer la ville entière des autres scènes.
- CroustyArcadeTuningRefresh est un outil éditeur de synchronisation de la conduite, pas une fonctionnalité de mission à fusionner.
- Les scripts TextMesh Pro, Readme/TutorialInfo et les contrôles de démonstration d'explosions appartiennent aux exemples/outils. Ils ne prouvent pas l'existence d'un chat, d'un menu de jeu ou d'un cycle jour/nuit dans le gameplay.

## Fonctions non trouvées dans le gameplay du projet

Aucun système propre au jeu trouvé pour : argent/récompenses, score global, sauvegarde, progression persistante, carburant, dégâts/santé, menu principal, pause générale ou musique/sons branchés dans les scènes de gameplay. La tournée connaît son achèvement, mais aucune victoire globale n'est définie. Le rejeu existant appartient à l'interface de défaite de SceneBase.

Ces éléments sont à créer seulement s'ils font partie du jeu souhaité ; ils ne sont pas des fonctionnalités existantes à fusionner.

## Ordre proposé pour la fusion

1. Faire de SceneBase la scène de référence, avec un bus et une caméra uniques. Garder une copie de travail réversible pour l'intégration.
2. Importer le magasin, les six adresses et le gestionnaire de livraison de SceneJojo. Fixer le nombre de commandes et les références au bus final.
3. Unifier HUD, repères, compteur éventuel de passagers et états de jeu. Bloquer les actions gameplay quand la partie est interrompue.
4. Préparer les géométries Roads/area Road de la ville principale, importer les cinq parcours et le carrefour, puis fermer les routes ou définir les sorties de trafic.
5. Importer les quatre agents comme prototypes, ajuster leur volume et vérifier les parcours avant de multiplier les véhicules.
6. Ajouter BusCarCrash et le prefab d'explosion au bus actuel, puis vérifier le seuil et la règle d'élan.
7. Compléter l'arrêt/passager déjà présent : porte, états d'embarquement, répétition, sortie de zone et HUD.
8. Revoir les scènes du build pour ne lancer que les scènes utiles au jeu final, puis valider la boucle entière dans SceneBase.

Critères de validation de la fusion : chargement au magasin -> livraison aux adresses -> retour au magasin ; trafic continu et freinage cohérent ; explosion des voitures au seuil ; arrêt et embarquement corrects avec le bus actuel ; interface lisible et neutralisée lors d'une interruption ; redémarrage réinitialisant les compteurs et objets ; systèmes du périmètre déjà réalisé toujours opérationnels.