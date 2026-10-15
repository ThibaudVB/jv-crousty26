# Mode GOD

Saisir exactement `CathyLaVodka` dans le menu. La comparaison est ordinale et sensible à la casse. Les préfixes, suffixes, variantes de casse et caractères ressemblants ne donnent aucun privilège. Les espaces ou caractères de contrôle que la normalisation transformerait en ce nom sont refusés avant le lancement.

Ce profil ignore les dégâts et la capture policière. Les policiers poursuivent normalement le bus. Tous les pouvoirs sont débloqués pour les boîtes, avec les limites habituelles de charges et d’inventaire. Le délai des livraisons reste applicable.

Les déblocages GOD ne sont pas inscrits dans la progression : changer de nom retire immédiatement les privilèges. Ce mécanisme est un code de triche local fondé sur le nom, pas une authentification de compte administrateur.

Le classement refuse les nouveaux scores de `CathyLaVodka` et retire ses anciens scores au chargement et à l’affichage, y compris le dernier résultat affiché. Le filtrage précède le regroupement des records par nom : `cathylavodka`, `CATHYLAVODKA` et les noms avec préfixe/suffixe restent éligibles. Le prochain enregistrement légitime nettoie aussi les anciennes données persistées. `Tools/Validation/GodLeaderboardChecks.ps1` vérifie ces règles avec les véritables sources C# et un stockage Unity simulé en mémoire ; ces contrôles passent.

`FlightDurationChecks.Run` couvre le nom exact, les variantes refusées, le changement de profil, les dégâts, la capture et la conservation de l’élan pendant une seconde de simulation après expiration et arrêt manuel. Compilation du jeu et des contrôles réussie le 15 octobre 2026 ; exécution Unity bloquée au démarrage (code de sortie 1), donc simulation et ressenti en jeu non validés lors de cette modification.
