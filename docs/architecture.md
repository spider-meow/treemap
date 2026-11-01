# Architecture (proposition, à valider)

> Rien n'est figé. Cette page sert de base de discussion avant d'écrire du code.

## Stack

| Option | Pour | Contre |
|---|---|---|
| **C# + WPF/WinUI** (recommandée) | LibreHardwareMonitor donne les températures CPU/GPU facilement ; overlay transparent simple ; WMI natif | Moins familier si on vient du web |
| Tauri (Rust + web) | UI web, binaire léger | Capteurs matériels plus pénibles à brancher |
| Electron | UI web, écosystème | Lourd, mauvais fit pour un outil "léger" |

## Briques techniques

- **Specs** : WMI / CIM (`Win32_Processor`, `Win32_VideoController`, `Win32_PhysicalMemory`, `Win32_DiskDrive`...).
- **Capteurs** : LibreHardwareMonitor (températures, charges, ventilos). NVML pour les finesses NVIDIA si besoin.
- **FPS** : ETW (providers DXGI/D3D9 Present, comme PresentMon). Demande les droits admin, sans injection.
- **Scan disque** : parcours récursif parallèle au départ. Lecture directe de la MFT (façon WizTree) en amélioration ultérieure (droits admin).
- **Treemap** : algorithme squarified, rendu en canvas/DrawingVisual, agrégation des petits fichiers pour garder la fluidité.
- **Overlay** : fenêtre transparente, click-through, toujours au-dessus. Fonctionne en jeu fenêtré/borderless ; le plein écran exclusif est hors périmètre.

## Structure du repo envisagée

```
treemap/
  docs/            documentation (ici)
  src/
    Treemap.Core/      scan disque, specs, capteurs (sans UI, testable)
    Treemap.App/       interface
    Treemap.Overlay/   fenêtre overlay
  tests/
  .claude/         skills, agents et commandes Claude (voir tooling.md)
```

## Questions ouvertes

- Stack définitive (C# vs Tauri) ?
- Fenêtre classique ou icône dans la barre système + panneau ?
- GPU de la machine (NVIDIA / AMD / intégré) et jeux visés (anti-cheat ou non) ?
