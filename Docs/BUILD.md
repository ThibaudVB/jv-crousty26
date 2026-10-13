# Build Windows

`Tools > Crousty > Build > Construire Windows` génère `Build/Crousty.exe` avec les scènes activées dans Build Profiles. Le menu principal est la première scène et Start Game ouvre SceneBase. Les autres scènes sont conservées.

La brume de la ville est activée par `ProceduralCity` pendant le jeu. `GraphicsSettings` utilise désormais Fog Modes = Custom avec les variantes de brume conservées : la détection automatique sur les scènes sauvegardées, où la brume est désactivée, pouvait les retirer. Voir la [documentation Unity sur Fog Modes](https://docs.unity.com/en-us/engine/6000.0/manual/unity-editor/editor-settings-reference/comp-manager-group/class-graphics-settings).

Les surfaces procédurales et les feux utilisent des matériaux dans `Resources/Crousty` : `ProceduralSurface` et `ProceduralSignal`. Ils rendent explicite l'inclusion du shader URP Lit, de l'instancing et de l'émission des feux, plutôt que de dépendre uniquement d'un Shader.Find et de mots-clés activés pendant le jeu.

`CroustyBuild` vérifie avant chaque build les scènes de démarrage, les variantes de brume, le ciel, l'éclairage, les marqueurs, les matériaux procéduraux, la banque de sons, le panneau STOP et les sprites du gérant. Pendant la préparation des scènes, il vérifie les scripts manquants et les références de la fumée. Ces vérifications ne modifient pas les scènes.

La fumée de drift, la mini-carte, les HUD et l'interface du menu utilisent leurs références sérialisées dans les scènes. Les ressources dynamiques du ciel, du cycle jour/nuit, du dialogue et des sons sont incluses via leurs dossiers Resources.

Le contrôle dans le player a également révélé deux roues `BR WHEEL.008` dans BASE avec un Rigidbody dynamique et un MeshCollider non convexe. Leurs colliders sont désormais convexes, ce qui rend cette combinaison valide pour PhysX. Aucune scène source n'a été modifiée.

`Tools/Validation/BuildPlayerChecks.cs` peut être copié dans Assets d'une copie isolée complète du projet pour tester un player réel avec `--crousty-build-checks`. Il contrôle le menu, les shaders, les sons, le lancement de BASE, la génération de la ville, la brume, la mini-carte et les particules des roues. Il capture également une vue avec la brume et une autre sans brume. Ce script de contrôle n'est pas inclus dans le projet de release.

Validation du 12 octobre 2026 sous Unity 6000.4.0f1 : build Windows réussi, puis 16 contrôles réussis dans un player réel, sans erreur au lancement de BASE. Les vues [avec brume et fumée](Previews/build-fog-smoke.png) et [sans brume](Previews/build-without-fog.png) ont été rendues et inspectées : la brume masque bien les bâtiments lointains et le matériau des particules est présent. Il s'agit d'un contrôle automatique de chargement et de rendu, pas d'une mesure de performances ni d'un essai manuel de conduite.
