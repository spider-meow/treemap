# Architecture

## Décision : C# + WPF (.NET LTS)

Choisi le 2026-11-01. Machine cible : Windows 11, RTX 3060, i5-10400F, disque C: presque plein (≈105 Go libres), donc on évite les chaînes d'outils lourdes.

| Option | Pour | Contre | Verdict |
|---|---|---|---|
| **C# + WPF** | LibreHardwareMonitor pour les températures ; overlay transparent simple ; WMI et ETW natifs ; SDK ≈ 1 Go | Moins familier qu'une stack web | **Retenue** |
| Tauri (Rust + web) | UI web, binaire léger | Rust + Build Tools VS = 6 à 10 Go ; capteurs à brancher à la main | Écartée |
| Electron | Node déjà installé, UI web | Lourd en RAM, contradictoire pour un outil de monitoring ; FPS via PresentMon externe | Écartée |

État de l'environnement au moment du choix : Node 24 et Python 3.13 présents ; seuls les runtimes .NET 8 (Core + Desktop) sont installés, **pas de SDK**. SDK cible : .NET 10 (LTS), paquet winget `Microsoft.DotNet.SDK.10`.

## Décision : interface web dans WebView2 (2026-11-01)

L'interface WPF a atteint ses limites pour ce qui est demandé ensuite : une vue 3D des composants, un design travaillé (palette, typographie, transitions) et des animations. Le moteur C# reste tel quel ; seule la couche d'affichage change.

- **Interface** : `src/Treemap.Web`, Vite + React 19 + TypeScript, `motion` pour les animations, `lucide-react` pour les icônes, Three.js (react-three-fiber) prévu pour la vue 3D. Tests avec Vitest.
- **Hébergement** : `Treemap.App` ouvre l'interface dans un WebView2 (`WebHostWindow`). L'interface construite est servie depuis le dossier `web/` à côté de l'exécutable, par un nom d'hôte virtuel (`https://treemap.app`) : aucun port réseau n'est ouvert. Les popups et la navigation hors de cette origine sont bloquées.
- **Pont** : messages JSON entre la page et le moteur. Format côté page → moteur `{ id, type, payload }`, réponse `{ kind: "response", id, ok, payload | error }`, évènement poussé `{ kind: "event", type, payload }`. Le routage vit dans `Treemap.Core.Bridge.BridgeRouter` (testé, sans dépendance à WebView2) ; `WebBridge` (dans App) le branche au WebView2 et n'accepte que les messages venant de l'origine servie. Messages actuels : `specs.get` et l'évènement `metrics` (une fois par seconde). Hors de l'appli, `bridge/mock.ts` répond à la place du moteur.
- **Overlay** : reste une fenêtre WPF native (transparente, click-through) ; une page web ne peut pas jouer ce rôle.
- **Transition** : l'interface WPF reste l'écran par défaut ; `--web` / `--dev` ouvrent la nouvelle. Elle sera retirée quand Stockage et Moniteur auront leur équivalent web.
- **Design** : direction « fiche de catalogue industriel » (voir `PRODUCT.md` et `.impeccable/surfaces/`), palette crème / vert forêt / orange mandarine, accent jaune cristal.

## Briques techniques

- **Specs** : WMI via `System.Management` (`Win32_Processor`, `Win32_VideoController`, `Win32_PhysicalMemory`, `MSFT_PhysicalDisk`...). Mémoire vidéo lue dans le registre (WMI plafonne `AdapterRAM` à 4 Go). Chaque section échoue indépendamment dans une liste d'avertissements.
- **Capteurs** : kernel32, compteurs de performance et NVML (GPU NVIDIA), sans droits administrateur ni pilote. LibreHardwareMonitor est écarté pour l'instant car il charge un pilote noyau (voir [monitoring.md](monitoring.md)).
- **FPS** : ETW (providers DXGI et D3D9, événement Present, comme PresentMon) via `Microsoft.Diagnostics.Tracing.TraceEvent`. Demande les droits administrateur, aucune injection.
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
      Monitoring/        mesures en direct, historique, contenu de l'overlay
        Fps/             comptage de FPS, source ETW
      Settings/          réglages (JSON)
      Bridge/            routeur des messages JSON de l'interface web
    Treemap.Web/       interface web (Vite + React), hébergée dans WebView2
    Treemap.App/       interface WPF et hôte WebView2, référence Core
      Controls/          TreemapControl
      ViewModels/        Main, Components, Storage, Monitor, OverlaySettings
      Views/             ComponentsView, StorageView, MonitorView
      Services/          MonitoringService, OverlayController, GlobalHotkey, AdminRelauncher
      OverlayWindow      fenêtre overlay (dans le projet App, pas de projet séparé)
      Themes/            palette
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
