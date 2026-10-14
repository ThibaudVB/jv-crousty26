# Progression de la police

Réglages BASE : une patrouille au départ, jusqu’à quatre à long terme ; une voiture poursuit au début d’une première alerte, jusqu’à six lors des poursuites avancées. Les renforts ne créent plus deux voitures de réserve au-delà du quota. Les deux voitures stationnées par barrage sont distinctes du quota mobile.

La progression augmente d’un niveau toutes les six commandes livrées, sur huit niveaux : difficulté maximale à 48 livraisons cumulées, au lieu de quatre précédemment. Le tutoriel de trois commandes conserve donc la difficulté minimale. La progression se conserve entre tournées et après une fuite ; elle repart de zéro à la nouvelle partie.

Le début laisse 15 secondes de grâce, demande 1,2 seconde de détection et environ quatre secondes d’immobilisation pour une arrestation. Perdre la police demande cinq secondes hors de vue à faible pression. Les alertes s’intensifient ensuite : détection plus rapide, suivi radio et recherches plus longs, coups de bélier plus rapprochés. La vitesse de poursuite de base passe de 138 à 125 km/h, le bonus tardif de 42 à 36 km/h et le rattrapage est moins fort. La difficulté tardive provient du cumul de ces pressions et des obstacles, avec moins de véhicules.

Les premiers barrages deviennent possibles après huit commandes livrées, avec dix secondes de préavis de génération. Jusqu’à trois barrages simultanés, un nouveau toutes les 40 à 12 secondes selon la pression ; leurs rangées passent progressivement de une à trois. Les livraisons individuelles ne réinitialisent plus le délai d’apparition à chaque fois.

L’hélicoptère est éligible à partir de la deuxième tournée chargée (celle de cinq Crousty après le tutoriel). Premier passage au plus tôt 18 secondes après le début de cette manche, sous réserve que la zone soit chargée. Chaque visite dure 22 à 32 secondes, même en poursuite ; une absence de 95 à 60 secondes suit le départ, l’éloignement ou la destruction. Les premières bombes attendent cinq secondes de contact. Les largages restent espacés de six secondes en manche 2, puis l’intervalle baisse de 25 % à chaque manche : 4,5 secondes en manche 3, 3,375 en manche 4, 2,531 en manche 5, jusqu’à un minimum d’une seconde. Cette cadence dépend de la manche ; la durée et le retour des visites suivent les livraisons cumulées. Les pauses figent ces délais.

Les valeurs de base sont enregistrées dans `Assets/SceneBase.unity` et correspondent aux nouvelles valeurs par défaut de `PoliceChaseManager`. Les autres scènes conservent leurs valeurs sérialisées propres.

Validation : `Tools/Validation/PoliceBalanceChecks.cs`, à copier sous Assets dans une copie isolée complète, puis exécuter `PoliceBalanceChecks.Run` sans `-quit`. Vérifie les quotas et vitesses à plusieurs paliers, le début facile, l’éligibilité de l’hélico à la deuxième manche et la durée limitée des visites avec délai de retour.

Résultat Unity 6000.4.0f1 : 12 contrôles réussis, zéro échec. Exemples mesurés sans bonus de rattrapage : 127,16 km/h au départ, 142,82 à 18 livraisons et 171,8 au maximum. Le ressenti final de difficulté reste à ajuster après une partie complète.
