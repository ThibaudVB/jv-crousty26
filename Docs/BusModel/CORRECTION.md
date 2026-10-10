# Faces du Crousty Bus — correction Unity

Le FBX `Assets/NouveauCroustyBus.fbx` présentait deux problèmes :

- Unity rejetait huit polygones de Carroserie comme auto-intersectants. La triangulation explicite avec Blender préserve les sommets, les UV par coin, les normales importées, les matériaux, les transformations et les identifiants FBX. Blender produit 1 153 triangles, dont 172 d’aire nulle ; Unity importe maintenant les 981 triangles non dégénérés, contre 980 auparavant, sans avertissement de polygone rejeté pour ce modèle. Il ne s’agissait donc pas de 173 faces visibles perdues.
- Les matériaux URP Lit importés utilisaient le rejet des faces arrière (_Cull=2). Certaines surfaces du modèle, notamment le dessous et des surfaces des lunettes, disparaissaient suivant l’angle. CroustyBusModelImport règle le rendu double face (_Cull=0) uniquement pour ce FBX à chaque import. Les autres modèles et matériaux ne sont pas modifiés par ce réglage.

Le fichier .meta du modèle garde son GUID. Les références de mesh et de matériau des neuf renderers du prefab CroustyBus sont toutes résolues après réimportation. Les scripts de conduite, colliders et réglages physiques ne sont pas modifiés par cette correction.

Validation dans une copie isolée sous Unity 6000.4.0f1 : import réussi, 981 triangles de carrosserie, références du prefab valides, matériaux en double face, rendus de contrôle sous plusieurs angles. Les images avant.png et apres.png montrent le changement de visibilité des faces arrière depuis une vue basse ; ce comparatif ne représente pas un changement de couleur ou de forme souhaité.

`Tools/Validation/triangulate_bus.py` permet de reproduire la triangulation avec Blender sur une copie source du FBX. La correction binaire conserve les identifiants utilisés par les scènes Unity. Si le modèle est réexporté depuis Blender, trianguler avant export pour éviter le retour des polygones rejetés ; le postprocesseur Unity réapplique automatiquement le rendu double face.
