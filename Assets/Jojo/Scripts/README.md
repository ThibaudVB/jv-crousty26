# Scripts Jojo — Système de livraison Crousty

Vue d'ensemble des scripts du dossier, ce que fait chaque fonction, et les points encore à surveiller.
Tous les scripts sont dans le namespace `Jojo`.

## Sommaire

1. [Fonctionnement global](#1-fonctionnement-global)
2. [DeliveryManager.cs](#2-deliverymanagercs)
3. [BeaconZone.cs, DeliveryPoint.cs, CroustyShop.cs](#3-beaconzonecs-deliverypointcs-croustyshopcs)
4. [CroustyHUD.cs, WaypointHUD.cs](#4-croustyhudcs-waypointhudcs)
5. [GerantDialogue.cs, GerantLines.cs](#5-gerantdialoguecs-gerantlinescs)
6. [JojoVisuals.cs, BeaconPulse.cs](#6-jojovisualscs-beaconpulsecs)
7. [Points encore à surveiller](#7-points-encore-à-surveiller)

---

## 1. Fonctionnement global

```
          ┌──────────────┐  entre dans le rayon   ┌──────────────────┐
  Bus ───►│ CroustyShop  │ ─────────────────────► │ DeliveryManager  │
 (joueur) └──────────────┘   (charge N commandes) │  (cerveau)       │
                                                  └───────┬──────────┘
                     tire N adresses au hasard            │  événement Changed
                     ┌────────────────────────────────────┤
                     ▼                                    ▼
              ┌──────────────┐                    ┌──────────────┐
              │ DeliveryPoint│                    │  CroustyHUD  │── compteur
              └──────────────┘                    └──────┬───────┘
                                                         ├── GerantDialogue (+ GerantLines)
                                                         └── WaypointHUD (repères)

  BeaconZone  : base commune de CroustyShop et DeliveryPoint (rayon + faisceau)
  JojoVisuals : outils statiques (faisceaux, matériaux, sprites, textes UI)
  BeaconPulse : anime la largeur du halo des faisceaux
```

**Boucle de jeu :**
1. Le joueur (bus) va au magasin Crousty → les commandes sont chargées automatiquement.
2. `ordersPerRun` adresses sont tirées au hasard et affichent un faisceau vert.
3. En passant près d'une adresse, un crousty est lancé (automatiquement, ou avec la touche si `autoDeliver` est décoché) et atterrit en arc.
4. Quand tout est livré, le joueur retourne au magasin pour une nouvelle tournée.

**Mise en place dans la scène :**
| Objet | Script |
|---|---|
| GameObject vide « DeliveryManager » | `DeliveryManager` |
| Le magasin | `CroustyShop` |
| Chaque adresse de livraison | `DeliveryPoint` |
| GameObject vide « HUD » | `CroustyHUD` (ajoute `GerantDialogue` et `WaypointHUD` tout seul) |
| Le bus | doit avoir un `BusController` (script hors de ce dossier) |

---

## 2. DeliveryManager.cs

**Rôle :** le cerveau du système. Charge les commandes au magasin, détecte l'adresse la plus proche, lance les croustys, tient les compteurs. Singleton : `DeliveryManager.Instance`.

Les réglages sont privés (`[SerializeField]`) : on les change dans l'Inspector, et les autres scripts les lisent avec des propriétés en lecture seule (`Player`, `Shop`, `Points`, `AutoDeliver`, `DeliverKey`).

| Champ | Défaut | Rôle |
|---|---|---|
| `ordersPerRun` | 10 | Nombre de commandes par tournée (min 1) |
| `player` | auto | Transform du joueur. Si vide → cherche le `BusController` |
| `shop` | auto | Le `CroustyShop`. Si vide → cherché dans la scène |
| `deliveryPoints` | auto | Liste des adresses. Si vide → tous les `DeliveryPoint` **actifs** de la scène |
| `autoDeliver` | true | Livre tout seul en passant. Décoché → il faut appuyer sur `deliverKey` |
| `deliverKey` | E | Touche de livraison |
| `throwDuration` / `throwArcHeight` / `throwStartHeight` | 0.8 / 4 / 2.5 | Vol du crousty |
| `croustyProjectilePrefab` | vide | Objet lancé. Si vide → cube orange |

| Propriété / événement | Signification |
|---|---|
| `CroustyLeft` | Croustys encore dans le bus |
| `OrdersTotal` | Nombre de commandes de la tournée |
| `HasStarted` | `true` dès la première tournée chargée |
| `PointInRange` | Adresse livrable la plus proche cette frame (ou `null`) |
| `AllDelivered` | Tournée commencée + plus rien dans le bus + plus rien en vol |
| `Changed` | Événement déclenché quand les compteurs changent (chargement, lancer, arrivée) |

| Fonction | Ce qu'elle fait |
|---|---|
| `Awake()` | Enregistre le singleton (warning s'il y en a déjà un), remplit `player`, `shop`, `deliveryPoints` s'ils sont vides. |
| `OnDisable()` | Arrête les lancers en cours, détruit les projectiles et termine les livraisons en attente, pour que la tournée ne reste jamais bloquée. |
| `Update()` | Recharge au magasin si possible, puis cherche une adresse livrable et lance un crousty. |
| `LoadOrders()` | Retire les cases vides de la liste, désactive toutes les adresses, en tire `min(ordersPerRun, nb d'adresses)` au hasard (Fisher-Yates partiel). Le warning « aucun DeliveryPoint » ne s'affiche qu'une fois. |
| `FindDeliverablePoint()` | Adresse en attente, dans le rayon, la plus proche (distance à plat). |
| `ThrowCrousty()` / `ThrowRoutine()` | Lance le projectile en cloche vers `DropPosition`, puis valide la livraison (même si l'adresse a été détruite entre-temps). |
| `CreateProjectile()` | Instancie le prefab (colliders désactivés) ou crée un cube orange. |

---

## 3. BeaconZone.cs, DeliveryPoint.cs, CroustyShop.cs

**`BeaconZone`** (classe de base abstraite) : tout ce qui est commun au magasin et aux adresses.

| Champ / fonction | Rôle |
|---|---|
| `radius` | Rayon de la zone, mesuré à plat (anciennement `pickupRadius` / `deliveryRadius`, les valeurs de la scène sont conservées grâce à `[FormerlySerializedAs]`) |
| `marker`, `markerColor`, `beamHeight`, `beamWidth` | Marqueur. Si `marker` est vide → faisceau lumineux. Si c'est un prefab du dossier Project → une copie est placée dans la scène |
| `Awake()` | Crée ou instancie le marqueur |
| `IsInRange(pos)` / `DistanceTo(pos)` | Distance à plat (hauteur ignorée) |
| `OnDrawGizmos()` | Cercle à plat de la couleur du marqueur |

**`DeliveryPoint`** : une adresse. Ajoute `dropTarget` (point d'atterrissage) et une machine à états :

```
Inactive ──SetOrderActive(true)──► Waiting ──MarkPending()──► Pending ──CompleteDelivery()──► Delivered
   ▲                                                                                              │
   └────────────────────── SetOrderActive(false) (au rechargement suivant) ──────────────────────┘
```
Le marqueur n'est visible **que** dans l'état `Waiting`. `IsWaiting` / `IsPending` donnent l'état.

**`CroustyShop`** : le magasin. Ajoute `screenLabel` (texte du repère HUD). Son faisceau reste toujours visible.

Les deux utilisent `Reset()` pour leurs valeurs par défaut (rayon 10 vert pour une adresse, rayon 8 jaune pour le magasin) quand on ajoute le script dans l'éditeur.

---

## 4. CroustyHUD.cs, WaypointHUD.cs

**`CroustyHUD`** : crée le Canvas (Screen Space Overlay, 1920×1080, ordre 100), le compteur en haut à gauche, puis ajoute `GerantDialogue` (si `messageText` est vide) et `WaypointHUD`.
- Le compteur n'est mis à jour que sur l'événement `DeliveryManager.Changed`.
- Le message du gérant est demandé chaque frame à `GerantLines` (la boîte ignore un message identique).

**`WaypointHUD`** : repères à l'écran (rond de couleur + distance, flèche collée au bord si la cible est hors écran).

| Champ | Défaut | Rôle |
|---|---|---|
| `showWaypoints` | true | Afficher les repères |
| `waypointSize` | 44 | Taille des ronds |
| `screenEdgeMargin` | 60 | Marge avec le bord de l'écran |
| `heightOffset` | 4 | Le repère vise 4 m au-dessus de la cible |
| `labelFontSize` | 22 | Taille du texte de distance |

Les repères sont réutilisés (pool). Le texte de distance n'est reconstruit que quand il change.

---

## 5. GerantDialogue.cs, GerantLines.cs

**`GerantDialogue`** : boîte de dialogue en bas à gauche (portrait, étiquette « GÉRANT », bandeau papier, texte qui s'écrit lettre par lettre, petit triangle qui sautille quand le texte est complet). Images chargées depuis `Resources/Gerant` si les champs sont vides. Option `bold` pour le texte en gras.

**`GerantLines`** : classe statique qui choisit la réplique selon l'état de la tournée (aller au magasin → tout livré → « Appuie sur E » si pas auto → nombre de commandes restantes).

---

## 6. JojoVisuals.cs, BeaconPulse.cs

**`JojoVisuals`** (statique) :

| Fonction | Ce qu'elle fait |
|---|---|
| `SetColor(obj, color)` | Matériau unlit opaque, **partagé** par couleur (pas de nouveau matériau à chaque projectile). |
| `CreateBeacon(...)` | Faisceau : cœur fin + halo qui pulse + Point Light au sol. |
| `CreateUnlitMaterial(...)` | Shader `URP/Unlit`, sinon `Unlit/Color`, sinon `Sprites/Default` ; erreur claire si aucun n'existe. |
| `DefaultFont` | Police par défaut de Unity, chargée une fois. |
| `CreateOutlinedText(...)` | Texte UI blanc, gras, contour noir (utilisé par le HUD et les repères). |
| `CircleSprite` / `ArrowSprite` | Sprites générés une fois puis gardés en cache. |

**`BeaconPulse`** : fait « respirer » un objet en X et Z avec un sinus (`speed` 2.5, `amount` 0.25).

---

## 7. Points encore à surveiller

1. **Shaders dans un build** : ajouter `Universal Render Pipeline/Unlit` dans *Project Settings > Graphics > Always Included Shaders*, sinon les faisceaux peuvent être roses dans le jeu exporté.
2. **Trop de lumières** : une Point Light par faisceau (10 adresses + magasin). URP en gère 8 par objet par défaut → certaines peuvent clignoter.
3. **Fallback non-URP** : `Unlit/Color` ne gère pas la transparence, les faisceaux seraient des colonnes pleines.
4. **Adresses désactivées** : la recherche automatique ne trouve que les `DeliveryPoint` actifs au démarrage.
