# Ville procédurale

SceneBase choisit une nouvelle seed à chaque partie. Le composant `ProceduralCity` sur `Session SceneBase` désactive la map fixe en Play et réutilise le FBX Pastel V3. Le fichier fourni dans Downloads et celui du projet sont identiques (SHA-256 vérifié le 11 octobre 2026).

## Bibliothèque et style

La génération est répartie entre `ProceduralCity.cs` (streaming, trafic, navigation) et `ProceduralCity.Generation.cs` (catalogue et assemblage). Les meshes, matériaux, proportions et orientations proviennent du modèle d'origine. Les îlots ordinaires réemploient les ensembles de façades et ruelles complets, avec une préférence pour les compositions denses de quatre à six bâtiments. L'îlot entier tourne par quarts de tour ; ses façades ne sont plus orientées ou étirées indépendamment.

Le catalogue reprend les 37 bâtiments ordinaires, les compositions d'îlots et les ensembles de mobilier des rues : lampadaires, feux, bancs, poubelles, arrêts de bus, colonnes Morris, supports à vélos, terrasses, jardinières et arbres. Les éléments placés au milieu d'un îlot sont écartés du mobilier générique pour ne pas empiéter sur ses bâtiments ou ses rampes. Les proportions du modèle sont conservées avec une échelle uniforme de 64/60 entre la grille d'origine et les rues procédurales.

Les restaurants CROUSTY, CRISPYCOQ, POULETROYAL et WINGZ ne figurent plus dans le tirage des bâtiments ordinaires. Chaque restaurant généré est un véritable point de récupération. Ils suivent une grille de seize îlots (1 024 m), avec suppression des emplacements réservés à un parcours. Un Crousty est garanti au départ. Le restaurant valide le plus proche reste chargé même au-delà du rayon visuel, afin de pouvoir retrouver un point de ravitaillement. Les enseignes sont orientées vers la rue de récupération.

## Cascades et grands ensembles

Une mégarampe avec cercle de feu, réception et espace d'élan est garantie dans le quartier voisin du départ. Elle dispose d'une parcelle entière de 128 × 128 m : la rampe conserve sa hauteur d'environ 13 m au lieu d'être miniaturisée. Les tours aériennes de plus de 60 m sont aussi présentes près du départ.

Le parcours original des toits est conservé comme un ensemble connecté sur seize îlots : accès au sol, gradins, piste ovale, hélice, plongeon à travers le cercle de feu, réception, half-pipe, ponts et grande descente. Toutes les positions relatives utilisent la même transformation, même lorsqu'un pont traverse une frontière de chunk. La rotation de ce secteur dépend de la seed. Les grandes cascades réapparaissent à l'échelle de régions de 768 m ; elles ne remplissent pas chaque rue.

Des rampes d'accès aux toits autonomes, skateparks, marchés et garages ponctuent les autres quartiers. Le choix des lieux voisins suit une rotation liée à la seed pour éviter plusieurs exemplaires identiques dans la première vue. Le commissariat et ses inscriptions de façade proviennent aussi du FBX : un exemplaire est garanti dans les chunks du départ, à un emplacement qui change selon la seed, puis chaque grande région en reçoit un. Des petits kickers d'origine sont disposés occasionnellement sur un côté de rue, en laissant l'autre passage libre. Les structures et leurs rampes utilisent leurs véritables MeshCollider, pas une boîte englobant le volume sous la pente.

Les grands parcs occupent une surface continue de 108 × 108 m dans une parcelle de quatre îlots, avec le terrain de sport, les arbres et le mobilier de la map. Ils sont espacés de 768 m. Ils ne sont plus constitués de quatre petits jardins séparés par des rues.

## Streaming et gameplay

Les chunks mesurent 128 × 128 m, avec des îlots de 64 m. Neuf chunks sont construits avant le gameplay, puis le voisinage de rayon `viewDistance = 2` est complété progressivement. Un anneau de rétention évite les recréations continuelles aux frontières et conserve les ponts voisins. Les déplacements exceptionnels rechargent immédiatement le sol autour du bus.

La seed et les coordonnées déterminent les îlots indépendamment de l'ordre de chargement. Revenir dans un quartier reconstruit le même décor. Désactiver `randomSeed` et saisir la seed affichée dans la Console permet de retrouver une ville.

Les adresses restent en bord de rue, hors des parcours et des grands ensembles. Les tournées comportent six commandes. Toute commande en attente ou tout colis en vol retient son chunk. Les voitures suivent les rues ordinaires ou le périmètre des grands ensembles. Le GPS cherche un chemin dans la grille et contourne les rues intérieures supprimées par un parc ou une place de cascades. La mini-carte est rafraîchie lorsque la géométrie change.

## Signalisation et circulation

`ProceduralCity.RoadRules.cs` utilise le même graphe de rues que le GPS. Il dessine des passages piétons zébrés aux approches ouvertes, des lignes d'arrêt sur la voie entrante, des dents de cédez-le-passage et des lignes médianes uniquement là où une rue existe. Les marquages d'un chunk forment un seul mesh sans collision avec les roues.

Les carrefours à quatre branches alternent entre feux tricolores et priorité de l'axe nord-sud. Certains axes secondaires portent un STOP, les autres un cédez-le-passage ; le bras secondaire d'un carrefour en T suit la même règle. Les feux du FBX sont replacés du bon côté de la chaussée et leurs trois lampes changent réellement d'état. Les axes opposés ne passent jamais au vert simultanément et un bref temps de rouge intégral sépare les phases. Les voitures roulent à droite, s'arrêtent au rouge et à l'orange, marquent le STOP et laissent passer le trafic prioritaire. Cette logique régit les voitures autonomes ; le bus reste piloté librement par le joueur.

## Validation

Les scripts de `Tools/Validation` s'exécutent dans un projet Unity 6000.4.0f1 isolé, utilisant le véritable FBX, les scripts de génération, de livraison, de GPS et de trafic. Ils contrôlent notamment la présence effective de tous les éléments du parcours, leurs positions relatives entre chunks, les orientations des façades, la hauteur des rampes et des tours, les pentes détectées par la physique, le mobilier, la rareté des restaurants, les détours GPS, un déplacement de 2 km, le déchargement et la régénération, une livraison par projectile, les marquages, les phases des feux et l'arrêt des voitures.

La compilation de l'assembly complet du projet réussit. Deux seeds ont passé la validation : 26011 (parcours sans rotation) et 42 (rotation à 90°). Les contrôles vérifient notamment la présence de la station et de ses deux inscriptions dans le voisinage de départ et la diversité des lieux proches. Des vues générales et rapprochées ont été rendues et inspectées. Le projet isolé utilise des adaptateurs pour la conduite et l'interface : ces contrôles ne constituent pas un essai manuel du bus ni une mesure du framerate URP dans SceneBase.
