# treemap — documentation

Appli perso d'optimisation et de monitoring de PC (Windows 11). Repo : https://github.com/spider-meow/treemap

| Doc | Contenu |
|---|---|
| [vision.md](vision.md) | Ce que l'appli doit faire, périmètre, principes |
| [architecture.md](architecture.md) | Choix techniques et structure du code |
| [usage.md](usage.md) | Lancer l'appli, options, tests |
| [storage-scan.md](storage-scan.md) | Comment les tailles sont calculées (fichiers creux, écarts avec Windows) |
| [roadmap.md](roadmap.md) | Modules et ordre de construction |
| [tooling.md](tooling.md) | Skills / agents / commandes Claude installés dans `.claude/`, et ce qui a été volontairement laissé de côté |

## Statut

**V1 livrée** : un écran Composants (CPU, GPU, RAM, carte mère, disques, Windows, volumes) et un onglet Stockage (scan, arbre, treemap navigable, suspects connus). Pas encore de monitoring live, d'overlay ni d'optimisation : voir la roadmap.
