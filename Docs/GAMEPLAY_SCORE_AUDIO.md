# Tournées, score, menus et sons

La boucle de SceneBase est maintenant : récupérer six Crousty, livrer les six colis en **90 secondes**, gagner des points, puis retourner au restaurant pour lancer une nouvelle tournée. Le chrono commence au retrait et s’arrête à l’arrivée du sixième colis, pas au lancer. La pause suspend le temps de livraison. Les huit îlots disponibles les plus proches servent de pool de tirage : au plus un client par îlot procédural, et aucun dans la zone de retrait du restaurant. Si six clients éligibles ne sont pas encore chargés, la tournée attend leur génération.

`DeliveryManager` expose `runTimeLimit`, `nearbyAddressPool`, `pointsPerDelivery`, `completionBonus` et `pointsPerRemainingSecond`. Les valeurs initiales donnent **100 points par colis**, puis **500 + 10 × secondes restantes entières** pour une tournée terminée. Exemple : six livraisons en 30 secondes donnent 600 + 500 + 600 = **1 700 points**. Le score s’additionne entre les tournées d’une même partie ; le meilleur score est conservé localement. Recommencer la partie remet le score courant à zéro.

Le cartouche SCORE existant reçoit un comptage fluide, une petite impulsion de taille et un gain flottant. Un compte à rebours reprend le style du compteur de vitesse, sous l’horloge. Tous les nouveaux textes du HUD sont en anglais : **DELIVERY RUN**, **TIME LEFT**, **RUN COMPLETE**, **OUT OF TIME**, ainsi que les consignes de livraison. L’horloge conserve le format AM/PM et les phases **MORNING**, **DAY**, **SUNSET**, **NIGHT**. Les quinze dernières secondes sont signalées visuellement et par l’alarme. Le dépassement du délai déclenche `PoliceGameOverManager.TriggerTimeoutGameOver()` : même écran et même bouton de reprise que lors d’une capture, avec le titre **OUT OF TIME!**. Une livraison en vol ne peut plus donner de points après la défaite. La capture policière termine aussi la tournée.

Les panels de menu, options, catégories, pause et défaite utilisent `CroustyPanelMotion`. Les transitions durent 0,32 s à l’ouverture et 0,16 s à la fermeture, avec fondu, déplacement, légère rotation et rebond. Les boutons conservent leurs éléments graphiques mais se compriment à la pression et rebondissent au clic. Les animations utilisent le temps réel pour continuer pendant la pause ; elles restaurent les positions, tailles et rotations d’origine. La navigation logique change immédiatement, et les éléments en transition ne reçoivent pas de clics. Les outils de vérification en mode édition conservent la navigation instantanée et ne modifient pas la mise en page.

L’horloge **Time of day / Clock**, le chrono **Delivery countdown / Countdown** et le gain **Carte score / Animated score gain** sont enregistrés comme objets du Canvas dans **SceneBase** et **SceneJojo**. Ils sont visibles et modifiables hors Play. `CroustyHUD` reçoit leurs composants TMP via les champs sérialisés `timeValue`, `timerValue`, `scoreValue` et `scoreGain` ; il actualise les valeurs et anime les objets existants, sans créer ces panneaux au lancement. Les polices, fonds, ancres et emplacements reprennent les cartouches existants. L’outil d’éditeur `Tools/Crousty/Placer l'horloge et la tournée dans le Canvas` sert uniquement à mettre à jour une ancienne scène encore ouverte en mémoire ; la conversion des deux fichiers de scène est déjà effectuée.

## Banque sonore

Le singleton `CroustySoundDesignManager` démarre automatiquement et persiste entre les scènes. **Assets/Resources/Crousty/SoundBank.asset** référence directement les quatorze fichiers d’Assets/SoundEffect ; il permet de régler le clip, le volume, le début de lecture et la durée maximale de chaque cue. Le volume général reste relié au réglage existant du menu. Dix sources sont réutilisées pour les effets, dont trois réservées à l’interface, et quatre pour les boucles. Les sons de jeu se suspendent pendant la pause ; les sons d’interface restent disponibles, et l’ambiance se fait plus discrète.

| Fichier | Déclenchement |
| --- | --- |
| pop button sound.wav | Clic de bouton, souris ou activation du bouton |
| Paper UI transition in the menu.mp3 | Ouverture et fermeture de Pause ou Options, et ouverture de l’overlay de défaite ; aucun son papier au changement de catégorie ni au chargement initial du menu |
| Coin give.wav | Arrivée d’une livraison et gain de points |
| Recompense fin de commande.wav | Sixième colis arrivé avant le délai |
| Cartoon - Alarm.mp3 | Quinze secondes restantes et défaite |
| audio__Fart (6).mp3 | Activation réussie du PET |
| Acceleration_fart.mp3 | Reprise de l’accélération, avec délai entre deux sons |
| Crousty Bus Ambiance .mp3 | Ambiance en boucle pendant la partie |
| Drift Sound Effect.wav | Drift, avec volume progressif lié à la fumée existante |
| Explosion Sound Effects.wav | Explosion d’une voiture percutée |
| fly mode.mp3 | Activation du mode avion |
| Police Siren Sound Effect.wav | Poursuite policière active |
| reverse drive mode .mp3 | Marche arrière |
| voiture qui klaxonne.mp3 | Touche H ou collision légère avec une voiture |

Le son « paper » commence après son silence initial, à 0,74 s ; le premier effet d’explosion commence à 1,19 s et se limite à 2,8 s pour éviter de jouer toute la compilation de 29 secondes. Les autres petites latences détectées ont aussi été compensées. Une courte atténuation évite les coupures abruptes. L’ambiance est importée en streaming, les grands effets en mémoire compressée, et les petits effets préchargés en arrière-plan. Les fichiers audio d’origine sont conservés.

## Nouveau STOP

**Assets/Resources/Crousty/StopSign.glb** est une copie du modèle « Stop sign by Poly by Google - 60GyU9CdZ9r.glb » fourni par l’utilisateur. L’importeur glTFast déjà installé dans le projet conserve son mesh et sa texture. Les STOP procéduraux réutilisent le même asset, ajusté à 3,4 m de hauteur et orienté face à l’approche de la route. Le pied reste à la hauteur du sol. Le panneau conserve la logique de chute ; le mesh importé n’est pas dupliqué ni détruit à chaque quartier. L’ancien panneau reste uniquement un secours si l’asset ne peut pas être chargé.

## Vérification

Les assemblies de jeu et d’éditeur compilent avec le compilateur Unity, sans erreur. Les outils d’éditeur existants gardent quelques avertissements d’API obsolète. Les **44 vérifications** de `Tools/Validation/GameplayLoopChecks.cs` passent dans le projet isolé : références vers les textes enregistrés dans le Canvas, absence de création d’objets UI au démarrage, quatorze clips, pool audio, import et physique du STOP, transitions en pause, son papier réservé aux changements d’overlay, textes anglais, tirage local, six arrivées, calcul exact du score, fin de tournée, cumul, compte à rebours, défaite et refus d’un colis tardif. Les aperçus se trouvent dans `Docs/Previews/loop-hud.png` et `stop-poly.png`. Le test utilise des adaptateurs de conduite et des matériaux temporaires compatibles avec son moteur de rendu ; il ne remplace pas un essai manuel du bus pour équilibrer les 90 secondes.
