# Design

Le système visuel est décrit dans [`DESIGN.md`](../DESIGN.md) (à la racine, format DESIGN.md, avec ses jetons en en-tête) et son annexe `.impeccable/design.json` (rampes de teintes, mouvement, extraits de composants). Le contexte produit est dans [`PRODUCT.md`](../PRODUCT.md) et la direction retenue dans `.impeccable/surfaces/`. Les jetons vivent dans `src/Treemap.Web/src/styles/tokens.css` ; l'overlay natif les reprend dans `src/Treemap.App/Themes/Palette.xaml` et la vue 3D dans `components/pc3d/parts3d.tsx` : une couleur qui change se change aux trois endroits.

## Revue finale (2026-11-07)

Faite sur l'interface complète (Composants, Stockage, Moniteur, Jeu, vue 3D, overlay).

| Contrôle | Résultat |
|---|---|
| Détecteur de motifs à éviter (Impeccable) sur `src/Treemap.Web/src` | aucun signalement |
| Contraste des couples de couleurs réellement utilisés | tous au-dessus de 4,5:1 (le plus bas : encre sur orange 4,67:1 ; l'orange n'est jamais du texte, le jaune jamais sur crème) |
| Texte plus petit que 12 px | corrigé : les graduations des graphiques passent de 11 à 12 px |
| Couleur seule porteuse de sens | non : verdicts écrits, bord pointillé pour « protégé », légendes de graphiques en texte et trait |
| Mouvement réduit | respecté (`prefers-reduced-motion` : fondu court, 3D et graphiques sans transition) |
| Images matricielles livrées | aucune (icône en SVG) : rien à documenter côté provenance |
| Overlay natif | repeint aux couleurs du système (encre, filet orange, valeurs crème, FPS en jaune), police Bahnschrift faute de pouvoir charger la police web |

## Ce que la revue n'a pas couvert

- **Pas de thème sombre** : l'interface est crème, y compris dans une pièce sombre (choix de la direction « fiche de catalogue »). À rediscuter si l'éblouissement gêne.
- **Fenêtre étroite** : l'appli a une largeur minimale de 960 px ; en dessous, rien n'est prévu.
- **Mode contraste élevé de Windows** : non géré.
- **Clavier** : parcours vérifié par les tests (listes, cases, dialogues, interrupteurs), pas par un lecteur d'écran réel.
- **Vue 3D** : glisser pour tourner, molette et animation « Assembler » non essayés à la main ; le canvas n'est pas accessible en lui-même (la liste des pièces l'est).
- **Surbrillance du FPS** sur l'overlay : le jaune n'a pas été vu en jeu (la mesure demande les droits administrateur).
