# Branche `dev-jojo` — livraisons, points et HUD de Jojo

Ce document résume les commits de la branche `dev-jojo` (auteur : Jordan / Jojo) et la façon dont ils ont été fusionnés avec `main` le 11 octobre 2026. Il s'adresse à l'équipe et à une IA qui doit comprendre l'état du code avant de travailler dessus.

Tous les changements sont dans `Assets/Jojo`. Aucun fichier hors de ce dossier n'a été modifié par la branche, à part ce document.

## En bref

| Sujet | Avant (`main`) | Après la fusion |
| --- | --- | --- |
| Livraison | Touche E près d'une adresse | **Automatique** dès que le bus entre dans le rayon d'une adresse |
| Commandes par tournée | `ordersPerRun` fixe (6 dans SceneBase, 10 dans SceneJojo) | **3, puis 5, 10, 15, 20…** (`firstRunOrders`, `ordersStep`) |
| Points | Aucun | `DeliveryManager.Score` (+`pointsPerDelivery` par livraison), **pas encore affiché** |
| Faisceau du magasin | Toujours visible | **Visible seulement quand il faut aller au magasin** |
| HUD en jeu | `CroustyHUD` de Thibaud | **Inchangé** : `CroustyHUD` de Thibaud |
| Mini map | `BusMinimap` de Thibaud | **Inchangé** : `BusMinimap` (la mini map de Jojo a été supprimée) |
| HUD de Jojo (maquette) | — | Gardé dans `CroustyHUDJojo.cs` + `GerantDialogue.cs`, **désactivé** |

## Historique des commits

| Commit | Message | Contenu |
| --- | --- | --- |
| `3b4a7ef` | feat: boite de dialogue du gerant + livraison auto | `GerantDialogue.cs` : boîte de dialogue du gérant (portrait, étiquette, texte machine à écrire), images dans `Resources/Gerant`. Option de livraison automatique. |
| `92779bc` | fix: command crousty | Tournées 3 → 5 → 10 → 15…, suppression de la touche E, suppression des repères à l'écran du HUD de l'époque. |
| `0137caf` | add carte | Mini map ronde (`MiniMap.cs`) : caméra orthographique au-dessus du bus, rendue dans une RenderTexture. |
| `866abb5` | fix map | Mini map déplacée en bas à droite. |
| `d526a7f` | add point minimap | Pastilles des objectifs sur la mini map, collées au bord quand ils sont loin. |
| `d40d355` | fix faisceau magasin crousty | Propriété `NeedsPickup`, le faisceau du magasin suit cette propriété. |
| `514dfe3` | HUD complete | HUD de la maquette (argent/points, commandes, objectif) + système de points. |
| *(merge)* | Merge main dans dev-jojo | Fusion décrite ci-dessous. |

## Changements de code encore actifs

### `DeliveryManager.cs`

- **Supprimés** : `ordersPerRun`, `autoDeliver`, `deliverKey`, `PointInRange`, la dépendance à `UnityEngine.InputSystem`. Les valeurs encore enregistrées dans les scènes pour ces champs sont ignorées par Unity et disparaîtront à la prochaine sauvegarde des scènes.
- **Livraison automatique** : dans `Update`, l'adresse en attente la plus proche dans le rayon reçoit son crousty.
- **Tournées** : `firstRunOrders = 3`, `ordersStep = 5`. Tournée n° 0 → 3, puis `ordersStep * n` → 5, 10, 15… Le nombre réel est limité au nombre d'adresses (`NextRunOrders`), avec un avertissement dans la console.
- **Points** : `pointsPerDelivery = 100`, ajoutés à `Score` quand le crousty atterrit.
- **Nouvelles propriétés publiques** :
  - `NeedsPickup` : `!HasStarted || AllDelivered` (il faut aller au magasin) ;
  - `NextRunOrders` : nombre de commandes de la prochaine tournée ;
  - `Score`.
- **Conservé de `main`** : l'événement `DeliveryCompleted`, utilisé par `BusGPS`. Il est appelé juste après l'ajout des points.

### `CroustyShop.cs`

- `Update` active le faisceau (`marker`) seulement si `DeliveryManager.Instance.NeedsPickup`.
- Conservé de `main` : `maxHeightDifference` et `screenLabel` (utilisé par les repères de `CroustyHUD`).

### `DeliveryPoint.cs`

Pas modifié par la branche. La limite de hauteur de `main` est conservée.

## Décisions prises pendant la fusion

1. **Mini map** : on garde `BusMinimap` (Thibaud), déjà branchée au GPS et à la police. `MiniMap.cs` (Jojo) est supprimé.
2. **HUD** : on garde `CroustyHUD` (Thibaud), qui suit la règle de `Docs/UI-panels.md` (interface enregistrée dans les scènes, `PrepareUI` côté éditeur). Le HUD de Jojo est conservé comme référence, désactivé :
   - `Assets/Jojo/Scripts/CroustyHUDJojo.cs` (classe `CroustyHUDJojo`) : argent/points, commandes `livrées/total`, panneau objectif, création de toute l'UI par le code au lancement ;
   - `Assets/Jojo/Scripts/GerantDialogue.cs` : boîte de dialogue du gérant, utilisée par ce HUD.

   Les deux fichiers sont entourés de `#if CROUSTY_HUD_JOJO … #endif` : ils ne sont pas compilés. Pour les tester, ajouter `CROUSTY_HUD_JOJO` dans *Project Settings > Player > Scripting Define Symbols*, puis ajouter le composant `CroustyHUDJojo` sur un objet de la scène. Ils ne sont branchés à aucune scène.
3. **Touche E** : `Docs/INTEGRATION_SCENE_BASE.md` indique que la touche E est conservée dans SceneBase. Ce n'est plus le cas : la livraison est automatique partout.

## Ce qui reste à faire / à vérifier

- **Tester en Play dans SceneBase et SceneJojo.** La fusion n'a pas été compilée ni testée dans Unity : seule une vérification des références par recherche dans le code a été faite.
- **Afficher les points** : `Score` existe mais aucun HUD actif ne l'affiche. Le panneau « argent » de la maquette est dans `CroustyHUDJojo.cs`, à reporter dans le HUD de la scène.
- **Panneaux de la maquette à reporter dans `CroustyHUD`** (argent, commandes `livrées/total`, objectif, gérant), en suivant la règle des panels enregistrés dans la scène. Positions de la maquette 1920×1080 dans `CroustyHUDJojo.BuildUI`.
- **Nombre d'adresses** : SceneBase a 6 adresses. Les tournées de 10 et plus sont limitées à 6. Il faut ajouter des `DeliveryPoint` (au moins 15–20).
- **Repères à l'écran** : `CroustyHUD` affiche encore les repères ronds verts à l'écran. Jojo les avait retirés de son HUD, car la mini map indique déjà les objectifs. À rediscuter.
- **Icônes de la maquette** : `CroustyHUDJojo` attend `Assets/Jojo/Resources/HUD/dollar.png`, `sac.png` et `epingle.png`, pas encore ajoutées.
