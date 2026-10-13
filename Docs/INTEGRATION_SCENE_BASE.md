# Intégration dans SceneBase

## Minimap dynamique

SceneBase utilise désormais une ville chargée par chunks avec une nouvelle seed à chaque partie. Voir [VILLE_PROCEDURALE.md](VILLE_PROCEDURALE.md) pour la génération et l'intégration des livraisons, du GPS et du trafic. Les placements fixes décrits plus bas restent documentés comme état antérieur.

`BusMinimap` est attaché au `DeliveryManager` de SceneBase. En Play, il crée une caméra orthographique inclinée à 55° et un HUD carré en haut à droite : le bus reste au centre et orienté vers le haut, la carte tourne avec son cap, et les marqueurs indiquent le magasin, les livraisons en attente et la police. Les objectifs hors de la zone visible restent indiqués sur le bord. Le radar dessine les silhouettes de la géométrie active avec des rues claires et des îlots sombres ; aucune image fixe de la ville ni coordonnées de carte ne sont nécessaires.

Pour une future carte générée par seed, les routes et bâtiments doivent avoir des MeshFilter classés avec RadarMapElement, puis appeler BusMinimap.RefreshMap avec la racine générée. Les nouvelles adresses doivent aussi être enregistrées dans `DeliveryManager.deliveryPoints` pour apparaître comme objectifs actifs. Le rayon affiché (`worldRadius`), la hauteur de la caméra et la taille du HUD se règlent sur le composant.

## Livraisons sur la map Pastel V3 — 11 octobre 2026

Le groupe `Livraisons Crousty` utilise maintenant l'origine de `CroustyCity_PastelPalette_V3_Unity`. Le magasin correspond au repère importé `Pickup_Crousty`, avec un rayon de chargement de 16 m. Les six adresses sont réparties au pied des bâtiments des îlots r4/c1, r3/c1, r2/c1, r1/c3, r0/c4 et r4/c4, avec un rayon de livraison de 12 m. Le chargement automatique, la touche E et les tournées de six commandes sont conservés.

Le modèle Pastel V3 est désormais lisible pour permettre la construction du NavMesh du GPS. Le magasin et les adresses limitent l'écart vertical à 6 m pour éviter une interaction depuis les parcours surélevés ou pendant le vol.

Validation Unity 6000.4.0f1 dans un projet temporaire isolé : 55 contrôles réussis, couvrant le repère du restaurant, la construction du NavMesh, les chemins complets depuis le bus vers les six adresses, les hauteurs au sol, les portées d'interaction et les transitions attente/envoi/livré. Le script `Tools/Validation/DeliveryMapChecks.cs` s'exécute en batch après copie sous `Assets/Editor` du projet de validation ; l'argument `-croustySourceProject` permet de désigner le projet source. Cette validation ne remplace pas une appréciation visuelle et une tournée jouée dans SceneBase.

## État actuel — migration du trafic, 10 octobre 2026

Le trafic utilise maintenant les scripts les plus récents, `CarFollowPoints` et `IntersectionChooser`. Les descriptions NavMesh et les validations du 9 octobre ci-dessous sont historiques et ne valident pas ce nouveau contrôleur.

- Les quatre voitures de SceneBase et les quatre voitures de Scenes Antoine utilisent CarFollowPoints, avec le tag voitures. Les tags nécessaires au mode de recherche libre sont également déclarés.
- CarPointRoute conserve les points ordonnés et les liaisons des anciens parcours, sans dépendance au NavMesh. CarFollowPoints conserve son mode original de recherche par tags lorsque route est vide. Avec une route assignée, il suit les points successifs et utilise IntersectionChooser pour choisir une branche à la fin du parcours.
- SceneBaseTrafficSetup sélectionne un point devant chaque voiture au démarrage. Le circuit de SceneBase garde sa route de retour. La fin de parcours sans suite de la scène de travail Antoine reste un arrêt volontaire du contrôleur.
- Les anciens composants manquants et les NavMeshAgent des voitures ont été retirés des deux scènes. Le NavMesh enregistré est conservé comme asset mais ne pilote plus les voitures.
- Le nouveau déplacement suit directement les segments entre points, à hauteur constante ; il ne possède pas le freinage devant les obstacles de l’ancien contrôleur NavMesh. Les virages, collisions, pentes et distances entre véhicules restent à évaluer en Play.

Validation de cette migration : compilation de l’assembly de gameplay avec le compilateur et les références Unity 6000.4.0f1 réussie (un avertissement préexistant dans BusCameraFollow) ; aucun script manquant dans les deux scènes ; références de composants et de routes vérifiées ; quatre contrôleurs par scène ; aucune fin sans suite dans SceneBase. Aucun test en Play ni build player exécuté pour cette migration.

## Historique de l’intégration du 9 octobre


Les fonctionnalités des scènes de travail sont réunies dans Assets/SceneBase.unity. Les scènes sources, leurs scripts et leurs prefabs ne sont pas modifiés. Les scènes du build restent identiques.

## Fonctionnalités intégrées

- Magasin Crousty, six adresses de livraison et six commandes par tournée.
- Chargement automatique au magasin, livraison avec E, projectile en cloche et nouvelle tournée après le retour au magasin.
- HUD de livraison, distances, flèches et marqueurs des objectifs, compteur de passagers.
- Quatre voitures IA, cinq parcours et un carrefour à deux sorties. Les parcours sont replacés dans la ville de la base et une route de retour ferme le circuit.
- NavMesh enregistré dans Assets/SceneBaseData/TrafficNavMesh.asset, dédié à cette scène. Le trafic peut ainsi démarrer sans rebake des meshes FBX non lisibles dans le player.
- Colliders des voitures ajustés au modèle, Rigidbody cinématique unique par voiture et détection des obstacles.
- BusCarCrash sur le bus actuel, avec l'effet original : explosion à partir de 60 km/h, conservation de 90 % de la vitesse.
- Arrêt et passager déjà présents conservés. BusDoor est ajouté au bus actuel ; l'embarquement exige l'arrêt du bus, s'interrompt quand il repart et reprend lorsqu'il revient. Le passager revient à l'arrêt 15 secondes après le départ du bus une fois embarqué.
- HUD masqué et livraisons suspendues pendant la défaite/pause. Le redémarrage réinitialise la scène entière.

La police, le bus, sa caméra et les 78 plots/lampadaires restent ceux de SceneBase. Aucun second bus, caméra, arrêt ou exemplaire de la ville n'est importé.

## Fichiers propres à la base

Les raccords SceneBaseSession, SceneBaseBusStop et SceneBaseTrafficSetup sont dans Assets/Scripts/SceneBase et ne sont attachés que dans la base. Le composant BusStopZone et le composant BusStopPassenger d'origine sont désactivés par des overrides d'instances dans cette scène ; leurs fichiers et prefabs restent inchangés.

Le prefab graphique de commandes Assets/Sprites/Canvas.prefab et les scènes de démonstration restent disponibles à leur emplacement d'origine. Ils ne contiennent pas de fonctionnalités de gameplay branchées à importer.

## Validation

48 vérifications réussies, 0 échec, sur une copie isolée du projet avec Unity 6000.4.0f1 :
- bus/caméra uniques, références des livraisons, 78 objets d'impact et effet d'explosion ;
- accessibilité des six adresses et du magasin depuis le NavMesh ;
- quatre agents actifs, déplacement de chaque voiture, freinage devant un obstacle et absence de fin de parcours sans suite ;
- chargement de six commandes, six livraisons par injection de la touche E, fin de tournée et rechargement ;
- interruption/reprise de l'embarquement, comptage unique, affichage et renouvellement du passager ;
- suspension/reprise de l'interface et des livraisons ;
- collision ordinaire à faible vitesse, explosion à haute vitesse et élan conservé ;
- défaite policière et redémarrage réinitialisant commandes, passagers et trafic.

Tous les segments et toutes les liaisons du réseau sont également vérifiés par NavMesh.CalculatePath lors de l'assemblage. Les empreintes SHA256 des fichiers existants hors SceneBase sont comparées après le report.

Les tests ne remplacent pas une appréciation visuelle en Play dans l'éditeur. Les réglages de conduite et de poursuite sont conservés.

## Correction des rampes et vibrations du van — 9 octobre 2026

Dans SceneBase, SceneBaseGrounding laisse les WheelCollider déterminer la hauteur du van et applique un couple amorti pour suivre la pente. Il désactive le saut vertical automatique du contrôleur sur les bordures. La conduite arcade conserve la vitesse verticale issue de la physique sans y ajouter une seconde fois la pente du véhicule. Le centre de masse est abaissé et la suspension utilise 0,3 m de débattement avec un amortissement de 5500.

Le collider du mesh Road_Markings de la nouvelle ville est désactivé par un override dans SceneBase : la peinture ne doit pas former des bosses physiques. SceneBaseTerrainRefresh actualise les anciens réglages dans la scène déjà chargée, avec Undo, sans la recharger ni la sauvegarder automatiquement. Le suivi du terrain est activé uniquement par SceneBaseGrounding ; les autres scènes gardent leur comportement.

Validation physique dans une copie isolée sous Unity 6000.4.0f1 : franchissement des deux véritables meshes Ramp_Access à 30 km/h, puis avec le contrôleur complet réglé à 90 km/h ; contact au sol sur 85 à 87 % des pas mesurés dans la montée à haute vitesse. Un parcours plat de 179 m est également vérifié, avec une variation verticale de 0,14 mm pendant la fenêtre de mesure après stabilisation. Aucun échec dans ces contrôles. L'appréciation visuelle finale reste à faire dans le mode Play de l'éditeur principal.

## Fumée des pneus pendant le drift

SceneBaseDriftSmoke est attaché uniquement au van de SceneBase. Quatre émetteurs suivent les contacts au sol des pneus, avec une émission quatre fois plus forte aux roues arrière. L'intensité suit la glisse physique et l'animation de drift arcade, à partir de 14 km/h. Les particules restent en espace monde, montent et grossissent légèrement, puis se dissipent ; l'émission s'arrête dès qu'une roue perd son contact. Un reset éloigné nettoie la traînée.

Chaque bouffée utilise désormais une icosphère subdivision 1 : 12 sommets géométriques et 20 faces triangulaires. Les sommets de rendu sont séparés par face pour conserver un ombrage plat et des facettes visibles. Le mesh est partagé entre les quatre émetteurs. Le matériau Assets/SceneBaseData/DriftSmoke.mat utilise le shader URP CartoonDriftSmoke, blanc avec une ombre bleutée, qui atténue l'opacité en fin de vie. L'émission de 24 bouffées par seconde aux pneus arrière produit deux traînées lisibles ; les émetteurs sont créés une fois et plafonnés à 128 particules au total. Le composant ne modifie pas la physique du véhicule.

Validation isolée des icosphères : 35 contrôles réussis, 0 échec, couvrant la géométrie à 20 triangles, les normales plates orientées vers l'extérieur, les drifts gauche/droite, la prédominance arrière, la dissipation après redressement, la présence du mesh et du shader, et l'absence de fumée à l'arrêt, en ligne droite et en l'air. Un rendu de caméra est également inspecté pour vérifier les facettes. Les pics mesurés sont de 78 à 80 bouffées vivantes.
