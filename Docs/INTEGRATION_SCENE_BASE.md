# Intégration dans SceneBase

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
