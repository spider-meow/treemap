# Vue 3D des composants

Bouton « Vue 3D » sur l'écran Composants (ou `--view=3d` au lancement). Elle remplace la fiche par une **planche éclatée** : le PC démonté sur un seul dessin, comme une illustration technique de catalogue.

## Principe

- **Axonométrie** (caméra orthographique), aplats de couleur et arêtes d'encre : pas de dégradé, d'ombre portée ni de verre, comme le reste de l'interface. Les couleurs sont celles de la palette (`components/pc3d/parts3d.tsx`, à garder en phase avec `styles/tokens.css`).
- Les pièces sont à leur place réelle sur la carte mère puis écartées verticalement, reliées à la carte par un **trait pointillé** ; chacune porte son **numéro de catalogue** (01 processeur, 02 carte graphique, 03 mémoire, 04 carte mère, 05 stockage), le même que sur la fiche.
- Les pièces dessinées viennent de ce que le moteur a lu : un module de mémoire dessiné par barrette détectée, un disque par disque (barrette M.2 pour NVMe/SSD, boîtier 2,5" sinon, trois au maximum). Ce qui n'est pas lu n'est pas dessiné.
- Les modèles sont **procéduraux** (boîtes et disques), pas des modèles du commerce : ils évoquent un type de pièce, pas la référence exacte.

## Interactions

- Glisser pour tourner, molette pour zoomer (bornes relatives à la taille de la scène), pas de déplacement latéral.
- Survol : la pièce passe en jaune cristal. Clic : orange, et sa fiche (les mêmes lignes que sur la fiche, avec la charge et la température en direct pour la carte graphique) s'affiche à côté ; clic dans le vide ou sur la même pièce dans la liste : désélection.
- « Assembler » / « Éclater » anime le passage d'un état à l'autre (instantané si l'utilisateur a demandé moins d'animations).
- **Liste des pièces** à côté du dessin : même sélection au clavier et au lecteur d'écran (le canvas n'est pas accessible en lui-même), et survol synchronisé avec le dessin.

## Technique

- `three`, `@react-three/fiber`, `@react-three/drei`. Chargées à la demande (`React.lazy`) : l'écran Composants ne paie pas ce poids tant que la 3D n'est pas ouverte.
- Rendu **à la demande** (`frameloop="demand"`) : aucune image n'est dessinée tant que rien ne bouge, donc pas de charge GPU en arrière-plan.
- Sans WebGL, un message l'indique et la liste reste utilisable.
- Les données des pièces sont partagées avec la fiche (`lib/parts.tsx`).

## Limites

- Pas de modèles réalistes ni de textures ; l'apparence est la même quel que soit le modèle du composant.
- Les tests ne dessinent pas : jsdom n'a pas WebGL, la scène y est simulée. La scène a été vérifiée à l'œil dans le navigateur intégré et dans la fenêtre de l'appli.
