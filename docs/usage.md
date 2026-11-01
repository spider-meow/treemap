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

L'appli n'a pas besoin des droits administrateur. Sans eux, certains dossiers système ne sont pas lisibles ; l'écart est expliqué dans l'interface.

## Options de lancement (développement)

| Option | Effet |
|---|---|
| `--tab=components` ou `--tab=storage` | ouvre directement un onglet |
| `--scan=C:\chemin` | ouvre l'onglet Stockage et lance l'analyse du chemin |

```bash
dotnet run --project src/Treemap.App -- --scan=C:\
```

## Tests

```bash
dotnet test Treemap.slnx
```

Les tests couvrent le formatage des tailles, l'algorithme de treemap, le scanner (y compris fichiers creux, cachés, annulation), la détection des suspects et la lecture des specs. Le test `WmiProvider_ReadsThisMachine` interroge la vraie machine : il suppose Windows.

## Structure

Voir [architecture.md](architecture.md). Règle : tout ce qui est calcul vit dans `Treemap.Core` (testable sans interface), `Treemap.App` ne contient que l'interface et ses view-models.
