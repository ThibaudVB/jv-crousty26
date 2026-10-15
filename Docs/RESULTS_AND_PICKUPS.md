# Résultats et box

Score final : secondes entières de survie × 20 + livraisons × 100 + bonus consommés × 5 + voitures explosées × 30 + dollars gagnés × 60. Le temps utilise le temps de jeu écoulé (les pauses sont exclues). Les dollars sont les gains bruts de la partie : les achats ne les retirent pas du calcul. Chaque ligne affiche la quantité puis sa contribution. Le classement et le record reçoivent la somme finale ; les gains en cours de tournée restent la monnaie du jeu.

SceneBase contient une ligne Money et un bouton Main Menu, clonés des éléments existants pour conserver les papiers, la police et les interactions. Retour vers CroustyMainMenu avec remise du temps à 1.

L’ancien clip de giro est rétabli. Il est joué par les sources 3D des véhicules, avec le budget de volume commun et l’atténuation par distance. Pendant la musique, il descend à 35 % de son volume réglé. L’introduction de la poursuite ne double plus le son avec une source 2D.

Les rues ouvertes reçoivent quatre box par rangée, au milieu du tronçon, tous les cinq blocs (320 m) le long de chaque rue, avec décalage entre rues voisines. Les box réapparaissent après 18 secondes, se déchargent avec leur quartier et utilisent le tirage des pouvoirs débloqués. Chaque anneau procédural reçoit aussi une box centrée. Les visuels réutilisent le prefab du tutoriel à une taille de 1,8 m, avec uniquement un collider déclencheur. L’audio des anneaux n’est pas modifié.

Validation : scripts compilés avec le compilateur Unity ; calcul réel testé pour zéro, chaque catégorie, un total combiné (93 535) et les entrées négatives. Références de SceneBase et cadence des rangées vérifiées statiquement. Pas de validation visuelle en Play sur cette révision.
