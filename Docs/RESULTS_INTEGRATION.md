# Intégration de l'écran Results

Intégration du 13 octobre 2026, depuis les commits distants `745b859` et `6a5d7b0`, dans la SceneBase locale sauvegardée par `09a68f6`.

Seul le sous-arbre `GameOverPolice` a été remplacé par le panneau Results distant, avec ses textures, sa vidéo et ses cinq valeurs. Les références du gestionnaire de fin ont été reliées au nouveau panneau. Les 7 101 autres documents sérialisés de SceneBase sont identiques octet pour octet à la version locale précédente. L'asset RetroBlendy n'a pas été modifié.

Le gestionnaire conserve la navigation à la manette et le redémarrage par chemin de scène. Il compte les bonus consommés, y compris pendant l'image de la défaite, et affiche les livraisons cumulées, les explosions et le score. Le compteur de livraisons existait déjà localement ; il n'a pas été ajouté une seconde fois. BusCarCrash conserve sa détection et ses interactions avec la police ; seul le compteur d'explosions a été ajouté.

La vidéo est explicitement lue avec le temps non affecté par Time.timeScale, pour continuer pendant l'écran de défaite.

## Validation

- Compilation des scripts de jeu et d'éditeur réussie avec Unity 6000.4.0f1.
- Références de SceneBase vérifiées : aucun lien local ou asset manquant.
- Tests en mode Play sur une copie isolée : capture par la police, valeurs de résultats, bonus consommé juste avant la défaite, lecture vidéo avec Time.timeScale = 0, sélection à la manette, bouton Play Again, redémarrage de la conduite, remise à zéro des compteurs et défaite par chrono.
- Aucun message d'erreur Unity pendant les deux parties de test.
- [Capture du panneau inspectée](Previews/results-integration.png). Les valeurs affichées dans cette capture sont des données de test.

Les scripts de contrôle sont uniquement dans la copie de validation sous Temp ; ils ne sont pas inclus dans le jeu.
