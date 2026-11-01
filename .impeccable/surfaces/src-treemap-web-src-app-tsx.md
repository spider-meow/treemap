---
version: 1
slug: "src-treemap-web-src-app-tsx"
primary_target: "src/Treemap.Web/src/App.tsx"
related_targets: []
---

# Surface brief: treemap app shell and screens

## Scope and visitor mode

Mode: Operate. The whole desktop app UI (shell, Composants, Stockage, Moniteur, later Jeu). Built code-led. One owner uses it on their own PC, often before or after a long game session, in a dim room, on a 1080p to 1440p monitor.

## Audience, job, task, constraints

The owner wants to understand the machine, reclaim disk space safely, watch live figures and prepare the PC for a game. Frequent glances, occasional deliberate actions (delete, optimise). French only. Palette imposed by the owner: cream white ground, Forest Green #015D47, Mandarin Orange #FD550A, Crest Yellow #E0F354 as accent. Standard controls and navigation stay standard (side rail, buttons, tabs, tables, inputs). The world lends type, palette, density and one signature move only.

## Chosen direction

Industrial product catalogue sheet (Olivetti and Braun era), assigned by the roll. Memorable moment: the numbered specimen plate whose index numeral follows the object across screens and slides into its detail like a sheet pulled from a stack.

## Unresolved

Exact grotesque face (to be chosen against the font index and the banned list). 3D exploded view styling inside this world (next phase). Whether the Jeu screen gets its own plate colour.

## Direction contract

THESIS: treemap is a specimen catalogue of one PC. Every part, drive, game and process becomes a numbered plate. It refuses the dark neon gaming dashboard and the grid of rounded translucent cards with a tinted icon tile above each heading.

OWN-WORLD: Cream sheet ground tinted slightly green-yellow so it sits with Crest Yellow. Flat, full-bleed colour fields with no gradients, no shadows, no glass: Forest Green for the rail and the heavy plates, Mandarin Orange for the one primary action per screen and for live or changed values, Crest Yellow only as index tabs and highlights on green or orange, never as text on cream. Ink is a green-black, rules are 2 to 3 px solid ink. Corners square or 2 px. One grotesque throughout, heavy weights for giant index numerals, tabular figures for data. Specimen plates carry an object index numeral, a colour tab, a caption block and ruled spec rows. Numerals identify objects only; they never number sections or pages. Orange is a fill or rule, never text on cream or green (contrast): text on orange is ink. Live figures use ruled tables and real axis-labelled charts, not metric tiles or sparklines.

STORY: The owner opens the app and reads their PC like a catalogue they can trust: numbered, measured, explained in plain French. They understand what is where, act with a preview and a way back, and see the same numbers follow each object from Composants to Stockage to Jeu.

FIRST VIEWPORT: 1440 by 900. Left rail 248 px solid Forest Green: wordmark "treemap" in cream at top, un-numbered nav Composants, Stockage, Moniteur, Jeu, active item on a Crest Yellow tab with green text, drive usage meter at the foot. Cream workspace: page header with the title set large and a one-line plain-French summary (no section number, no eyebrow), the single Mandarin Orange button "Copier le résumé" top right. Below, an asymmetric sheet of specimen plates of different scales (Carte graphique and Processeur large, Mémoire, Carte mère, Stockage, Système smaller), never a grid of identical cards; each plate carries its object index numeral (the stable catalogue number of that part, reused on every screen), a colour tab and ruled spec rows. Disk usage runs as a full-width ruled bar above the grid.

FORM: Industrial catalogue sheet, candidate 7 of my ordered grounded list, assigned. seed: seed:dbe46ff2622a38c0

SIGNATURE: The index numeral is the object's identity across screens. Selecting a plate makes its numeral grow and travel into the detail view (shared layout transition, 280 to 360 ms, ease-out-quart, no bounce) while other plates step back like sheets in a stack. Live values roll like a counter. Page changes slide the workspace one sheet at a time. Reduced motion replaces all travel with a short fade.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance
