# Utilisation et développement

## Prérequis

- Windows 11
- SDK .NET 10 (`winget install --id Microsoft.DotNet.SDK.10 --exact`)

## Lancer

```bash
dotnet run --project src/Treemap.App
```

Deux onglets :

- **Composants** : ce que contient le PC, avec le bouton "Copier le résumé".
- **Stockage** : choisir un disque ou un dossier ("Dossier…"), "Analyser", puis explorer l'arbre à gauche, la carte (clic pour descendre dans un dossier) ou l'onglet "À examiner" pour les gros consommateurs connus. L'appli ne supprime jamais rien.

- **Moniteur** : processeur, mémoire, carte graphique, VRAM, disque et réseau en direct, et les réglages de l'overlay en jeu (voir [monitoring.md](monitoring.md)). **Ctrl+Alt+F10** affiche ou masque l'overlay.

L'appli n'a pas besoin des droits administrateur, sauf pour le FPS de l'overlay (Windows réserve cette mesure aux administrateurs ; l'onglet Moniteur propose de relancer l'appli). Sans eux, certains dossiers système ne sont pas lisibles ; l'écart est expliqué dans l'interface.

## Options de lancement (développement)

| Option | Effet |
|---|---|
| `--tab=components`, `--tab=storage` ou `--tab=monitor` | ouvre directement un onglet |
| `--scan=C:\chemin` | ouvre l'onglet Stockage et lance l'analyse du chemin |

```bash
dotnet run --project src/Treemap.App -- --scan=C:\
```

## Tests

```bash
dotnet test Treemap.slnx
```

Les tests couvrent le formatage des tailles, l'algorithme de treemap, le scanner (y compris fichiers creux, cachés, annulation), la détection des suspects, la lecture des specs, les mesures en direct, le comptage de FPS, le contenu et le placement de l'overlay et les réglages. Plusieurs tests (`WmiProvider_ReadsThisMachine`, `SystemSource_ReturnsPlausibleValuesOnThisMachine`, `Gpu_WhenPresent_ReportsCoherentValues`) interrogent la vraie machine : ils supposent Windows.

## Structure

Voir [architecture.md](architecture.md). Règle : tout ce qui est calcul vit dans `Treemap.Core` (testable sans interface), `Treemap.App` ne contient que l'interface et ses view-models.
