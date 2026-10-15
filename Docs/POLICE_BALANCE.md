# Police, trafic et points de retrait

La difficulté routière d’avant le rééquilibrage est rétablie : trois patrouilles initiales, jusqu’à dix ; deux poursuivants à la première alerte, jusqu’à douze avec les renforts. Les deux véhicules de réserve sont de nouveau présents. Progression à chaque commande livrée, maximum au bout de quatre commandes. Vitesse de base 138 km/h, bonus tardif 42 km/h, ancien rattrapage et anciens coups de bélier.

Grâce initiale de trois secondes, détection de 0,35 seconde, arrestation initiale de 2,5 secondes, recherche initiale de huit secondes. Barrages dès la première livraison, jusqu’à six, intervalle de 24 à cinq secondes. Les paramètres sérialisés de SceneBase correspondent au code. Le debug visuel reste retiré ; le son des poursuites est conservé.

L’hélicoptère reste disponible à partir de la deuxième manche, avec des visites de 22 à 32 secondes et un retour après 95 à 60 secondes selon la pression. Les largages restent espacés de six secondes en manche 2 puis accélèrent de 25 % par manche, avec un minimum d’une seconde. La pression aérienne reste indépendante de la police terrestre : un palier toutes les six commandes, maximum à 48. Le retour à l’ancienne police ne raccourcit donc plus prématurément les pauses entre passages.

Les restaurants sont placés sur une grille de quatre blocs (256 m), contre huit précédemment (512 m). Les emplacements réservés aux parcours et grands éléments peuvent encore supprimer certains restaurants. La recherche du point de retrait et sa génération utilisent la même grille, y compris aux coordonnées négatives.

Trafic civil : 28 / 48 / 72 voitures selon le réglage léger / normal / dense, contre 12 / 28 / 48. Les civils sont replacés au-delà de 190 m et apparaissent sur une rue chargée entre 55 et 160 m, hors champ ; au démarrage, la distance minimale est de 30 m. Leur quota est indépendant des policiers.

Validation : paramètres de police comparés à e76c1cd et grille de restaurants vérifiée statiquement. Le test Play PoliceBalanceChecks a été actualisé, mais non exécuté sur cette révision : la copie temporaire de validation Unity n’est plus disponible.
