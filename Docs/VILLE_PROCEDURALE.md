# Ville procédurale

SceneBase choisit une nouvelle seed à chaque partie. Le composant `ProceduralCity` sur `Session SceneBase` désactive la map fixe en Play et réutilise le FBX Pastel V3. Le fichier fourni dans Downloads et celui du projet sont identiques (SHA-256 vérifié le 11 octobre 2026).

## Bibliothèque et style

La génération est répartie entre `ProceduralCity.cs` (streaming, navigation), `ProceduralCity.Generation.cs` (catalogue et assemblage), `ProceduralCity.RoadRules.cs` (signalisation) et `ProceduralCity.Traffic.cs` (trafic). Les meshes, matériaux, proportions et orientations proviennent du modèle d'origine. Les îlots ordinaires réemploient les ensembles de façades et ruelles complets, avec une préférence pour les compositions denses de quatre à six bâtiments. L'îlot entier tourne par quarts de tour ; ses façades ne sont plus orientées ou étirées indépendamment.

Le catalogue reprend les 37 bâtiments ordinaires, les compositions d'îlots et les ensembles de mobilier des rues : lampadaires, feux, bancs, poubelles, arrêts de bus, colonnes Morris, supports à vélos, terrasses, jardinières et arbres. Les éléments placés au milieu d'un îlot sont écartés du mobilier générique pour ne pas empiéter sur ses bâtiments ou ses rampes. Les proportions du modèle sont conservées avec une échelle uniforme de 64/60 entre la grille d'origine et les rues procédurales.

Les restaurants CROUSTY, CRISPYCOQ, POULETROYAL et WINGZ ne figurent plus dans le tirage des bâtiments ordinaires. Chaque restaurant généré est un véritable point de récupération. Ils suivent une grille de seize îlots (1 024 m), avec suppression des emplacements réservés à un parcours. Un Crousty est garanti au départ. Le restaurant valide le plus proche reste chargé même au-delà du rayon visuel, afin de pouvoir retrouver un point de ravitaillement. Les enseignes sont orientées vers la rue de récupération.

## Cascades et grands ensembles

Une mégarampe avec cercle de feu, réception et espace d'élan est garantie dans le quartier voisin du départ. Elle dispose d'une parcelle entière de 128 × 128 m : la rampe conserve sa hauteur d'environ 13 m au lieu d'être miniaturisée. Les tours aériennes de plus de 60 m sont aussi présentes près du départ.

Le parcours original des toits est conservé comme un ensemble connecté sur seize îlots : accès au sol, gradins, piste ovale, hélice, plongeon à travers le cercle de feu, réception, half-pipe, ponts et grande descente. Toutes les positions relatives utilisent la même transformation, même lorsqu'un pont traverse une frontière de chunk. La rotation de ce secteur dépend de la seed. Les grandes cascades réapparaissent à l'échelle de régions de 768 m ; elles ne remplissent pas chaque rue.

Des rampes d'accès aux toits autonomes, skateparks, marchés et garages ponctuent les autres quartiers. Le choix des lieux voisins suit une rotation liée à la seed pour éviter plusieurs exemplaires identiques dans la première vue. Le commissariat et ses inscriptions de façade proviennent aussi du FBX : un exemplaire est garanti dans les chunks du départ, à un emplacement qui change selon la seed, puis chaque grande région en reçoit un. Des petits kickers d'origine sont disposés occasionnellement sur un côté de rue, en laissant l'autre passage libre. Les structures et leurs rampes utilisent leurs véritables MeshCollider, pas une boîte englobant le volume sous la pente.

Les grands parcs occupent une surface continue de 108 × 108 m dans une parcelle de quatre îlots, avec le terrain de sport, les arbres et le mobilier de la map. Ils sont espacés de 768 m. Ils ne sont plus constitués de quatre petits jardins séparés par des rues.

## Streaming et gameplay

Les chunks mesurent 128 × 128 m, avec des îlots de 64 m. Neuf quartiers sont construits avant le gameplay. Ensuite, la génération avance par petites étapes : préparation d’un îlot, import d’un mesh avec sa collision, puis signalisation carrefour par carrefour. `generationBudgetMs = 2` fixe le budget souple par frame ; `chunksPerFrame` limite le nombre de quartiers terminés. Une opération Unity, notamment la cuisson d’un MeshCollider, reste indivisible : ce budget ne garantit pas un maximum strict de 2 ms.

Le rayon de construction est circulaire, de `viewDistance × 128 m`, avec une marge de rétention de 128 m. La priorité dépend de la distance entre le bus et les limites réelles du quartier. Après une téléportation, la construction devenue trop éloignée est annulée et le sol des neuf quartiers autour du bus est posé immédiatement. Les bâtiments arrivent ensuite dans le budget normal.

Un brouillard linéaire bleu gris masque la périphérie. Sa limite reste en retrait du quartier incomplet le plus proche, avec une marge de 24 m ; elle s’ouvre progressivement à 45 m/s lorsque le décor est prêt. `streamingFog` et `streamingFogColor` permettent de le régler. Le brouillard d’origine est restauré à la destruction du composant. Après une téléportation, la reprise du décor peut encore être visible à très courte distance.

Le déchargement retire au plus un quartier par passe de maintenance (0,2 s), puis détruit ses meshes progressivement, au plus huit par frame dans le budget partagé. La désactivation du quartier et la destruction finale de ses conteneurs restent indivisibles. Le magasin et le placement du trafic sont vérifiés à 5 Hz ; les feux restent actualisés chaque frame. La liste des îlots denses est calculée une fois au chargement du catalogue. Le marqueur Profiler `Crousty.City.Streaming` permet d’inspecter la construction et la destruction différées.

La seed et les coordonnées déterminent les îlots indépendamment de l'ordre de chargement. Revenir dans un quartier reconstruit le même décor. Désactiver `randomSeed` et saisir la seed affichée dans la Console permet de retrouver une ville.

Les adresses restent en bord de rue, hors des parcours et des grands ensembles. Les tournées comportent six commandes. Toute commande en attente ou tout colis en vol retient son chunk. Le GPS cherche un chemin dans la grille et contourne les rues intérieures supprimées par un parc ou une place de cascades. La mini-carte indexe seulement le quartier terminé ou retiré, et écarte les empreintes hors champ lors du rendu. Les adresses sont enregistrées après la construction complète du quartier.

## Signalisation et circulation

`ProceduralCity.RoadRules.cs` utilise le même graphe de rues que le GPS. Il dessine des passages piétons zébrés aux approches ouvertes, des lignes d'arrêt sur la voie entrante, des dents de cédez-le-passage et des lignes médianes uniquement là où une rue existe. Les marquages d'un chunk forment un seul mesh sans collision avec les roues.

Les carrefours à quatre branches alternent entre feux tricolores et priorité de l'axe nord-sud. Certains axes secondaires portent un STOP, les autres un cédez-le-passage ; le bras secondaire d'un carrefour en T suit la même règle. Les feux du FBX sont replacés du bon côté de la chaussée et leurs trois lampes changent réellement d'état. Les axes opposés ne passent jamais au vert simultanément et un bref temps de rouge intégral sépare les phases. Les voitures roulent à droite, s'arrêtent au rouge et à l'orange, marquent le STOP et laissent passer le trafic prioritaire. Cette logique régit les voitures autonomes ; le bus reste piloté librement par le joueur.

## Trafic

`ProceduralCity.Traffic.cs` crée `trafficCars` voitures (48 par défaut) à partir des modèles de `Resources/Crousty/Vehicles` (NormalCar2, repeinte dans la palette pastel, et 1377 Car). Les modèles regardent vers +Z ; ils sont mis à l'échelle `vehicleLength` (4,4 m), reçoivent le tag `voitures`, un `BoxCollider` et un Rigidbody kinematic : `BusCarCrash` les fait toujours klaxonner ou exploser.

Chaque voiture (`CityTrafficCar`) roule dans la voie de droite, à 4 m de l'axe, sur le même graphe que le GPS. À chaque carrefour, elle choisit une sortie ouverte au hasard (tout droit deux fois plus probable, demi-tour seulement en impasse) et la traverse en courbe de Bézier. Elle s'arrête pare-chocs avant la ligne au rouge, à l'orange si elle peut freiner, marque le STOP une seconde, cède le passage aux axes prioritaires et aux voitures engagées, et laisse passer le trafic d'en face avant de tourner à gauche. Elle n'entre dans un carrefour ni tant qu'une voiture dont la trajectoire croise la sienne y est engagée, ni si sa sortie est bouchée ; deux virages à gauche opposés se croisent par l'intérieur. Elle garde 2,5 m derrière la voiture qui la précède ou derrière le bus, et klaxonne si le bus la bloque. Deux voitures bloquées l'une par l'autre plus de 4 s se dégagent.

Les voitures apparaissent sur une rue d'un quartier terminé, entre 70 et 200 m du bus, hors du champ de la caméra ; une voiture sortie du rayon de streaming est replacée de la même façon et une voiture détruite est remplacée au bout de 0,4 s. Les voitures à points de passage posées dans la scène (`CarFollowPoints`) sont désactivées : leurs routes n'existent pas dans la ville procédurale.

Validation du 13 octobre 2026 dans une copie isolée sous Unity 6000.4.0f1 : `Tools/Validation/TrafficChecks.cs` (lancé par `TrafficChecksSetup.Run`) passe 14 contrôles avec 48 voitures sur 3 600 frames à pas fixe, seed 26011. Aucun écart de voie sur 133 922 mesures, 286 virages à gauche et 295 à droite, 192 engagements aux feux dont aucun au rouge, 58 STOP marqués, aucun chevauchement, attente derrière le bus, recyclage après téléportation et remplacement d'une voiture détruite. `ProceduralChecks` passe sans échec (87 contrôles ; le nombre varie avec la longueur de l'itinéraire GPS tiré). Le rendu des voitures et leur ressenti en jeu restent à apprécier en Play.

## Validation

Les scripts de `Tools/Validation` s'exécutent dans un projet Unity 6000.4.0f1 isolé, utilisant le véritable FBX, les scripts de génération, de livraison, de GPS et de trafic. Ils contrôlent notamment la présence effective de tous les éléments du parcours, leurs positions relatives entre chunks, les orientations des façades, la hauteur des rampes et des tours, les pentes détectées par la physique, le mobilier, la rareté des restaurants, les détours GPS, un déplacement de 2 km, le déchargement et la régénération, une livraison par projectile, les marquages, les phases des feux et l'arrêt des voitures.

La compilation de l'assembly complet du projet réussit. Deux seeds ont passé la validation : 26011 (parcours sans rotation) et 42 (rotation à 90°). Les contrôles vérifient notamment la présence de la station et de ses deux inscriptions dans le voisinage de départ et la diversité des lieux proches. Des vues générales et rapprochées ont été rendues et inspectées. Le projet isolé utilise des adaptateurs pour la conduite et l'interface : ces contrôles ne constituent pas un essai manuel du bus ni une mesure du framerate URP dans SceneBase.

## Vérification de l’optimisation (12 octobre 2026)

L’assembly complet compile avec Roslyn fourni par Unity. La suite du contenu et du gameplay passe 91 contrôles sur la seed 26011. Les tests chargent explicitement les quartiers du parcours des toits hors du rayon circulaire et laissent plusieurs passes au streamer lors des déplacements.

`Tools/Validation/StreamingChecks.cs` et `StreamingChecksSetup.cs` passent 16 contrôles supplémentaires : sol avant décor, import mesh par mesh, équivalence avec la génération synchrone, abandon après téléportation, sol immédiat, magasin distant, nombre de quartiers borné, fermeture et ouverture du brouillard, retour déterministe et indexation incrémentale idempotente de la mini-carte. Les adaptateurs BusMinimap doivent exposer `AddMapGeometry(Transform)` et `RemoveMapGeometry(Transform)`.

L’aperçu `Docs/Previews/streaming-fog.png` utilise le véritable FBX dans le projet isolé. Ces contrôles ne mesurent pas le framerate URP de SceneBase sur le PC du joueur. Le chargement initial des neuf quartiers reste synchrone.
