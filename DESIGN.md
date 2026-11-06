---
name: treemap
description: "A specimen catalogue of one PC: numbered plates, ruled tables, flat colour fields."
colors:
  cream: "#F4F1E1"
  cream-deep: "#E8E4CC"
  ink: "#0D2B24"
  ink-soft: "#3B5C52"
  forest: "#015D47"
  forest-deep: "#013B2F"
  on-forest: "#F4F1E1"
  on-forest-soft: "#B9D6C9"
  crest: "#E0F354"
  mandarin: "#FD550A"
  alert-text: "#B42A00"
typography:
  display:
    fontFamily: "Archivo Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "clamp(2.5rem, 3.4vw + 1rem, 4rem)"
    fontWeight: 850
    lineHeight: 0.95
    letterSpacing: "-0.02em"
    fontVariation: "wdth 75"
  numeral:
    fontFamily: "Archivo Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "clamp(5rem, 6vw + 2rem, 8rem)"
    fontWeight: 850
    lineHeight: 0.78
    letterSpacing: "-0.02em"
    fontVariation: "wdth 62"
  headline:
    fontFamily: "Archivo Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "1.5rem"
    fontWeight: 700
    lineHeight: 1.1
    letterSpacing: "-0.005em"
  lead:
    fontFamily: "Archivo Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "1.125rem"
    fontWeight: 450
    lineHeight: 1.5
  body:
    fontFamily: "Archivo Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "0.9375rem"
    fontWeight: 450
    lineHeight: 1.5
  label:
    fontFamily: "Archivo Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 750
    lineHeight: 1.5
    letterSpacing: "0.04em"
rounded:
  square: "2px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  xxl: "48px"
  page: "64px"
components:
  button-primary:
    backgroundColor: "{colors.mandarin}"
    textColor: "{colors.ink}"
    rounded: "{rounded.square}"
    padding: "0 24px"
    height: "44px"
  button-primary-hover:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.mandarin}"
  button-secondary:
    backgroundColor: "{colors.cream}"
    textColor: "{colors.ink}"
    rounded: "{rounded.square}"
    padding: "0 24px"
    height: "44px"
  button-secondary-hover:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.cream}"
  button-danger:
    backgroundColor: "{colors.alert-text}"
    textColor: "{colors.cream}"
    rounded: "{rounded.square}"
    padding: "0 24px"
    height: "44px"
  button-quiet-hover:
    backgroundColor: "{colors.crest}"
    textColor: "{colors.ink}"
  plate-paper:
    backgroundColor: "{colors.cream-deep}"
    textColor: "{colors.ink}"
    rounded: "{rounded.square}"
    padding: "24px"
  plate-forest:
    backgroundColor: "{colors.forest}"
    textColor: "{colors.on-forest}"
    rounded: "{rounded.square}"
    padding: "24px"
  plate-crest:
    backgroundColor: "{colors.crest}"
    textColor: "{colors.ink}"
    rounded: "{rounded.square}"
    padding: "24px"
  rail:
    backgroundColor: "{colors.forest}"
    textColor: "{colors.on-forest}"
    width: "248px"
  rail-item-active:
    backgroundColor: "{colors.crest}"
    textColor: "{colors.forest}"
    height: "44px"
  tag-live-value:
    backgroundColor: "{colors.mandarin}"
    textColor: "{colors.ink}"
  selection-bar:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.cream}"
---

# Design System: treemap

## Overview

**Creative North Star: "The Specimen Catalogue"**

treemap reads one PC the way an industrial catalogue of the Olivetti and Braun years reads a product range: every part, drive, game and process is a numbered specimen on a sheet, measured and explained in plain French. The owner opens it before or after a long game session, in a dim room, for one person's own machine. It is a tool (Operate mode): scanability, standard controls and the real usage scene outrank expression, and the personality lives in precise details, not in decoration.

The surface is flat. Colour arrives as full fields (Forest Green for the rail and the heavy plates, Crest Yellow for one plate and for highlights, Mandarin Orange for the single primary action and for live values) on a cream ground tinted slightly green-yellow so it sits with the yellow. Structure comes from 2 px ink rules, ruled spec rows, square corners and one grotesque used at two widths. Nothing glows, floats, blurs or bounces.

It rejects the dark neon gaming dashboard and the grid of identical rounded translucent cards with a tinted icon tile above each heading.

**Key Characteristics:**
- Numbered plates: the catalogue number of an object (01 processeur, 02 carte graphique, 03 mémoire, 04 carte mère, 05 stockage, 06 système, 07 réseau, 08 overlay, 09 à 11 booster) follows it across every screen.
- Flat colour fields, 2 px ink rules, square corners (2 px at most).
- One grotesque, two voices: normal width to read, condensed heavy for numerals and titles.
- Live data in ruled tables and axis-labelled charts, never metric tiles or sparklines.
- Safe by construction: every destructive action shows what it will do first, and the engine decides, not the page.

## Colors

A cream sheet, a green-black ink, two heavy fields and one highlight. Orange and yellow are accents with strict rules about where they may appear.

### Primary
- **Forest Green** (`#015D47`): the rail, the heavy plates (carte graphique, suspects, la session en cours), usage fills below the alert level, the "uninstall" cells. Text on it is cream (6.97:1) or the soft pale green (5.09:1).
- **Mandarin Orange** (`#FD550A`): the one primary button per screen, live or changed values, usage fills once a volume or resource is high, the selected cell or row, the newest point of a chart, switches that are on. Always a fill or a rule carrying **ink** text (4.67:1). Never text: it is 2.86:1 on cream.

### Secondary
- **Crest Yellow** (`#E0F354`): the active tab in the rail, hover fill, the stockage plate and the overlay plate, numerals and chart lines on forest (6.44:1). Never on cream as text or thin line (1.08:1).

### Neutral
- **Cream** (`#F4F1E1`): the ground of every screen, the secondary button, the inputs.
- **Paper** (`#E8E4CC`): the default plate, the detail band, the list bands (ink on it 11.8:1, soft ink 5.8:1).
- **Ink** (`#0D2B24`): text, every rule, chart lines, the selection bar and the native overlay's ground (13.3:1 on cream).
- **Soft Ink** (`#3B5C52`): secondary text (6.5:1 on cream, 5.8:1 on paper).
- **Deep Forest** (`#013B2F`): hover and pressed state on forest, the track of bars on forest.
- **Pale Forest** (`#B9D6C9`): secondary text on forest.
- **Alert Red** (`#B42A00`): error and irreversible-action text on cream (5.66:1) and the danger button fill with cream text.

### Named Rules
**The Fill-Not-Text Rule.** Mandarin is a fill or a rule carrying ink, never text. Crest is for forest and ink grounds, never for text on cream.

**The One Primary Rule.** One Mandarin button per screen. Everything else is secondary, quiet, or (for irreversible actions only) danger.

**The Earned Orange Rule.** Orange marks what deserves attention: a live value, a high usage, the current selection. A screen with orange everywhere has none.

## Typography

**Display and body font:** Archivo Variable (with Segoe UI, system-ui, sans-serif), self-hosted through `@fontsource-variable/archivo`, width axis 62 to 100.

**Character:** A heavy industrial grotesque. Condensed and very heavy for numerals and titles (width 62 and 75, weight 850), normal width at 450 to 700 for everything that is read. Figures are tabular wherever data is aligned.

### Hierarchy
- **Display** (850, `clamp(2.5rem, 3.4vw + 1rem, 4rem)`, 0.95, width 75): the page title, once per screen, over a 6 px ink rule.
- **Numeral** (850, `clamp(5rem, 6vw + 2rem, 8rem)`, 0.78, width 62): the catalogue number of a plate, the scan counters. It identifies an object, never a section or a page.
- **Headline** (700, 1.5rem, 1.1): plate titles, dialog titles (800 at width 75).
- **Lead** (450, 1.125rem, 1.5): the one-line summary under the title, max 62ch.
- **Body** (450, 0.9375rem, 1.5): values and prose; spec values at 600.
- **Label** (750, 0.75rem, +0.04em, uppercase): table heads, verdict tags, group titles. Chart ticks are 12 px.

### Named Rules
**The Numerals Identify Rule.** Large numerals are catalogue numbers of objects. They never number sections, steps or pages.

**The Tabular Rule.** Every aligned figure (sizes, rates, percentages) uses tabular numbers; live ones roll like a counter instead of jumping.

## Layout

A 248 px forest rail on the left (wordmark, un-numbered navigation, drive meter at the foot) and a cream workspace that scrolls on its own, `max-width` 96rem, padded 32 px by 48 px. Screens are an asymmetric **sheet** of 12 columns: plates of different spans (4, 5, 6, 7, 8 or 12), never a grid of identical cards. The sheet and the plate groups follow the width of the workspace through container queries (breakpoints 880, 860, 760 and 520 px), not the window. Spacing is a 4 px scale (4, 8, 16, 24, 32, 48, 64); rhythm comes from the rules, not from empty space.

Density is that of a catalogue: ruled rows with 8 px of padding, 44 px minimum target height for buttons and navigation. Standard controls and navigation stay standard (rail, buttons, tables, switches, dialogs).

## Elevation & Depth

**Flat by construction.** No shadows, no blur, no glass, no gradient fills. Depth is a hierarchy of colour fields and 2 px rules: a heavy field (forest, crest) steps forward from the paper plates, and the selected item turns Mandarin. The only layering is the modal dialog, which sits on a deep-forest scrim at 62 % and slides up 20 px with an ease-out-expo curve, and the selection bar, a solid ink bar fixed to the bottom of the workspace.

The single hairline construction that looks like a gradient is the 10 % graduation of the ruled bar (a repeating ink line, not a colour transition). In the 3D view, faces are lit flat (Lambert, one directional light) with ink edges: it reads as a technical drawing, not a render.

### Named Rules
**The Flat-By-Default Rule.** No box-shadow anywhere. If something must stand out, it takes a heavier colour field or a rule.

## Shapes

Square. Radius is 2 px on every control and plate; nothing is pill-shaped or circular (the only round forms are the fans drawn on the graphics card in the 3D view). Edges are 2 px solid ink for plates, controls and dialogs, 1 px hairlines (ink at 35 %) between spec rows. A **dashed** 2 px edge means "not for deleting" (protected cell, protected row, empty drop zone). Cards are never nested.

## Components

### Buttons
- **Shape:** square, 2 px radius, 2 px ink border, 44 px high, 24 px side padding, 700 weight.
- **Primary:** Mandarin fill, ink text. On hover it inverts to an ink fill with Mandarin text.
- **Secondary:** cream fill, ink text, 2 px ink border; hover inverts to ink with cream text.
- **Quiet:** no border, underlined 2 px, hover fills Crest.
- **Danger:** Alert Red fill with cream text, hover ink; only for irreversible actions (permanent deletion, emptying the Recycle Bin).
- **Pressed:** moves 1 px down. Disabled: 50 % opacity, no pointer.

### Plates
- **Corner Style:** 2 px, 2 px ink border, 24 px padding.
- **Background:** paper by default; forest (cream text, crest numeral) for the heavy plate of a screen; crest (ink text) for one accent plate.
- **Head:** the title bottom-aligned on the left, the condensed catalogue numeral on the right.
- **Body:** ruled spec rows (label 13 px soft ink, value 600), a 1 px hairline above each row.

### Ruled bar
A 1.5 rem track with a 2 px ink border, filled Forest from the left and Mandarin once usage reaches 80 %, graduated every 10 %. On forest the fill is Crest.

### Charts
Three hairline gridlines (0, half, full scale) with their values at the left, a time axis (−2 min, −1 min, maintenant), a 2.25 px ink line (crest on forest), a square marker on the newest reading in Mandarin with an ink edge. Two series are told apart by solid and dashed lines and a text legend, never by colour alone.

### Switches and corner picker
A switch is a ruled 44 by 24 px slot with an ink square slider; on, the slot is Mandarin. The corner picker is the screen in miniature: press the corner where the overlay sits (the chosen one is ink).

### Treemap
Every entry is a cell whose area is its share. Cells are paper with 1 px ink edges; forest means "remove through its uninstaller"; a dashed edge means protected; hover fills Crest; the selected cell is Mandarin with an inset ink outline; a ticked cell carries an ink corner mark.

### Dialogs
A native `<dialog>` with a 2 px ink border, a heading over a 2 px rule, one assurance sentence (grey for a reversible action, Alert Red for an irreversible one), an optional typed-word field, and a footer with the secondary action first and the primary or danger action last. The primary action states its consequence ("Envoyer 185 Go à la corbeille", "Supprimer 185 Go définitivement").

### Navigation
The rail: un-numbered items with a 20 px stroke icon and a 600 label. The active item is a Crest tab with Forest text that **slides** to the next item on navigation. Pages slide one sheet at a time (28 px, 280 ms, ease-out-quart; fade only under reduced motion).

### Overlay (native)
A catalogue tag over the game: ink field, a 4 px Mandarin rule on the left edge, square corners, labels in pale green (7.8:1), values in cream, the frame rate in Crest, set in Bahnschrift (the DIN of Windows) because the native window cannot use the web font.

### 3D exploded view
An axonometric drawing of the machine: flat fills in the plate colours, ink edges, parts lifted apart on dashed drop lines, each with a catalogue-number tag. Hover turns a part Crest, selection Mandarin. A list of the parts beside it carries the same selection for keyboards.

## Do's and Don'ts

### Do:
- **Do** use `{colors.mandarin}` only as a fill or a rule carrying `{colors.ink}` text, and only for the one primary action, live values, high usage and the current selection.
- **Do** keep every border 2 px solid ink and every radius 2 px or less.
- **Do** give each object its fixed catalogue number and reuse it on every screen.
- **Do** show live figures in ruled tables and axis-labelled charts, with rolling numbers.
- **Do** state what an action will do, in plain French, before it does it; show the way back (Recycle Bin, restore) or say plainly that there is none.
- **Do** treat the web interface and the native overlay as one palette: tokens in `src/Treemap.Web/src/styles/tokens.css`, mirrored in `Themes/Palette.xaml` and in `pc3d/parts3d.tsx`.
- **Do** respect `prefers-reduced-motion`: all travel becomes a short fade, charts and the 3D view update without easing.

### Don't:
- **Don't** use Crest Yellow as text or a thin line on cream (1.08:1).
- **Don't** add shadows, blurs, glass, tinted gradients or glow, in the web interface or the overlay.
- **Don't** use hero-metric tiles, sparklines as content, or a grid of identical rounded cards with an icon tile above each heading.
- **Don't** number sections, steps or pages with the large numerals; they identify objects only.
- **Don't** let a colour carry meaning alone: a state is also written (a verdict tag, a dashed edge, a text legend).
- **Don't** put a primary action and a danger action side by side in the same state; irreversible actions need the typed word.
- **Don't** make the web page the authority on what may be deleted or closed: the engine decides and re-checks.
