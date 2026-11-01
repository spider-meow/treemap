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
| `--web` | affiche la nouvelle interface web (WebView2) au lieu de l'interface WPF ; `--tab=` choisit l'écran |
| `--dev` | comme `--web`, mais charge l'interface depuis le serveur Vite (`http://localhost:5173`), avec rechargement à chaud |

```bash
dotnet run --project src/Treemap.App -- --scan=C:\
```

### Interface web

Pour l'instant seul l'écran Composants est refait dans le nouveau design ; l'interface WPF reste celle par défaut jusqu'à ce que Stockage et Moniteur soient portés.

```bash
cd src/Treemap.Web
npm install
npm run build            # produit dist/, copié à côté de l'exécutable au build de l'appli
dotnet run --project ../Treemap.App -- --web
```

Pour travailler sur le design : `npm run dev` dans `src/Treemap.Web`, puis lancer l'appli avec `--dev`. Dans un navigateur ordinaire, un moteur factice répond à la place du moteur C# (valeurs d'exemple). `npm test` lance les tests de l'interface.

## Tests

```bash
dotnet test Treemap.slnx
```

Les tests couvrent le formatage des tailles, l'algorithme de treemap, le scanner (y compris fichiers creux, cachés, annulation), la détection des suspects, la lecture des specs, les mesures en direct, le comptage de FPS, le contenu et le placement de l'overlay et les réglages. Plusieurs tests (`WmiProvider_ReadsThisMachine`, `SystemSource_ReturnsPlausibleValuesOnThisMachine`, `Gpu_WhenPresent_ReportsCoherentValues`) interrogent la vraie machine : ils supposent Windows.

## Structure

Voir [architecture.md](architecture.md). Règle : tout ce qui est calcul vit dans `Treemap.Core` (testable sans interface), `Treemap.App` ne contient que l'interface et ses view-models.
