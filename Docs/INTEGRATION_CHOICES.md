# Comparaison avant intégration — 15 octobre 2026

Analyse après `git fetch origin --prune`. Aucun checkout, merge, commit, stash ou remplacement de fichier du jeu effectué.

## Versions comparées

- Base locale HEAD : `f88ee86`, branche `main`, plus 25 fichiers suivis modifiés et les deux nouveaux fichiers locaux GOD/tests.
- GitHub main : `184ff12` (9 commits d'écart avec le HEAD local).
- GitHub fix : `1b6bba6` (« push fix all »).
- Ancêtre commun de main et fix : `62ecc77`.
- main contient cinq commits absents de fix : `89eabc5`, `6efa5a3`, `083744d`, `c176316`, `184ff12`. fix contient un commit absent de main.

« Sortant » désigne ici les fichiers locaux actuels, corrections non commitées comprises. « Entrant » désigne les nouveautés GitHub ; chaque ligne précise main ou fix. Ce vocabulaire décrit cette proposition, pas une garantie sur le libellé d'un futur outil de rebase.

## Choix proposés

| Nº | Sujet | Sortant | Entrant | Recommandation |
|---|---|---|---|---|
| 1 | GPS | Bonus vers les livraisons ; guidage garage déjà disponible séparément | fix transforme le bonus en GPS garage uniquement, avec icône, repère bleu et choix par longueur du trajet | Décision de gameplay : conserver le sortant tant que le remplacement par le garage seul n'est pas choisi. Les améliorations du repère garage peuvent être intégrées séparément. |
| 2 | Progression des pouvoirs | PET au départ, prix fixes, exception GOD | main active un pouvoir initial aléatoire, prix 600–900 / 1000–1400 et vol en dernier dans SceneBase | Mixte si ces nouvelles règles sont souhaitées : nouvelles règles + exception GOD locale. Sinon conserver les règles sortantes. |
| 3 | Inertie | Conservation de l'élan en chute, figures et ailes corrigées | main ajoute BusFlightInertia et un ralentissement progressif après contact avec le sol | Mixte : les deux corrections concernent des phases différentes. Garder VisualPivot et tous les correctifs de vol locaux ; ajouter le composant entrant et ses adaptations BusWheelRig. |
| 4 | Anneaux | Intersection de la trajectoire avec l'ouverture elliptique, suivi du même Rigidbody | main vérifie le cercle par huit rayons après franchissement | Sortant : conserver la vérification au point de franchissement. Adapter aussi la génération : ne pas conserver les affectations ringCollider/searchDistance si l'API entrante est écartée. |
| 5 | Boîtes et tutoriel | Boîtes espacées, tirage équilibré, boîte consommée même inventaire plein | main masque les boîtes pendant le tutoriel et modifie la caméra/le timing du tutoriel | Mixte : garder distribution et comportement locaux ; reprendre la mise en scène entrante. Corriger le rétablissement des boîtes des quartiers générés pendant le tutoriel. |
| 6 | Scène et HUD | Réglages locaux des ailes, scènes conservant nos composants et références | main apporte son HUD récent ; fix remanie aussi le HUD, les crédits et les références d'objets | Mixte : partir de la scène main récente, réappliquer les réglages locaux des ailes, puis reporter seulement les éléments fix choisis. Ne remplacer SceneBase par aucune version complète sans ces reports. |
| 7 | Crédits | Crédits accessibles comme catégorie des options | fix sépare l'accès aux crédits des trois onglets de réglages | Entrant, en conservant les textes anglais existants : le script entrant écrit « CRÉDITS » en dur. |
| 8 | Police d'écriture RetroBlendy | Asset local antérieur | main et fix ont chacun modifié les données générées de la police | Partir de main ; vérifier/régénérer les glyphes nécessaires. Ne pas fusionner manuellement les tableaux avec « accepter les deux ». |
| 9 | GOD, leaderboard, collisions, santé/police, garages | Correctifs locaux de cette session et modifications déjà présentes avant elle | Aucun remplacement global nécessaire | Conserver le sortant ; ajouter uniquement les dépendances des fonctionnalités choisies. Garder le garage sans cube visuel, même si le repère GPS entrant est ajouté. |

## Conflits effectivement détectés

Simulation avec `git merge-file -p` sur des copies dans `Logs/integration-audit`, sans application au projet :

- Local + main, ancêtre HEAD : FireRingSoundZone.cs (2 blocs), PowerUnlockProgression.cs (1 bloc).
- main + fix, ancêtre 62ecc77 : SceneBase.unity (2 blocs), RetroBlendy SDF.asset (68 blocs).
- Les autres fichiers touchés par les deux côtés dans ces comparaisons se fusionnent textuellement ; cela ne valide pas leur comportement ni les références Unity.
- Ces deux comparaisons isolées ne constituent pas une fusion finale des trois versions et ne prouvent pas que la future intégration compilera.

## Risques concrets à préserver

1. Les scènes distantes ont encore les ailes à `modelRotation.x = 90`, `height = 0.25`, `alongBus = 0.05`. Le local utilise 0, 0, 0.1 et le battement autour de la charnière verticale. Reprendre une scène distante entière annule une partie de la correction visuelle.
2. Accepter tout le bloc entrant de PowerUnlockProgression retire la condition IsGodMode dans IsUnlocked. Il faut fusionner cette méthode avec les nouveaux prix, pas choisir un bloc entier.
3. Choisir le script d'anneau local tout en conservant la génération entrante laisse des références à ringCollider/searchDistance qui n'existent pas dans ce script : erreur de compilation.
4. Le tutoriel entrant interdit la création des boîtes lorsque PowerBoxesEnabled est faux. Réactiver les objets existants ne recrée pas les boîtes absentes des quartiers déjà terminés. Intégration à adapter.
5. Avec un pouvoir initial aléatoire, la boîte tutorielle qui exige un PET doit rester récupérable même si PET n'est pas débloqué. Le code actuel filtre d'abord par les pouvoirs débloqués : point à traiter si le choix 2 est entrant/mixte.
6. Le GPS entrant modifie conjointement BusGPS, BusGPSRoadGuide, CroustyHUD, BusMinimap, ProceduralCity et sa génération, plus GarageGPSMarker et l'icône PNG/meta. Reprendre seulement BusGPS est insuffisant.
7. Les changements de scripts peuvent être combinés sans conflit textuel tout en perdant les réglages Unity. Les composants de figures sont réordonnés dans la scène fix ; leur apparition comme lignes supprimées dans le diff n'est pas à elle seule une suppression fonctionnelle.

## Suite après choix

Sauvegarder l'ensemble des changements locaux suivis et non suivis, préparer une intégration isolée à partir des versions ci-dessus, appliquer les décisions fonctionnalité par fonctionnalité, vérifier les références des scènes et compiler. Tester ensuite le tutoriel, la progression, le GPS, GOD/classement, le vol/atterrissage/figures/ailes et les collisions. Ne pas annoncer une validation en jeu sur la seule base d'une compilation.
