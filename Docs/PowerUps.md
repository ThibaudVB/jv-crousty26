# Atouts et charges

Chaque boîte accorde un atout avec sa capacité complète : PET ×3, RIZ ×3,
GPS ×1, VOL ×1. Les limites sont appliquées aussi dans les contrôleurs et
l'inventaire. Recharger un type déjà présent complète son emplacement sans
créer une deuxième pile. L'atout de réserve passe automatiquement devant
quand l'actif est épuisé.

E au clavier ou X à la manette active l'atout principal. Tab au clavier,
ou gauche/droite de la croix directionnelle à la manette, permute le contenu
des deux emplacements. La permutation conserve les objets UI et les charges.
La sélection de la manette change désormais l'atout réellement activé.

Le GPS consomme son utilisation seulement quand il a trouvé une route vers
une livraison en attente. Sans commandes, il conserve sa charge et affiche
« GPS : prends des commandes » sur la mini-carte. La route reste affichée
après consommation jusqu'à la livraison. Le vol conserve les ailes installées
dans SceneBase.

`Tools/Validation/PowerUpIntegrationChecks.cs` vérifie les ramassages réels,
les trois activations du PET et du RIZ, les limites, la recharge d'une pile,
les compteurs du HUD, le GPS en réserve, la permutation, une route dans la
ville procédurale, la conservation de la charge sans commandes et le vol
avec déploiement des ailes. Unity 6000.4.0f1 termine avec
`POWERUP_CHECKS_DONE failures=0` dans la copie isolée.

L'ancienne erreur de références HUD de SceneBase est comptée séparément ;
aucune nouvelle erreur Unity n'apparaît dans les tests. Cette correction
d'atouts ne modifie pas SceneBase.
