# Architecture

## Décision : C# + WPF (.NET LTS)

Choisi le 2026-11-01. Machine cible : Windows 11, RTX 3060, i5-10400F, disque C: presque plein (≈105 Go libres), donc on évite les chaînes d'outils lourdes.

| Option | Pour | Contre | Verdict |
|---|---|---|---|
| **C# + WPF** | LibreHardwareMonitor pour les températures ; overlay transparent simple ; WMI et ETW natifs ; SDK ≈ 1 Go | Moins familier qu'une stack web | **Retenue** |
| Tauri (Rust + web) | UI web, binaire léger | Rust + Build Tools VS = 6 à 10 Go ; capteurs à brancher à la main | Écartée |
| Electron | Node déjà installé, UI web | Lourd en RAM, contradictoire pour un outil de monitoring ; FPS via PresentMon externe | Écartée |

État de l'environnement au moment du choix : Node 24 et Python 3.13 présents ; seuls les runtimes .NET 8 (Core + Desktop) sont installés, **pas de SDK**. SDK cible : .NET 10 (LTS), paquet winget `Microsoft.DotNet.SDK.10`.

## Briques techniques

- **Specs** : WMI via `System.Management` (`Win32_Processor`, `Win32_VideoController`, `Win32_PhysicalMemory`, `MSFT_PhysicalDisk`...). Mémoire vidéo lue dans le registre (WMI plafonne `AdapterRAM` à 4 Go). Chaque section échoue indépendamment dans une liste d'avertissements.
- **Capteurs** : LibreHardwareMonitor (températures, charges, ventilos). NVML pour les finesses NVIDIA si besoin.
- **FPS** : ETW (providers DXGI/D3D9 Present, comme PresentMon). Demande les droits admin, sans injection.
- **Scan disque** : parcours récursif parallèle (`DiskScanner`), taille réelle des fichiers creux/compressés, voir [storage-scan.md](storage-scan.md). Lecture directe de la MFT (façon WizTree) en amélioration ultérieure (droits admin).
- **Treemap** : algorithme squarified dans `Treemap.Core` (pur, testé), rendu par un contrôle WPF maison (`TreemapControl`, `DrawingContext`), 600 cellules maximum, petits fichiers regroupés.
- **Interface** : WPF avec le thème Fluent sombre intégré (`ThemeMode="Dark"`, encore marqué expérimental), MVVM minimal sans bibliothèque externe, palette neutre avec un seul accent (`Themes/Palette.xaml`).
- **Overlay** : fenêtre transparente, click-through, toujours au-dessus. Fonctionne en jeu fenêtré/borderless ; le plein écran exclusif est hors périmètre.

## Structure du repo

```
treemap/
  Treemap.slnx
  docs/            documentation (ici)
  src/
    Treemap.Core/      calcul, sans interface et testable
      Formatting/        tailles lisibles
      Layout/            treemap squarified
      Storage/           scanner, arbre, détecteur de suspects
      Specs/             lecture WMI, résumé texte
    Treemap.App/       interface WPF, référence Core
      Controls/          TreemapControl
      ViewModels/        Main, Components, Storage
      Views/             ComponentsView, StorageView
      Themes/            palette
    Treemap.Overlay/   fenêtre overlay                                    [à venir]
  tests/
    Treemap.Core.Tests/   xUnit, référence Core
  .claude/         skills, agents et commandes Claude (voir tooling.md)
```

Cible : `net10.0-windows` partout (Core utilise WMI, le registre et l'API Win32, donc Windows seulement). Commandes : `dotnet build Treemap.slnx`, `dotnet test Treemap.slnx`, voir [usage.md](usage.md).

## Workflow git

Petits commits directement sur `main`, sans pull request.

## Questions ouvertes

- Fenêtre classique ou icône dans la barre système + panneau ?
- GPU de la machine (NVIDIA / AMD / intégré) et jeux visés (anti-cheat ou non) ?
