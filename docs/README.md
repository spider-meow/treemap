# treemap — documentation

Appli perso d'optimisation et de monitoring de PC (Windows 11). Repo : https://github.com/spider-meow/treemap

| Doc | Contenu |
|---|---|
| [vision.md](vision.md) | Ce que l'appli doit faire, périmètre, principes |
| [architecture.md](architecture.md) | Choix techniques et structure du code |
| [usage.md](usage.md) | Lancer l'appli, options, tests |
| [storage-scan.md](storage-scan.md) | Comment les tailles sont calculées (fichiers creux, écarts avec Windows) |
| [storage-cleanup.md](storage-cleanup.md) | Supprimer depuis l'écran Stockage : Corbeille, garde-fous, désinstalleurs, Flight Simulator |
| [monitoring.md](monitoring.md) | Mesures en direct, overlay en jeu, FPS : sources, limites, état de vérification |
| [roadmap.md](roadmap.md) | Modules et ordre de construction |
| [tooling.md](tooling.md) | Skills / agents / commandes Claude installés dans `.claude/`, et ce qui a été volontairement laissé de côté |

## Statut

**V1 livrée** : trois onglets, Composants (CPU, GPU, RAM, carte mère, disques, Windows, volumes), Stockage (scan, arbre, treemap navigable, suspects connus) et Moniteur (mesures en direct, overlay en jeu avec FPS). Pas encore d'optimisation (programmes au démarrage, nettoyage) : voir la roadmap. La capture FPS réelle reste à valider en administrateur.
