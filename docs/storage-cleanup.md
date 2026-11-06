# Suppression depuis l'écran Stockage

Décidé avec l'utilisateur : **Corbeille par défaut, avec garde-fous**. Par défaut rien n'est supprimé définitivement ; la suppression définitive n'existe que si l'utilisateur la force explicitement (voir plus bas).

## Principe

1. L'utilisateur coche des éléments de l'analyse (ou ouvre le détail d'un élément) puis demande « Supprimer… ».
2. Une fenêtre montre **avant toute action** chaque élément (nom, chemin, taille, ce que le moteur en dit).
3. Il confirme (et tape `SUPPRIMER` quand c'est demandé).
4. Le moteur envoie chaque élément à la **Corbeille de Windows** (`SHFileOperation`, avec « annuler » possible, donc « Restaurer » fonctionne).

La Corbeille ne libère de la place qu'une fois vidée : l'interface le dit, et propose d'ouvrir la Corbeille. Vider la Corbeille n'est pas fait par treemap.

## Forcer : suppression définitive

Dans la fenêtre de suppression, l'interrupteur **« Supprimer définitivement »** saute la Corbeille (`FileSystemDeleter`) : irréversible. Garde-fous :

- la **confirmation tapée** (`SUPPRIMER`) est toujours exigée, et vérifiée par le moteur ;
- les protections restent entières : un élément `blocked` ou `uninstallOnly` (Windows, jeux installés, liens, profil…) est refusé même en mode forcé, et un seul refus annule toute la demande ;
- les attributs lecture seule sont levés avant de supprimer (fréquents dans `node_modules` et les dossiers de jeux) ; un fichier utilisé par un programme est signalé comme non supprimé, sans rien casser d'autre.

## Vider la Corbeille

Une bande en haut de l'écran Stockage montre le contenu de la Corbeille (`storage.recycleBin`, lecture seule) et propose « Vider la Corbeille… » : suppression définitive pour tous les disques, derrière la même confirmation tapée (`storage.emptyRecycleBin`, vérifiée par le moteur).

## Qui décide : le moteur, pas l'interface

`DeletionAdvisor` (dans `Treemap.Core.Storage.Cleanup`) classe chaque chemin. `StorageSession.Delete` refait cette évaluation au moment de supprimer, vérifie que le chemin appartient à l'analyse en cours, et que la confirmation tapée est la bonne. Si un seul élément est refusé, **rien** n'est supprimé. Les chemins reçus de la page ne sont jamais passés tels quels au shell : on utilise le chemin du nœud de l'analyse (le shell interprète les jokers `*` et `?`).

| Verdict | Signification | Exemples |
|---|---|---|
| `allowed` | Envoi à la Corbeille possible | add-ons et cache de Microsoft Flight Simulator, `node_modules`, `Temp`, téléchargements, dossiers quelconques |
| `uninstallOnly` | Logiciel installé : on ouvre son désinstalleur | jeux Steam / Epic / Xbox, dossiers de `Program Files`, contenu `Official` de Flight Simulator |
| `blocked` | Jamais proposé | Windows, racines de disques, profil utilisateur, `AppData`, bibliothèques (Documents, Images…), liens et jonctions, disques non fixes, éléments introuvables |

Confirmation **tapée** (`SUPPRIMER`) : données personnelles (Documents, Images, OneDrive…), dossier Téléchargements entier, données d'applications (`AppData`), `ProgramData`, et tout élément inconnu de 10 Go ou plus. Les éléments connus et recréables (cache, `node_modules`, add-ons) demandent seulement la confirmation simple.

## Microsoft Flight Simulator

- `...\Packages\Community\*` : les add-ons, supprimables un par un ou en bloc ; le jeu reste intact.
- `...\Packages\ROLLINGCACHE.CCC` : cache recréé par le jeu.
- `...\Packages\Official\*` : **le jeu lui-même**, jamais supprimé à la main. Boutons « Désinstaller dans Steam » (`steam://uninstall/1250410`) ou « Ouvrir Applications installées » selon l'installation détectée (Steam : `AppData\Roaming\Microsoft Flight Simulator`, Store/Xbox : `Packages\Microsoft.FlightSimulator_*`).

## Désinstalleurs

Seules ces adresses peuvent être lancées (`DeletionAdvisor.IsSafeLauncherUri`), et elles viennent du moteur, jamais de la page : `ms-settings:appsfeatures`, `steam://nav/games`, `steam://uninstall/<id>` (id lu dans `appmanifest_*.acf`), `com.epicgames.launcher://apps`.

## Limites connues

- Chemins de 260 caractères ou plus : refusés (le shell ne les gère pas).
- Un élément trop gros pour la Corbeille : Windows demande lui-même confirmation avant de le détruire (`FOF_WANTNUKEWARNING`) ; un refus est signalé comme non supprimé.
- Les règles de classement sont fondées sur les chemins : un jeu installé dans un dossier sans `steamapps`, `Epic Games` ou `XboxGames` est vu comme un dossier ordinaire (confirmation tapée s'il pèse 10 Go ou plus).

## Messages du pont

`storage.drives`, `storage.scan` (réponse à la fin, évènement `storage.progress` pendant), `storage.cancel`, `storage.node`, `storage.preview`, `storage.delete`, `storage.reveal`, `storage.openUninstaller`, `storage.openRecycleBin`, `storage.recycleBin`, `storage.emptyRecycleBin`, (`preview` et `delete` acceptent `permanent: true`), `storage.pickFolder`. Détail du format dans [architecture.md](architecture.md).
