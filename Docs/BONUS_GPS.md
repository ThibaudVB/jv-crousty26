# Bonus GPS

Dans SceneBase, le GPS est sur DeliveryManager et possède trois charges au départ pour les tests. Après chargement au magasin, G sélectionne une adresse accessible ; G à nouveau passe à la suivante. Le trajet cyan et sa distance apparaissent sur la minimap. Le trajet est recalculé toutes les 0,75 s ; sans chemin complet, il est masqué.

Seule la fin de livraison de l'adresse guidée consomme une charge. Changer d'adresse ou recalculer ne coûte rien. Le choix est bloqué pendant le vol du colis guidé. Après trois livraisons, le guidage est épuisé. La pause suspend les commandes et masque la minimap.

Un futur pickup peut appeler BusGPS.GrantBonus() pour recharger à trois charges ; startingCharges peut être mis à zéro pour commencer sans bonus. Aucun pickup physique n'est ajouté ici.

Le GPS construit une navigation indépendante sur les colliders de navigationRoot, avec une marge de 2 m autour des obstacles et une hauteur de véhicule de 4 m. La racine actuelle est détectée via Ground_Asphalt. Après génération/remplacement de la carte, appeler RebuildNavigation(nouvelleRacine) : la navigation sera reconstruite à la prochaine sélection. Les meshes doivent rester lisibles en mémoire ; Read/Write est activé sur le FBX de la carte actuelle pour fonctionner aussi hors éditeur. La première sélection construit la navigation de façon synchrone ; pour une très grande carte procédurale, prévoir sa construction pendant le chargement.

Validation Unity 6000.4.0f1 : trois chemins complets calculés sur la carte active, consommation à la fin de trois vrais lancers du gestionnaire, nettoyage des tracés et refus de guidage une fois les charges épuisées. Ce contrôle ne remplace pas un parcours conduit manuellement ni un profilage sur la machine cible.

La minimap utilise un radar cartographique incliné à 55°, orienté selon le cap du bus. Les silhouettes de la géométrie active sont aplaties : rues claires, îlots sombres, empreintes des bâtiments et parcs discrets. Le trajet cyan, les marqueurs et le nord utilisent la même projection. Les hauteurs de la carte sont simplifiées pour privilégier la lecture des routes ; ce radar ne distingue pas les étages superposés.

Pour la génération procédurale, appeler aussi `BusMinimap.RefreshMap(nouvelleRacine)` après génération ou modification de chunks. `RadarMapElement` permet de classer chaque MeshFilter comme Road, Block, Park ou Building ; la carte actuelle utilise ses noms de meshes comme adaptateur. La génération du radar ne modifie pas les matériaux ni les couches de la scène principale. Le shader est référencé dans SceneBase pour être inclus dans le build.
