# V1 — Version complète du jeu

Le dossier `V1` réunit **tous les systèmes du projet dans une seule scène**, avec des améliorations, sans toucher aux fichiers d'origine.
On peut donc continuer à travailler dans `Assets/Scripts`, `Assets/Ia System`, `Assets/Jojo`, etc. sans risque de conflit.

## Lancer le jeu

Ouvrir **`Assets/V1/V1.unity`** et appuyer sur Play.

La scène part de `Scenes Antoine` (carte, trafic, arrêts de bus, crash) et ajoute :
- la police de `SceneBase` (PoliceSystem, PoliceCaptureTimer, écran Game Over, EventSystem) ;
- les livraisons de `SceneJojo` (DeliveryManager + HUD, Magasin Crousty, Point1 à Point6).

## Commandes

| Action | Clavier | Manette |
|---|---|---|
| Avancer | Z / W / ↑ | Gâchette droite |
| Freiner / reculer | S / ↓ | Gâchette gauche |
| Tourner | Q / A / ← et D / → | Stick gauche |
| Frein à main / **dérapage** | Espace | A (bouton du bas) |
| **Klaxon** | H (maintenir) | Y (bouton du haut) |
| Remettre le bus droit | R | Select |
| Caméra | Souris | Stick droit |
| Libérer / reprendre la souris | Échap / clic gauche | — |

## Nouveautés

### Dérapage + boost (façon Mario Kart)
- Espace **en tournant** au-dessus de 35 km/h : le bus part en glissade. Le sens du virage est verrouillé ; la direction sert à serrer ou élargir le virage.
- Plus on dérape longtemps, plus le boost se charge : **bleu** (0,8 s), **orange** (1,8 s), **violet** (3 s). Les étincelles et la jauge à l'écran prennent la couleur du niveau.
- Relâcher Espace : **boost** de +15, +25 ou +40 km/h au-delà de la vitesse max, avec flamme, son et zoom caméra.
- Traces de pneus au sol, fumée, crissement des pneus.

### Klaxon
- Son de klaxon de bus généré par code (pas de fichier audio).
- Les voitures du trafic **devant** le bus (40 m, cône de 70°) se rangent sur la droite et ralentissent ; certaines klaxonnent en retour.
- Les passagers qui attendent à un arrêt se tournent vers le bus ; ceux qui marchent vers la porte se mettent à courir.

### Police
- Gyrophares rouge/bleu sur la voiture de police (pas de son de sirène pour l'instant).
- Alerte en haut de l'écran avec la jauge d'arrestation, qui clignote de plus en plus vite.
- Game Over : le son se coupe, l'écran passe devant le HUD, et « Recommencer » marche même si la scène n'est pas dans les Build Settings (dans l'éditeur).

### HUD et gérant
- Compteur de vitesse en bas à droite (orange pendant un boost), jauge de dérapage, nombre de passagers.
- Le gérant commente aussi la police (« Les flics ! ») et les boosts (« Joli dérapage ! »).
- Système de livraison Crousty nettoyé (version de la branche `refacto-jojo`).

### Caméra et crash
- La caméra trouve le bus toute seule si `Target` est vide, se décale pendant les dérapages et tremble lors des chocs et des boosts.
- Le crash compte les voitures détruites et secoue la caméra.

### Corrections
- La touche de reset (`resetKey`) était ignorée (R codé en dur) : corrigé.
- Le champ de vision dynamique ne marchait pas si `Target Rigidbody` était vide : corrigé.
- `ExplosionFramePlayer` bloquait le jeu si l'explosion n'avait aucune image : corrigé.
- `NavMeshAutoBaker` n'écrit plus une ligne par route dans la console (option `verboseLogs`).
- `BusStopZone` ne recrée plus un matériau à chaque changement de couleur, et l'arrêt devient gris une fois vide.
- `BusStopPassenger` ne plante plus si le bus disparaît, et le délai avant de courir est réglable.

## Organisation

```
V1/
  V1.unity              Scène complète
  Prefabs/              Copies de PoliceCar, Man, Zone de livraison, Explosion (branchées sur les scripts V1)
  Resources/Gerant/     Images de la boîte de dialogue du gérant
  Scripts/
    Bus/                BusController (dérapage, boost, manette), BusHorn, BusDriftEffects,
                        BusCameraFollow, BusCarCrash, BusPassengers
    Traffic/            TrafficCarNavMesh (réaction au klaxon), TrafficRoad, TrafficIntersection
    Police/             PoliceChaseManager, PoliceCarAI, PoliceCatchZone, PoliceGameOverManager, PoliceSiren
    Passengers/         BusStopPassenger, BusStopZone
    Map/                NavMeshAutoBaker
    Effects/            ExplosionFramePlayer, ProceduralAudio (klaxon, crissement, boost)
    Delivery/           Livraisons Crousty + HUD (CroustyHUD, BusHUD, WaypointHUD, GerantDialogue...)
```

Tous les scripts sont dans le **namespace `V1`** : c'est ce qui permet d'avoir deux classes `BusController` (l'originale et celle de V1) dans le même projet. Les noms des champs sont les mêmes qu'à l'origine, donc les réglages des scènes sont conservés.

`BusController` ajoute tout seul `BusHorn` et `BusDriftEffects` au bus, `PoliceCarAI` ajoute `PoliceSiren`, et `CroustyHUD` ajoute `WaypointHUD` et `BusHUD` : il n'y a rien à installer à la main.

## À savoir

- La scène V1 n'est pas dans les Build Settings (pour ne pas modifier un fichier partagé). Pour un build, l'ajouter dans *File > Build Profiles*.
- Pour un build, ajouter aussi `Sprites/Default` et `Universal Render Pipeline/Unlit` dans *Project Settings > Graphics > Always Included Shaders* (faisceaux, traces de pneus, fumée).
- Les réglages du dérapage (vitesse minimale, adhérence, durées de charge, bonus de vitesse) sont dans l'Inspector du `BusController`, section « Dérapage ».
