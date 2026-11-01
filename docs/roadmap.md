# Roadmap

Ordre proposé. Chaque étape doit être utilisable seule.

## 0. Cadrage
- [x] Nom du projet : `treemap`
- [x] Dossier `docs/` et outillage Claude (`.claude/`)
- [x] Choix de la stack : C# + WPF (voir architecture.md)
- [x] SDK .NET 10 installé, solution `Treemap.slnx` créée (Core, App WPF, tests xUnit), build OK
- [ ] Workflow : petits commits directement sur `main`, pas de PR
- [ ] Initialisation du repo git et premier push

## 1. Stockage (MVP)
- [ ] Scan d'un dossier / d'un disque, avec progression
- [ ] Liste des plus gros dossiers et fichiers
- [ ] Vue treemap navigable (clic pour descendre)
- [ ] Repérage des suspects connus (hiberfil, pagefile, WinSxS, vhdx, node_modules, caches)
- [ ] Export / "ouvrir dans l'explorateur", sans suppression pour l'instant

## 2. Specs
- [ ] Page récapitulative CPU / GPU / RAM / disques / Windows / BIOS
- [ ] Copie en un clic (pour support ou revente)

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
