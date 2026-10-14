# Moteur et sirènes

## Musique de poursuite

`Chase in the Arcade.mp3`, fourni par l’utilisateur, est copié tel quel dans Assets/SoundEffect (fichier original inchangé, stéréo et normalisation automatique désactivée). SoundBank possède une entrée ChaseMusic à 0,55, pilotée par le réglage Musique.

Avec ce morceau assigné, une nouvelle poursuite joue d’abord 1,4 seconde de la sirène deux tons, une seule fois, puis lance la musique depuis le début, sans fondu d’entrée. La piste boucle pendant toute la poursuite, indépendamment de la distance des voitures. Les sirènes continues des véhicules se taisent pendant cette séquence pour éviter une superposition avec la musique. Sans morceau assigné, les sirènes spatiales précédentes restent disponibles.

À la fin de la poursuite, le morceau baisse linéairement pendant 1,2 seconde puis s’arrête. Une nouvelle poursuite rejoue la séquence depuis le début. Une sortie durant l’avertissement annule le lancement de la musique. Pause : lecture, délai d’introduction et fondu sont suspendus. Défaite et changement de scène : arrêt et remise à zéro immédiats.

L’enveloppe de poursuite réduit l’ambiance à 15 % et les effets de conduite/ponctuels à 65 % de leurs niveaux habituels. Ces niveaux reviennent progressivement pendant le fondu de sortie. Les effets UI gardent leur niveau ; le réglage Effets continue de contrôler le moteur, les effets et le signal de sirène. Les gains sont appliqués au mixage sans modifier le fichier musical.

Validation dédiée : `Tools/Validation/ChaseMusicChecks.cs`, à copier dans Assets d’un projet isolé, puis exécuter `ChaseMusicChecks.Run` sans `-quit`.

Le gestionnaire sonore s’installe automatiquement au lancement. Les changements s’appliquent à SceneBase et aux autres scènes qui utilisent BusController et PoliceChaseManager, sans migration du Canvas ou de la scène.

Le moteur utilise une boucle issue d’un enregistrement réel de voiture, nettoyée et assombrie pour le bus. Le régime, le volume et l’ouverture du filtre suivent l’effort : accélérateur progressif de BusWheelRig, accélérateur de BusController en secours, vitesse signée, freinage, marche arrière, boost PET et vol. Cinq rapports simulés abaissent le régime aux passages de vitesse ; une marge de rétrogradage évite les oscillations. Le son de ralenti reste discret. L’ancien effet ponctuel Acceleration_fart ne se déclenche plus à chaque accélération ; le pouvoir PET garde ses effets.

Chaque voiture de police possède une source mono 3D sur son toit. La voiture poursuivante la plus proche de l’AudioListener porte la sirène dominante ; les barrages actifs peuvent aussi la porter. La sélection conserve une marge de distance avant de changer de voiture, puis effectue un fondu de 0,3 seconde. Les deux voix temporaires du fondu partagent un seul budget de volume et la même phase de boucle. Le nombre de policiers n’augmente donc pas le volume global des sirènes.

La nouvelle sirène est une composition originale à deux tons arrondis (495/660 Hz), avec transitions douces et harmoniques limitées. Elle remplace la sirène précédente dans SoundBank, à un niveau nominal de 0,18 au lieu de 0,35. L’atténuation est calculée contre l’AudioListener : volume de proximité jusqu’à 8 m, environ 28 % à 75 m et silence à 150 m. Aucun son de sirène en patrouille normale, après destruction ou hors poursuite. Le Doppler est désactivé pour conserver une transition stable entre les voitures.

Les sons moteur et sirènes s’arrêtent pendant la pause ou une défaite et sont nettoyés au changement de scène. Ils suivent le réglage Effets, tandis que l’ambiance existante conserve son réglage Musique. Les niveaux, les limites de pitch, les distances et la durée de fondu sont réglables dans `Assets/Resources/Crousty/SoundBank.asset`.

## Sources audio

- `VehicleEngineLoop.wav` : adaptation de **loop_0.wav**, par **domasx2**, [racing car engine sound loops](https://opengameart.org/content/racing-car-engine-sound-loops), sous [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). La page source indique un enregistrement de voiture du domaine public, découpé en boucle. Modifications dans ce projet : suppression de la composante continue, raccord de boucle, égalisation et niveau de crête à 0,75.
- `PoliceTwoToneLoop.wav` : création originale pour ce projet, générée avec `Tools/Validation/author_vehicle_audio.py`. Aucun extrait tiers.

Le script d’auteur prend en entrée le fichier original téléchargé depuis la page ci-dessus et produit les deux WAV mono 44,1 kHz. Les WAV sont importés en mémoire, préchargés et sans normalisation Unity supplémentaire. Les anciennes ressources audio restent disponibles dans le projet.

## Validation

`Tools/Validation/VehicleAudioChecks.cs` se copie dans Assets d’un projet isolé complet et se lance avec `VehicleAudioChecks.Run`. Sous Unity 6000.4.0f1 : **25 contrôles réussis, zéro échec**. Ils couvrent l’import des clips, le ralenti, la charge, les rapports, le freinage, la marche arrière, la pause, les effets, le retour au menu, les sources sur les voitures, six policiers proches, la marge de sélection, le plafond durant un fondu, les distances, la patrouille, les barrages et la destruction.

Validation musique sous Unity 6000.4.0f1 : 14 contrôles réussis, zéro échec. Mesure du fichier décodé : durée 148,824 s, crête 0,939, RMS 0,147 ; au gain musical 0,55, la crête propre à cette piste reste autour de 0,516 avant volume général. Fichier source et copie intégrée ont le même SHA-256.
