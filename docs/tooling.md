# Outillage Claude installé

Installé au niveau du projet dans `.claude/` (n'affecte pas les autres projets). Sources lues avant installation :

- **Impeccable** : https://github.com/pbakaus/impeccable (v4.8.0, Apache 2.0)
- **Everything Claude Code** : https://github.com/worldflowai/everything-claude-code (fork de affaan-m/everything-claude-code, MIT)

## Impeccable (design d'interface)

Un skill unique, `.claude/skills/impeccable/`, avec des commandes de design : `/impeccable critique`, `audit`, `polish`, `bolder`, `quieter`, `layout`, `typeset`, `harden`, etc. Utile pour l'UI de treemap (vue treemap, overlay, tableaux de bord).

Premier usage : `/impeccable init` écrit `PRODUCT.md` (contexte produit) à la racine, puis `/impeccable document` pourra écrire `DESIGN.md` quand une UI existera.

**À savoir** :
- Le lanceur `scripts/impeccable` télécharge un binaire (le "moteur") au **premier lancement** dans `~/.impeccable/bin/`, vérifié par somme SHA-256 (`engine-checksums.txt`). Rien n'a été téléchargé ni exécuté à l'installation. Le téléchargement n'aura lieu que quand on lancera une commande `/impeccable`, et seulement avec ton accord.
- Le **hook de détection** automatique (scan après chaque édition de fichier UI) n'est **pas** installé : c'est de l'exécution de code à chaque édition, donc opt-in. Pour l'ajouter plus tard, voir `.claude/settings.json` du repo Impeccable ou `npx impeccable install`.
- Les 4 sous-agents Impeccable (documenter, finish-reviewer, etc.) ne sont pas installés non plus.

## Everything Claude Code (workflow de dev)

| Type | Éléments installés |
|---|---|
| Agents (`.claude/agents/`) | planner, architect, tdd-guide, code-reviewer, security-reviewer, build-error-resolver, e2e-runner, refactor-cleaner, doc-updater |
| Commandes (`.claude/commands/`) | `/plan`, `/tdd`, `/verify`, `/checkpoint`, `/build-fix`, `/e2e`, `/eval`, `/learn`, `/orchestrate`, `/refactor-clean`, `/test-coverage`, `/update-codemaps`, `/update-docs`, `/ecc-code-review` |
| Skills (`.claude/skills/`) | coding-standards, backend-patterns, frontend-patterns, tdd-workflow, verification-loop, eval-harness, continuous-learning, strategic-compact, `ecc-security-checklist` |

### Ajustements par rapport au repo d'origine

- **`code-review` → `ecc-code-review`** et **`security-review` → `ecc-security-checklist`** : renommés pour ne pas masquer les `/code-review` et `/security-review` intégrés.
- **Hooks non installés** (`hooks/`, `scripts/`) : ils tournent à chaque édition / fin de session (prettier, tsc, persistance de mémoire dans `~/.claude/sessions/`). Les skills `continuous-learning` et `strategic-compact` en dépendent pour leur partie automatique ; sans hook ils restent consultables mais passifs.
- **Règles (`rules/`) non installées** : elles supposent du JS/TS (immutabilité, zod, 80 % de couverture, Zed pour la revue). À adapter une fois la stack choisie plutôt que d'imposer des conventions qui ne collent pas.
- **Retirés car hors sujet** : skills `clickhouse-io` et `project-guidelines-example` (exemple Zenith), commande `/setup-pm` (dépend des scripts non copiés).
- **Exemples orientés web/Next.js** : `frontend-patterns` et `backend-patterns` parlent React/Next/Supabase ; à ignorer si la stack finale est C# ou Rust.

## Mise à jour / désinstallation

Tout est dans `.claude/`. Supprimer le dossier désinstalle l'ensemble ; pour mettre à jour, recopier depuis les repos sources.
