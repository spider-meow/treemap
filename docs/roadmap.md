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

## 3. Monitoring live : V1 livrée
- [x] Charge CPU/GPU, RAM, VRAM, disque, réseau, avec historique de deux minutes et graphes
- [x] Température, puissance, fréquence et ventilateur du GPU NVIDIA
- [ ] Température du processeur (demande un pilote noyau : décision à prendre, voir monitoring.md)
- [ ] Cartes GPU AMD et Intel
- [ ] Historique plus long, export

## 4. Overlay : V1 livrée
- [x] Fenêtre transparente, au premier plan, click-through, sans prise de focus
- [x] Lignes au choix, coin de l'écran, opacité, réglages sauvegardés
- [x] Raccourci global Ctrl+Alt+F10
- [x] FPS via événements ETW (DirectX 9 à 12), relance en administrateur proposée
- [ ] **Valider la capture FPS réelle en administrateur avec un jeu** (non testé, voir monitoring.md)
- [ ] FPS pour Vulkan et OpenGL
- [ ] Temps de frame et 1 % bas
- [ ] Raccourci personnalisable
- [ ] Icône dans la zone de notification pour garder l'overlay actif fenêtre fermée

## 5. Optimisation
- [ ] Programmes au démarrage (lecture, puis activation/désactivation avec confirmation)
- [ ] Processus gourmands
- [ ] Nettoyage assisté (toujours avec aperçu et confirmation)
