# Bus Fighter — contrôle du 10 octobre 2026

Le bonus donne immédiatement 15 secondes de vol pilotable. F déclenche une seule impulsion verticale de 8 m/s pour dégager les roues, sans phase de décollage automatique ; ZQSD dirige le vol, Espace utilise jusqu’à 3 secondes cumulées de boost. À expiration, la gravité Unity reprend immédiatement : aucun vol plané, plafonnement de vitesse verticale, redressement, freinage aérien ou déplacement forcé à l’atterrissage. L’élan acquis est conservé ; une vitesse ascendante peut donc encore faire monter brièvement le bus avant sa chute.

Après expiration ou arrêt manuel du vol, les figures existantes sont disponibles pendant la chute : Z/S pour les saltos, Q/D pour les rotations, ou le stick de la manette. Les conditions habituelles de hauteur et de temps en l’air restent applicables. Seule l’animation visuelle est mise à jour pendant que le rig de conduite reste désactivé ; les figures ne modifient ni la vitesse ni la rotation physique du bus. Les récompenses d’atterrissage existantes sont conservées.

Les ailes suivent désormais le pivot visuel de la carrosserie, y compris pendant les saltos, avec leur placement et leur échelle conservés. Elles n’ajoutent aucun collider.

Protection des collisions avec les bâtiments : le bus utilise la détection continue au sol aussi. Avant chaque pas de physique, la ville vérifie les quartiers couverts par le volume du bus et son déplacement imminent. Si le chargement est en retard, elle termine la génération nécessaire avant de laisser la physique avancer, sans créer un second générateur concurrent. Ce rattrapage peut occasionner un ralentissement ponctuel. Les vérifications ajoutées couvrent l’attache des ailes pendant les rotations et les frontières de quartiers dans les deux sens ; leur compilation réussit, mais leur exécution Unity et le scénario signalé à 25/25 restent à valider en jeu.

## Corrections

- Remplacement de Gliding par Falling à la même valeur d’énumération. Suppression des paramètres et méthodes de vol plané, de la détection anticipée par rayons et de la remise à plat forcée.
- Les WheelCollider restent suspendus en vol et pendant la chute, puis retrouvent leur état initial au contact du sol. Un rayon limité à l’empreinte des pneus détecte ce contact ; les collisions du châssis restent actives. BusController et BusWheelRig restent désactivés pendant la chute : aucune commande de conduite ne peut réécrire la vitesse ou la rotation aérienne. L’amortissement linéaire est nul dans ces deux états et retrouve sa valeur d’origine à l’atterrissage ou à la désactivation.
- Retour à la conduite sur contact réel des roues ou du châssis avec une surface orientée vers le haut. Un impact sur le toit rend aussi les commandes, permettant le reset R existant. Les murs seuls ne déclenchent pas ce retour.
- SceneBaseGrounding ne stabilise plus le bus et n’ajoute plus de gravité pendant que la conduite ou le rig sont désactivés. Il reste activé pour préserver ses réglages de suspension et de centre de masse.
- Les commandes terrestres mémorisées sont vidées à la désactivation ; BusWheelRig conserve sa remise à zéro existante du drift et des couples. La fumée n’émet pas lorsque le rig ou ses roues sont désactivés.
- Les états initiaux du Rigidbody et des roues sont restaurés au contact et à la désactivation du bonus. Activation ignorée en pause, pendant un bonus, ou sur un Rigidbody cinématique. Une déconnexion du clavier efface les commandes de vol.
- Le HUD affiche « CHUTE ». Il masque le composant texte au lieu de son GameObject, pour ne pas se désactiver définitivement lorsqu’il est attaché au même objet que le texte.
- Caméra, logique de poursuite, PoliceCarAI, PoliceCatchZone et conduite/drift restent conservés. IsAirborne inclut la chute, donc la police reste absente jusqu’au contact et le délai de grâce existant s’applique ensuite.

## Vérification

35 contrôles réussis, 0 échec, dans une copie isolée de SceneBase sous Unity 6000.4.0f1. Compilation de l’assembly de gameplay réussie. Un avertissement préexistant concerne FindFirstObjectByType dans BusCameraFollow.

Le harnais Tools/Validation/FlightChecks.cs se lance dans une copie isolée du projet : le placer dans Assets/Editor de cette copie puis lancer Unity en batch avec `-executeMethod FlightChecks.Run` (sans `-quit`, le harnais termine lui-même). Il charge SceneBase en Play, suspend les mises à jour automatiques et utilise Physics.Simulate pour vérifier les transitions et les contacts physiques. Il ne doit pas être lancé dans la scène de travail principale : il ouvre une scène et ferme Unity en fin de test.

Les tests ne remplacent pas l’appréciation visuelle de la caméra, des virages et des collisions à grande vitesse. Dans SceneBase sauvegardée, le contrôleur de vol est attaché au bus ; BusFlightHUD n’a pas d’instance sérialisée. Son texte doit être relié dans l’interface pour afficher le compte à rebours.

Le chargement isolé signale également un collider Mesh concave sur un Rigidbody dynamique (« BR WHEEL.008 », City.fbx), ainsi qu’une erreur de l’indexeur Unity Search dans le cache copié. Ces diagnostics ne proviennent pas du contrôleur de vol et restent hors de cette correction.

## Effet de retour au sol

Le premier contact de la chute déclenche une couronne de poussière facettée (12 à 32 particules, durée de vie de 0,45 à 0,8 seconde) et une secousse de caméra amortie de 0,28 seconde. Les particules réutilisent le matériau et les émetteurs de SceneBaseDriftSmoke ; sans ce composant configuré, seule la secousse est disponible.

L’intensité utilise la vitesse avant résolution physique projetée vers la normale du sol, relativement à la surface : aucun effet sous 3 m/s, effet maximal à 22 m/s. La vitesse horizontale sur sol plat ne suffit pas à produire un gros impact. Les collisions répétées et rebonds ne redéclenchent pas l’effet une fois le mode Ground rétabli. Les effets ne modifient ni vitesse, ni rotation du bus, ni gravité.

Réglages dans BusFlightController : minimumImpactSpeed et fullImpactSpeed. Dans BusCameraFollow : landingShakeDistance (0,16 m max), landingShakeAngle (1,2 degré max) et landingShakeDuration. La secousse est retirée avant chaque calcul de suivi pour ne pas contaminer son lissage ; elle respecte la pause et se nettoie à la désactivation de la caméra.

La recherche du contrôleur de vol par la caméra utilise désormais le Rigidbody assigné ou les parents de la cible : SceneBase suit un objet enfant du bus. Le branchement du HUD dans la scène de travail peut avoir évolué depuis l’audit précédent ; il n’est pas modifié par cet ajout.

Validation de l’effet : 42 contrôles réussis, 0 échec, dans la copie isolée sous Unity 6000.4.0f1. Ils couvrent aussi le déclenchement unique, l’émission bornée de poussière, la réception par la caméra, le retrait exact de l’offset et l’expiration sans dérive. Le rendu et le confort de la secousse restent à apprécier en Play.

## Activation immédiate et caméra de chute

La phase Takeoff et ses réglages ont été retirés. La rotation est pilotable dès le premier pas de vol ; l’accélération aérienne et le boost restent inchangés. La chute conserve sa gravité, son inertie et ses effets d’impact.

Pendant Falling, la caméra revient progressivement à baseFOV et au cadrage normal (distance, height, lookAheadDistance, lookHeight). Elle garde l’horizon droit et utilise la direction horizontale du mouvement pour éviter de suivre le roulis du bus qui tombe. Aucun de ces ajustements ne modifie la physique du véhicule.

## Traînées verticales pendant la chute

BusFallSpeedEffect est ajouté automatiquement au bus par le contrôleur de vol. Il affiche 16 traînées fines bleu clair, dirigées vers le haut, sur les bords de l’écran. Le centre reste dégagé. Elles commencent à apparaître à 4 m/s de descente et atteignent leur intensité maximale à 30 m/s (108 km/h verticalement). Leur longueur, vitesse de défilement et opacité augmentent avec cette intensité. Les seuils startSpeed/fullSpeed et maximumOpacity sont réglables sur le composant.

L’effet lit uniquement `max(0, -Rigidbody.linearVelocity.y)` pendant Falling. Il ne change pas la physique : la gravité continue d’accélérer le bus vers le bas, sans limite de vitesse verticale introduite par le bonus. Une vitesse ascendante initiale est d’abord freinée par la gravité avant la descente. Les traînées respectent la pause, cessent au retour au sol et ne capturent pas les clics de l’interface. La texture et les objets graphiques sont créés une seule fois puis réutilisés et détruits avec le composant.

Validation des traînées : 51 contrôles Unity réussis, 0 échec. Le test visuel logique utilise un pas de 20 ms pour vérifier la progression selon la vitesse verticale, l’absence d’effet dû à la seule vitesse horizontale, le nombre borné de traînées, la pause et la coupure au contact. Les contrôles physiques de gravité et d’impact restent passants. Le rendu à l’écran reste à apprécier en Play.
