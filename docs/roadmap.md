# Roadmap

Ordre proposé. Chaque étape doit être utilisable seule.

## 0. Cadrage
- [x] Nom du projet : `treemap`
- [x] Dossier `docs/` et outillage Claude (`.claude/`)
- [x] Choix de la stack : C# + WPF (voir architecture.md)
- [x] SDK .NET 10 installé, solution `Treemap.slnx` créée (Core, App WPF, tests xUnit)
- [x] Repo git initialisé et poussé, workflow : petits commits sur `main`, un push par commit, pas de PR

## 1. Stockage : V1 livrée
- [x] Scan d'un dossier / d'un disque, avec progression et annulation
- [x] Arbre des dossiers trié par taille, avec part du dossier parent
- [x] Vue treemap navigable (clic pour descendre, "dossier parent" pour remonter)
- [x] Repérage des suspects connus (hiberfil, pagefile, WinSxS, disques virtuels, node_modules, jeux, corbeille, caches), avec explication et conseil
- [x] "Afficher dans l'Explorateur" (lecture seule, aucune suppression)
- [x] Taille réelle des fichiers creux et compressés (voir storage-scan.md)
- [ ] Relance en administrateur pour lire les dossiers système (aujourd'hui l'écart est seulement expliqué)
- [ ] Lecture directe de la MFT pour accélérer encore le scan
- [ ] Détection des doublons
- [ ] Défilement automatique de l'arbre vers l'élément sélectionné

## 2. Composants : V1 livrée
- [x] Écran récapitulatif CPU / GPU / RAM (modules) / carte mère / BIOS / disques / Windows
- [x] Occupation des volumes avec code couleur (orange dès 80 %, rouge dès 90 %)
- [x] Copie du résumé en texte en un clic
- [ ] Températures et ventilateurs (LibreHardwareMonitor)
- [ ] Santé SMART des disques

## 3. Monitoring live
- [ ] Charge CPU/GPU, RAM, disque, réseau
- [ ] Températures
- [ ] Historique court avec graphes

## 4. Overlay
- [ ] Fenêtre transparente click-through avec métriques choisies
- [ ] FPS via ETW
- [ ] Raccourci pour afficher/masquer, position et style configurables

## 5. Optimisation
- [ ] Programmes au démarrage (lecture, puis activation/désactivation avec confirmation)
- [ ] Processus gourmands
- [ ] Nettoyage assisté (toujours avec aperçu et confirmation)
