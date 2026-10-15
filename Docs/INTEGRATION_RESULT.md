# Intégration main + fix suivant les choix du 15 octobre 2026

Branche de travail : `codex/integration-fix-20261015`.
Sauvegarde intégrale du travail local : `codex/sauvegarde-avant-fix-20261015`, commit `3247a9c`.
Sources intégrées : main `184ff12` et fix `1b6bba6`. Aucun push effectué.

## Résultat

1. **GPS garage uniquement** : bonus, flèches au sol, icône de réparation et repère de garage entrants. Les autres garages restent visibles sur la minimap comme dans le correctif local ; seule la destination sélectionnée reçoit l'icône spéciale.
2. **Progression mixte** : pouvoir initial aléatoire parmi les pouvoirs au sol, prix aléatoires, vol en dernier ; GOD garde tous les déblocages sans contaminer la progression des autres noms.
3. **Inertie sortante** : BusWheelRig et BusFlightController sont identiques à la sauvegarde locale. Le composant BusFlightInertia entrant n'a pas été conservé ; ses références ont aussi été retirées des scènes de test importées pour éviter des scripts manquants.
4. **Anneaux entrants** : contrôle par huit rayons contre le collider du cercle ; génération et tests mis en cohérence avec cette API. L'ancien contrôle elliptique a été retiré.
5. **Tutoriel mixte** : caméra/timing et masquage des boîtes entrants ; densité, tirage équilibré et comportement de consommation locaux conservés. Les boîtes des quartiers générés pendant le tutoriel sont créées masquées puis réactivées. La boîte PET du tutoriel peut donner sa charge même si le pouvoir initial est GPS ou riz, sans débloquer PET dans les autres boîtes.
6. **Scène mixte** : HUD récent de main et réglages locaux des ailes. La navigation des crédits de fix fonctionne avec les références existantes du menu pause ; pas de remplacement global de SceneBase par la scène divergente de fix.
7. **Crédits entrants** : accès séparé des réglages, trois onglets de réglages ; titre anglais CREDITS conservé.
8. **RetroBlendy** : asset complet de main, sans mélange des tableaux générés de fix. Tables de caractères/glyphes et références internes contrôlées.
9. **Correctifs locaux conservés** : GOD exact, exclusion du classement, santé, police, livraisons, figures, ailes, collisions du bus et génération anticipée des collisions. Le garage reste sans cube visuel ajouté ; son nouveau repère GPS est indépendant du trigger de réparation.

## Validation effectuée

- Compilation du jeu et des scripts Editor avec Roslyn/assemblies Unity 6000.4 : réussie. Quatre avertissements de dépréciation déjà présents dans CroustySettings.
- `Tools/Validation/GodLeaderboardChecks.ps1` : réussit sur les véritables sources de profil/classement avec stockage Unity simulé en mémoire.
- `Tools/Validation/IntegrationAssetsChecks.ps1` : réussit. IDs uniques et références internes résolues dans SceneBase, le menu principal, les trois scènes Jojo/test et RetroBlendy ; réglages des ailes, présence des composants et nouvelle progression vérifiés ; glyphes RetroBlendy et imports du marqueur garage cohérents.
- Comparaison avec `3247a9c` : scripts BusDamage, BusHealthHearts, BusWings, DeliveryManager, BusAirTricks, BusController, BusWheelRig, BusFlightController, CroustyLeaderboard, CroustyNameEntry, scripts Police et PowerUpInventory inchangés.
- `FlightDurationChecks` mis à jour et compilé : couvre aussi la progression mixte, la boîte PET tutorielle, les boîtes générées masquées et le contrôle entrant des anneaux.
- Tentative d'exécution Play via Unity : l'éditeur quitte au démarrage avec code 1, avant les tests (`Logs/integration-flight-checks.log`). **Les contrôles Play, le rendu et une partie complète ne sont donc pas validés.**

## Essai en jeu restant

Lancer le tutoriel avec chaque pouvoir initial possible ; terminer la collecte de la boîte PET et vérifier le retour des boîtes normales. Vérifier le GPS garage et ses repères, le menu crédits au menu principal et en pause, les passages dans/hors des anneaux, puis le vol, les saltos, les ailes et les collisions sur une longue tournée. Vérifier enfin CathyLaVodka puis un profil normal pour confirmer les droits et le classement dans le player réel.
