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

- **Specs** : WMI / CIM (`Win32_Processor`, `Win32_VideoController`, `Win32_PhysicalMemory`, `Win32_DiskDrive`...).
- **Capteurs** : LibreHardwareMonitor (températures, charges, ventilos). NVML pour les finesses NVIDIA si besoin.
- **FPS** : ETW (providers DXGI/D3D9 Present, comme PresentMon). Demande les droits admin, sans injection.
- **Scan disque** : parcours récursif parallèle au départ. Lecture directe de la MFT (façon WizTree) en amélioration ultérieure (droits admin).
- **Treemap** : algorithme squarified, rendu en canvas/DrawingVisual, agrégation des petits fichiers pour garder la fluidité.
- **Overlay** : fenêtre transparente, click-through, toujours au-dessus. Fonctionne en jeu fenêtré/borderless ; le plein écran exclusif est hors périmètre.

## Structure du repo

```
treemap/
  Treemap.slnx
  docs/            documentation (ici)
  src/
    Treemap.Core/      scan disque, specs, capteurs (sans UI, testable)   [créé]
    Treemap.App/       interface WPF, référence Core                      [créé]
    Treemap.Overlay/   fenêtre overlay                                    [à venir]
  tests/
    Treemap.Core.Tests/   xUnit, référence Core                           [créé]
  .claude/         skills, agents et commandes Claude (voir tooling.md)
```

Cible : `net10.0` (Core, tests) et `net10.0-windows` (App, WPF). Commandes : `dotnet build Treemap.slnx`, `dotnet test Treemap.slnx`.

## Workflow git

Petits commits directement sur `main`, sans pull request.

## Questions ouvertes

- Fenêtre classique ou icône dans la barre système + panneau ?
- GPU de la machine (NVIDIA / AMD / intégré) et jeux visés (anti-cheat ou non) ?
