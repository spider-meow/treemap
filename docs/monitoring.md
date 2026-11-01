# Monitoring et overlay

## Ce qui est mesuré

Une mesure par seconde, sans droits administrateur, historique des deux dernières minutes.

| Métrique | Source |
|---|---|
| Charge CPU | `GetSystemTimes` (kernel32), delta entre deux lectures |
| Mémoire | `GlobalMemoryStatusEx` |
| Disque (activité, lecture, écriture) | compteurs de performance `PhysicalDisk\_Total` |
| Réseau (débit descendant et montant) | statistiques des interfaces actives, hors boucle locale et tunnels |
| GPU NVIDIA (charge, VRAM, température, puissance, fréquence, ventilateur) | NVML (`nvml.dll`, installée avec le pilote NVIDIA, la même source que `nvidia-smi`) |

Les lectures GPU ont été comparées à `nvidia-smi` sur la machine de développement (températures, puissance, fréquence, VRAM concordantes).

Une carte AMD ou Intel n'est pas lue pour l'instant : les cartes GPU affichent « Aucune carte NVIDIA détectée ».

## Ce qui n'est pas mesuré, et pourquoi

**Température du processeur.** Windows ne l'expose pas sans pilote noyau. La solution habituelle (LibreHardwareMonitor) charge un pilote noyau et demande les droits administrateur. Comme cela touche à la sécurité du système, ce n'est pas activé : à décider explicitement si on veut l'ajouter (voir la roadmap).

## L'overlay

Un panneau toujours au premier plan, qui ne prend jamais le focus et laisse passer tous les clics vers le jeu. Il se règle dans l'onglet **Moniteur** : lignes affichées (FPS, processeur, carte graphique, température GPU, mémoire, mémoire vidéo), coin de l'écran, opacité. Les réglages sont sauvegardés dans `%APPDATA%\treemap\settings.json`.

- Raccourci global **Ctrl+Alt+F10** pour l'afficher ou le masquer, même dans un jeu.
- Fonctionne avec les jeux en **fenêtré** ou **fenêtré sans bordure**. En plein écran exclusif Windows masque toute fenêtre par-dessus le jeu, aucune appli ne peut y échapper sans injection, ce que treemap refuse de faire (risque de blocage par les anti-cheat).
- Fermer la fenêtre principale ferme l'appli et donc l'overlay : pour jouer, la réduire plutôt que la fermer.

## Le FPS

Mesure par les événements de présentation d'images de Windows, comme Intel PresentMon : aucun code n'est injecté dans le jeu.

- Fournisseurs utilisés : `Microsoft-Windows-DXGI` (DirectX 10, 11, 12) et `Microsoft-Windows-D3D9` (DirectX 9). Leurs identifiants et l'événement « Present / début » ont été vérifiés dans les manifestes Windows de la machine.
- **Vulkan et OpenGL ne sont pas couverts.**
- Seule l'application au premier plan est mesurée, pour ne pas confondre un jeu avec le compositeur du bureau, qui présente des images en permanence.
- **Windows réserve l'écoute de ces événements aux administrateurs.** Sans cela, l'overlay affiche « admin requis » et l'onglet Moniteur propose « Relancer en administrateur », qui passe par la fenêtre de consentement habituelle de Windows. Les réglages sont conservés au redémarrage.

### État de vérification

La logique de comptage (FPS sur une seconde glissante, choix du processus au premier plan, états d'erreur) est testée. **La capture ETW réelle n'a pas pu être testée sans droits administrateur** : elle reste à valider une fois, en lançant l'appli en administrateur avec un jeu DirectX ouvert. Si le FPS affiché semble faux ou reste à « — », c'est le premier endroit où regarder.

## Code

| Élément | Où |
|---|---|
| Mesures, historique, échantillonnage | `Treemap.Core/Monitoring` |
| FPS | `Treemap.Core/Monitoring/Fps` |
| Choix des lignes de l'overlay et placement | `OverlayContent`, `OverlayPlacement` (testés) |
| Réglages | `Treemap.Core/Settings` |
| Fenêtre overlay, raccourci, relance admin | `Treemap.App` (`OverlayWindow`, `Services`) |
