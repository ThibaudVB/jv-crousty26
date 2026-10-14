# Choix des pouvoirs

Dans `Assets/SceneBase.unity`, `PowerUnlockProgression` sur CroustyBus gère les déblocages pour la partie en cours. Le PET est disponible dès le départ dans les box. Les achats ajoutent un pouvoir au tirage des box ; ils ne donnent pas directement de charges et ne remplacent pas les deux emplacements d’inventaire.

Les paliers correspondent à la taille de la tournée terminée, pas au cumul des commandes :

| Tournée | Pouvoir proposé |
| --- | --- |
| Tutoriel, 3 | Pas de choix ; PET déjà débloqué |
| 5 | Express GPS ou Asian Rice |
| 10 | GPS/Riz restant ou Flying Bus |
| 15 | Dernier pouvoir manquant : Vol normalement, ou GPS/Riz si le Vol a été acheté à 10 |

Prix provisoires dans l’Inspector : GPS 130 $, Riz 130 $, Vol 5 000 $. `DeliveryManager.Money` représente le score gagné moins les achats ; `Score`, les records et le classement restent indépendants des dépenses. Les déblocages et les dépenses sont réinitialisés au lancement d’une nouvelle partie.

Une box tire uniformément parmi les pouvoirs débloqués qu’elle peut effectivement ajouter à l’inventaire (emplacement libre ou recharge possible). Une box ne disparaît pas si aucun pouvoir ne peut être ajouté. Les scènes sans `PowerUnlockProgression` conservent leur fonctionnement antérieur.

Le panel et les deux Canvas sont enregistrés dans la scène : fond flouté sous le HUD, choix au-dessus. Aucun panel n’est construit en Play. Le décor est capturé puis flouté lors de l’ouverture. Le jeu est en pause, le HUD reste net, le dialogue normal et les repères de livraison sont masqués. Souris, navigation clavier/manette et validation UI sont prises en charge. La sélection utilise le prix orange et le symbole A de la maquette.

Un achat débite une seule fois, ferme le panel, rétablit le temps et le curseur puis prépare immédiatement la tournée suivante. Si aucun choix n’est abordable, un bouton Continue permet de poursuivre sans achat ; le choix reste proposé à la fin d’une tournée ultérieure. Les pouvoirs déjà achetés ne sont jamais reproposés.

`Tools/Crousty/Installer le panel de choix des pouvoirs` installe les objets sur une scène BASE qui ne possède pas encore le composant. Les éléments visuels utilisent les sprites et la police existants du projet.

Validation : `Tools/Validation/PowerChoiceChecks.cs` se copie dans Assets d’une copie isolée complète et s’exécute avec `PowerChoiceChecks.Run`. Les contrôles couvrent le pool PET initial, les box après déblocage, les paliers, les deux parcours d’achat, le débit séparé du score, le double submit, la pause, le manque d’argent et le lancement immédiat de la tournée suivante. Rendus inspectés en 1920×1080 et 1280×960.

Résultat sous Unity 6000.4.0f1 : 22 contrôles réussis, zéro échec, y compris survol souris et validation par l’événement Submit de l’UI. Aperçus : [16:9](Previews/power-choice.png) et [4:3](Previews/power-choice-4x3.png).
