# Scan du stockage : comment les tailles sont calculées

Les chiffres de treemap ne sont pas ceux de l'Explorateur Windows. Voici pourquoi, et ce que l'appli fait pour ne pas se tromper.

## Règles de comptage (`DiskScanner`)

| Cas | Traitement | Pourquoi |
|---|---|---|
| Fichier normal | taille du fichier | référence habituelle |
| Fichier **creux** (sparse) ou **compressé NTFS** | taille réellement allouée (`GetCompressedFileSize`) | un fichier creux peut annoncer 512 Go et n'en occuper que 3 |
| Fichier OneDrive **en ligne uniquement** | compté pour 0 | pas d'espace local |
| Jonction ou lien symbolique (dossier) | ignoré | évite les doubles comptes et les boucles |
| Fichiers cachés et système | comptés | c'est là que vivent `hiberfil.sys` et `pagefile.sys` |
| Dossier illisible (droits) | ignoré silencieusement | voir plus bas |

L'interface affiche en orange les corrections appliquées (par exemple "513 Go de moins que leur taille affichée par Windows").

## Pourquoi un total inférieur à "utilisé" dans Windows

Sur un disque entier, Windows déclare un espace utilisé un peu supérieur à ce que le scan peut lire : points de restauration, métadonnées NTFS (`$MFT`, journaux), dossiers réservés au système. L'écart est calculé et affiché quand il dépasse 1 Go. Il ne devrait pas être une surprise.

Les liens physiques (hard links), très présents dans `WinSxS`, sont comptés une fois par chemin : la taille de ce dossier peut donc être légèrement surestimée.

## Mémoire

Un dossier garde tous ses sous-dossiers mais seulement ses 20 plus gros fichiers ; les autres sont regroupés dans une entrée "N autres fichiers". Un scan complet de `C:\` (1,16 M de fichiers, 211 000 dossiers) reste ainsi raisonnable en mémoire.

## Performances mesurées

Sur la machine de développement (i5-10400F, SSD NVMe), disque `C:\` entier :

| Parallélisme | Temps |
|---|---|
| profondeur 2 | 13,7 s |
| profondeur 6 (défaut) | 5,8 s |

Le premier scan après un redémarrage est nettement plus long (cache des métadonnées froid, ~3 min observées).

## Suspects connus (`SpaceHogDetector`)

Détection par nom et par chemin, regroupée par catégorie, jamais de suppression. Catégories actuelles : fichier d'hibernation, fichier d'échange, vidage mémoire, disques virtuels (`.vhdx`, `.vhd`, `.vmdk`, `.vdi`), `node_modules`, WinSxS, `Windows.old`, corbeille, cache Windows Update, dossiers Temp, cache NuGet, bibliothèques de jeux (Steam, Epic, Xbox), Téléchargements. Chaque catégorie vient avec une explication et un conseil prudent. Pour en ajouter une : une ligne dans la liste `Rules` de `SpaceHogDetector.cs`, plus un test.
