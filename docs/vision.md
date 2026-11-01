# Vision

## Pourquoi

Le disque C: est plein (≈105 Go libres sur 930 Go) sans qu'on comprenne pourquoi. `treemap` est né de là : un outil perso et léger pour **comprendre** son PC, pas un "optimiseur magique".

Le nom vient de la vue treemap (blocs proportionnels à la taille), qui est le cœur du module stockage.

## Modules

1. **Stockage** (priorité 1) : scan du disque, vue treemap, liste des plus gros fichiers/dossiers, détection des suspects habituels (hiberfil.sys, pagefile, WinSxS, caches Windows Update, `.vhdx` WSL/Docker, `node_modules`, jeux, OneDrive).
2. **Specs** : CPU, GPU, RAM (type, vitesse), carte mère, disques, version de Windows, BIOS.
3. **Monitoring live** : charge, températures, RAM, disque, réseau, avec graphes.
4. **Overlay** : petite fenêtre transparente toujours au-dessus (FPS, températures, charge).
5. **Optimisation** : programmes au démarrage, processus gourmands, suggestions de nettoyage.

## Principes

- **Lecture seule d'abord.** L'appli observe et explique. Toute suppression ou modification passe par une confirmation explicite, jamais automatique.
- **Perso et léger.** Pas de télémétrie, pas de compte, pas de service en arrière-plan inutile.
- **Pas d'injection dans les jeux.** Le FPS se lit via les événements ETW de présentation (approche PresentMon), pas en injectant une DLL (risque anti-cheat).
- **Honnêteté sur l'existant.** WizTree, TreeSize, HWiNFO et RTSS font déjà très bien ces métiers. L'intérêt ici : un outil sur mesure, et apprendre.

## Hors périmètre (pour l'instant)

- Overclocking, réglages BIOS, modification du registre ou des paramètres de sécurité Windows.
- Nettoyeurs "en un clic" qui suppriment sans montrer ce qu'ils suppriment.
- Support macOS/Linux.
