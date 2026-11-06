# Booster de jeu

Écran **Jeu** (`--tab=game`). Il prépare le PC quand une partie commence, remet tout en état quand elle finit, et **mesure** ce que ça a changé au lieu de le promettre. Décisions prises avec l'utilisateur : usage personnel, aucune injection dans les jeux, les « nettoyeurs de RAM » sont écartés (effet placebo).

## Ce qu'une session fait

Chaque action est un interrupteur (les trois premiers sont actifs par défaut, le mode automatique est éteint) :

1. **Priorité haute pour le jeu** (`PriorityClass.High`), sans droits administrateur.
2. **Plan d'alimentation Hautes performances** pendant la partie (`powercfg /setactive`), puis retour au plan précédent. Si le plan n'existe pas sur le PC ou est déjà actif, l'action est ignorée et le dit.
3. **Fermer les applications cochées** : l'utilisateur coche, dans la liste des applications ouvertes, celles à fermer. La fermeture est **polie** (`CloseMainWindow`) : une application avec du travail non enregistré le demande, **rien n'est jamais tué**. Celles qui refusent sont signalées. Une seule entrée par application, même avec plusieurs fenêtres.
4. **Mesure** : mémoire utilisée, nombre de programmes et charge processeur avant, puis 8 secondes après (le temps que Windows libère la mémoire), avec le signe de la différence.

À la fin de la partie (le jeu se ferme, ou « Terminer la session », ou fermeture de treemap) : le plan d'alimentation est remis **sauf si l'utilisateur en a choisi un autre entre-temps**. Les applications fermées restent fermées ; « Relancer les applications fermées » les redémarre (celles dont on connaît l'exécutable ; pas celles du Store, installées sous `WindowsApps`).

**Reprise après plantage** : avant de changer de plan, l'ancien est écrit dans les réglages (`PendingPowerPlan`). Si treemap s'arrête en pleine session, le démarrage suivant le remet, à condition que le PC soit encore sur Hautes performances.

## Ce qui n'est jamais touché

`ProcessRules` classe chaque processus : `Game`, `Closable`, `Protected`, `Self`.

- **Protégés, jamais listés ni fermés** : Windows et tout programme sous `C:\Windows`, la sécurité (Defender), les **lanceurs de jeux** (Steam, Epic, EA, Battle.net, Ubisoft, GOG, Riot, Xbox), les **anti-triche**, les outils de pilote et de périphériques utiles aux jeux (NVIDIA, AMD, Afterburner, RTSS, G HUB, iCUE, Synapse), treemap lui-même.
- Un programme **sans fenêtre visible** ou **sans chemin lisible** est laissé tranquille.
- Même si une liste enregistrée contenait un nom protégé, le moteur le retire (`Apply`) et ne le ferme pas (`CloseApps` revérifie le rôle).

## Détecter un jeu

- Un programme lancé depuis un dossier de jeux (`steamapps\common`, `Epic Games`, `XboxGames`, `GOG`, `Riot Games`, `EA Games`, `Rockstar Games`…) est un jeu, sauf une liste d'outils installés à côté (Wallpaper Engine, SteamVR, gestionnaires de plantage…).
- Les autres se déclarent à la main : « C'est un jeu » dans la liste des applications. Ils s'ajoutent aux « jeux ajoutés à la main », qu'on peut retirer.
- **Mode automatique** (éteint par défaut) : démarre une session tout seul pour un jeu **ouvert pendant que treemap tourne**. Les jeux déjà ouverts au démarrage de treemap passent par le bouton, pour ne pas surprendre.

## Limites connues

- Seules les applications **avec une fenêtre** sont fermables ; les programmes qui vivent uniquement dans la zone de notification n'apparaissent pas.
- Pas de relance des applications du Microsoft Store.
- Pas de fermeture forcée (choix délibéré) : une application qui refuse de se fermer reste ouverte.
- Le gain réel dépend du PC : sur 16 Go de RAM le plus net est la mémoire rendue ; sur un processeur peu chargé, la priorité change peu de chose. La mesure affichée est celle du PC, pas une promesse.
- Le plein écran exclusif et le FPS ne sont pas concernés ici (voir [monitoring.md](monitoring.md)).

## Messages du pont

`booster.get`, `booster.update`, `booster.start`, `booster.stop`, `booster.relaunch`, évènement `booster.changed`. La liste des applications se rafraîchit toutes les 4 secondes ; `BoosterService` (côté appli) surveille les jeux toutes les 3 secondes hors du fil d'interface.
