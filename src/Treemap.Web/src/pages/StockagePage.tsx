import { AnimatePresence, motion } from 'motion/react'
import { ArrowUpRight, FolderOpen, HardDrive, ScanSearch, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import type { ChildDto, DeletionPreviewItem, DriveDto, HogDto, Verdict } from '../bridge/types'
import { Button } from '../components/Button'
import { DeleteDialog } from '../components/DeleteDialog'
import { PageHeader } from '../components/PageHeader'
import { RollingNumber } from '../components/RollingNumber'
import { RuledBar } from '../components/RuledBar'
import { Treemap } from '../components/Treemap'
import { useStorage } from '../hooks/useStorage'
import { formatBytes, formatFraction } from '../lib/format'
import './stockage.css'

const EASE = [0.25, 1, 0.5, 1] as const

const VERDICT_TEXT: Record<Verdict, string> = {
  allowed: 'Supprimable',
  uninstallOnly: 'Désinstaller',
  blocked: 'Protégé',
}

function Tag({ verdict }: { verdict: Verdict }) {
  return <span className={`tag tag--${verdict}`}>{VERDICT_TEXT[verdict]}</span>
}

/** The end of a long path is what identifies it: keep the last three folders. */
function shortPath(path: string): string {
  const parts = path.split('\\').filter(Boolean)
  return parts.length > 3 ? `…\\${parts.slice(-3).join('\\')}` : path
}

function parentOf(path: string): string {
  const trimmed = path.replace(/\\$/, '')
  const cut = trimmed.lastIndexOf('\\')
  return cut <= 2 ? trimmed.slice(0, cut + 1) : trimmed.slice(0, cut)
}

/** `#stockage?scan=C:\Jeux` (given by the host's --scan switch) starts an analysis as soon as the screen opens. */
function scanFromHash(): string | null {
  const query = window.location.hash.split('?')[1]
  return query ? new URLSearchParams(query).get('scan') : null
}

export function StockagePage() {
  const storage = useStorage()
  const { state } = storage
  const { scan } = storage

  useEffect(() => {
    const path = scanFromHash()
    if (path) void scan(path)
  }, [scan])

  return (
    <div className="stockage">
      <PageHeader
        title="Stockage"
        summary={
          state.phase === 'ready' && state.summary
            ? `${formatBytes(state.summary.sizeBytes)} dans ${state.summary.fileCount.toLocaleString('fr-FR')} fichiers, analysés en ${state.summary.elapsedSeconds.toLocaleString('fr-FR', { maximumFractionDigits: 1 })} s.`
            : 'Où partent les Go, et comment les récupérer sans risque.'
        }
        actions={
          state.phase === 'ready' && state.summary ? (
            <>
              <Button icon={<ScanSearch aria-hidden />} onClick={() => void storage.scan(state.summary!.rootPath)}>
                Réanalyser
              </Button>
              <Button icon={<HardDrive aria-hidden />} onClick={storage.backToDrives}>
                Autre disque
              </Button>
            </>
          ) : undefined
        }
      />

      {state.error && (
        <p className="status status--error" role="alert">
          {state.error}
        </p>
      )}

      <AnimatePresence mode="wait" initial={false}>
        <motion.div
          key={state.phase}
          initial={{ opacity: 0, y: 14 }}
          animate={{ opacity: 1, y: 0, transition: { duration: 0.28, ease: EASE } }}
          exit={{ opacity: 0, transition: { duration: 0.12 } }}
        >
          {state.phase === 'idle' && <DriveChooser drives={state.drives} onScan={storage.scan} onPick={storage.pickFolder} />}
          {state.phase === 'scanning' && <Scanning state={state} onCancel={storage.cancel} />}
          {state.phase === 'ready' && <Browser storage={storage} />}
        </motion.div>
      </AnimatePresence>
    </div>
  )
}

// ---- Choose what to analyse --------------------------------------------------------------------------------------

function DriveChooser({
  drives,
  onScan,
  onPick,
}: {
  drives: DriveDto[]
  onScan: (path: string) => void
  onPick: () => Promise<string | null>
}) {
  const pick = async () => {
    const path = await onPick()
    if (path) onScan(path)
  }

  return (
    <section aria-labelledby="choose-title" className="chooser">
      <h2 id="choose-title" className="chooser__title">
        Que faut-il analyser ?
      </h2>
      <ul className="chooser__list">
        {drives.map((drive, index) => {
          const used = drive.totalBytes - drive.freeBytes
          return (
            <li key={drive.path} className="drive">
              <span className="drive__letter">{drive.path.slice(0, 2)}</span>
              <div className="drive__bar">
                <RuledBar fraction={used / drive.totalBytes} label={`Occupation de ${drive.path}`} />
                <p className="drive__caption num">
                  {formatBytes(drive.freeBytes)} libres sur {formatBytes(drive.totalBytes)}
                  {drive.label ? ` · ${drive.label}` : ''}
                </p>
              </div>
              <Button variant={index === 0 ? 'primary' : 'secondary'} icon={<ScanSearch aria-hidden />} onClick={() => onScan(drive.path)}>
                Analyser {drive.path.slice(0, 2)}
              </Button>
            </li>
          )
        })}
      </ul>
      <Button variant="quiet" icon={<FolderOpen aria-hidden />} onClick={() => void pick()}>
        Choisir un dossier…
      </Button>
      <p className="chooser__note">
        L’analyse lit seulement les tailles. Rien n’est supprimé sans que tu aies vu la liste et confirmé.
      </p>
    </section>
  )
}

// ---- Scan in progress --------------------------------------------------------------------------------------------

function Scanning({ state, onCancel }: { state: ReturnType<typeof useStorage>['state']; onCancel: () => void }) {
  const drive = state.drives.find((d) => d.path === state.scanTarget)
  const used = drive ? drive.totalBytes - drive.freeBytes : 0
  const fraction = drive && used > 0 ? Math.min(1, (state.progress?.bytesScanned ?? 0) / used) : null

  return (
    <section aria-labelledby="scan-title" className="scanning">
      <h2 id="scan-title" className="chooser__title">
        Analyse de {state.scanTarget}
      </h2>
      <div className="scanning__figures" role="status" aria-live="polite">
        <div>
          <p className="scanning__value">
            <RollingNumber value={state.progress?.filesScanned ?? 0} format={(v) => Math.round(v).toLocaleString('fr-FR')} />
          </p>
          <p className="scanning__label">fichiers lus</p>
        </div>
        <div>
          <p className="scanning__value">
            <RollingNumber value={state.progress?.bytesScanned ?? 0} format={formatBytes} />
          </p>
          <p className="scanning__label">comptés</p>
        </div>
      </div>
      {fraction !== null && <RuledBar fraction={fraction} label="Avancement de l’analyse" />}
      <p className="scanning__path" aria-hidden="true">
        {state.progress?.currentPath ?? 'Démarrage…'}
      </p>
      <Button onClick={onCancel}>Annuler l’analyse</Button>
    </section>
  )
}

// ---- Browse, inspect, delete -------------------------------------------------------------------------------------

function Browser({ storage }: { storage: ReturnType<typeof useStorage> }) {
  const { state } = storage
  const node = state.node
  const [focused, setFocused] = useState<string | null>(null)
  const [checked, setChecked] = useState<ReadonlySet<string>>(new Set())
  const [detail, setDetail] = useState<DeletionPreviewItem | null>(null)
  const [dialogPaths, setDialogPaths] = useState<string[] | null>(null)

  const folderPath = node?.path ?? ''
  useEffect(() => {
    setFocused(null)
    setChecked(new Set())
  }, [folderPath])

  useEffect(() => {
    setDetail(null)
    if (!focused) return
    let cancelled = false
    storage
      .preview([focused])
      .then((p) => !cancelled && setDetail(p.items[0] ?? null))
      .catch(() => undefined)
    return () => {
      cancelled = true
    }
  }, [focused, storage.preview])

  const crumbs = useMemo(() => {
    if (!node || !state.summary) return []
    const root = state.summary.rootPath.replace(/\\$/, '')
    const relative = node.path.slice(root.length).split('\\').filter(Boolean)
    return relative.map((name, i) => ({ name, path: `${root}\\${relative.slice(0, i + 1).join('\\')}` }))
  }, [node, state.summary])

  if (!node || !state.summary) return null

  const items = node.children
  const focusedChild = items.find((c) => c.path === focused) ?? null
  const checkedBytes = items.filter((c) => checked.has(c.path)).reduce((sum, c) => sum + c.sizeBytes, 0)

  const toggle = (path: string) =>
    setChecked((current) => {
      const next = new Set(current)
      if (!next.delete(path)) next.add(path)
      return next
    })

  const enter = (child: ChildDto) => {
    if (child.hasChildren) void storage.open(child.path)
    else setFocused(child.path)
  }

  const goToHog = async (path: string) => {
    await storage.open(parentOf(path))
    setFocused(path)
  }

  return (
    <div className="browser">
      <div className="browser__main">
        <nav aria-label="Dossier affiché" className="crumbs">
          <button type="button" className="crumbs__item" onClick={() => void storage.open(state.summary!.rootPath)} aria-current={crumbs.length === 0 ? 'page' : undefined}>
            {state.summary.rootName}
          </button>
          {crumbs.map((crumb, i) => (
            <span key={crumb.path} className="crumbs__step">
              <span aria-hidden="true">›</span>
              <button type="button" className="crumbs__item" onClick={() => void storage.open(crumb.path)} aria-current={i === crumbs.length - 1 ? 'page' : undefined}>
                {crumb.name}
              </button>
            </span>
          ))}
          <span className="crumbs__total num">{formatBytes(node.sizeBytes)}</span>
        </nav>

        {items.length > 0 ? (
          <Treemap folderKey={node.path} items={items} focusedPath={focused} checked={checked} onFocus={setFocused} onOpen={enter} />
        ) : (
          <p className="coming">Ce dossier est vide.</p>
        )}

        <AnimatePresence initial={false} mode="wait">
          {focusedChild && (
            <motion.section
              key={focusedChild.path}
              className="detail"
              aria-label={`Détail de ${focusedChild.name}`}
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0, transition: { duration: 0.22, ease: EASE } }}
              exit={{ opacity: 0, transition: { duration: 0.1 } }}
            >
              <div className="detail__head">
                <h2 className="detail__name">{focusedChild.name}</h2>
                <Tag verdict={focusedChild.verdict} />
              </div>
              <p className="detail__figures num">
                {formatBytes(focusedChild.sizeBytes)} · {formatFraction(focusedChild.sizeBytes / Math.max(1, node.sizeBytes))} du dossier ·{' '}
                {focusedChild.fileCount.toLocaleString('fr-FR')} fichier{focusedChild.fileCount > 1 ? 's' : ''}
              </p>
              <p className="detail__path">{focusedChild.path}</p>
              {detail && (
                <p className="detail__note">
                  <strong>{detail.assessment.title}.</strong> {detail.assessment.note}
                </p>
              )}
              <div className="detail__actions">
                {focusedChild.hasChildren && (
                  <Button icon={<FolderOpen aria-hidden />} onClick={() => void storage.open(focusedChild.path)}>
                    Ouvrir
                  </Button>
                )}
                <Button icon={<ArrowUpRight aria-hidden />} onClick={() => void storage.reveal(focusedChild.path)}>
                  Afficher dans l’Explorateur
                </Button>
                {detail?.assessment.uninstall.map((hint) => (
                  <Button key={hint.uri} onClick={() => void storage.openUninstaller(focusedChild.path, hint.launcher)}>
                    {hint.label}
                  </Button>
                ))}
                {focusedChild.verdict === 'allowed' && (
                  <Button icon={<Trash2 aria-hidden />} onClick={() => setDialogPaths([focusedChild.path])}>
                    Supprimer…
                  </Button>
                )}
              </div>
            </motion.section>
          )}
        </AnimatePresence>

        <table className="listing">
          <caption className="visually-hidden">Contenu du dossier par taille décroissante</caption>
          <thead>
            <tr>
              <th scope="col" className="listing__check">
                <span className="visually-hidden">Sélection</span>
              </th>
              <th scope="col">Nom</th>
              <th scope="col" className="listing__share">Part</th>
              <th scope="col" className="listing__size">Taille</th>
              <th scope="col">État</th>
            </tr>
          </thead>
          <tbody>
            {items.map((child) => (
              <tr key={child.path} className={child.path === focused ? 'is-focused' : undefined}>
                <td className="listing__check">
                  <input
                    type="checkbox"
                    checked={checked.has(child.path)}
                    disabled={child.verdict !== 'allowed'}
                    onChange={() => toggle(child.path)}
                    aria-label={`Sélectionner ${child.name}`}
                  />
                </td>
                <td>
                  <button type="button" className="listing__name" onClick={() => setFocused(child.path)} onDoubleClick={() => enter(child)}>
                    {child.name}
                  </button>
                </td>
                <td className="listing__share">
                  <span className="share" aria-hidden="true">
                    <span className="share__fill" style={{ transform: `scaleX(${child.sizeBytes / Math.max(1, node.sizeBytes)})` }} />
                  </span>
                  <span className="visually-hidden">{formatFraction(child.sizeBytes / Math.max(1, node.sizeBytes))}</span>
                </td>
                <td className="listing__size num">{formatBytes(child.sizeBytes)}</td>
                <td>
                  <Tag verdict={child.verdict} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {node.hiddenCount > 0 && (
          <p className="detail__path">
            + {node.hiddenCount.toLocaleString('fr-FR')} autres éléments plus petits ({formatBytes(node.hiddenBytes)}).
          </p>
        )}
      </div>

      <aside className="hogs" aria-labelledby="hogs-title">
        <h2 id="hogs-title" className="hogs__title">
          À examiner
        </h2>
        {state.hogs.length === 0 ? (
          <p className="hogs__empty">Rien de volumineux n’a été repéré parmi les suspects habituels.</p>
        ) : (
          <ol className="hogs__list">
            {state.hogs.map((hog, index) => (
              <HogCard key={hog.ruleId} hog={hog} index={index + 1} total={state.summary!.sizeBytes} onGo={goToHog} />
            ))}
          </ol>
        )}
        {state.summary.notes.length > 0 && (
          <div className="hogs__notes">
            {state.summary.notes.map((note) => (
              <p key={note}>{note}</p>
            ))}
          </div>
        )}
      </aside>

      <AnimatePresence>
        {checked.size > 0 && (
          <motion.div
            className="selection-bar"
            role="region"
            aria-label="Sélection à supprimer"
            initial={{ y: '110%' }}
            animate={{ y: 0, transition: { duration: 0.3, ease: EASE } }}
            exit={{ y: '110%', transition: { duration: 0.2, ease: EASE } }}
          >
            <p>
              <strong className="num">{checked.size}</strong> élément{checked.size > 1 ? 's' : ''} · <span className="num">{formatBytes(checkedBytes)}</span>
            </p>
            <Button variant="quiet" onClick={() => setChecked(new Set())}>
              Tout désélectionner
            </Button>
            <Button variant="primary" icon={<Trash2 aria-hidden />} onClick={() => setDialogPaths([...checked])}>
              Supprimer…
            </Button>
          </motion.div>
        )}
      </AnimatePresence>

      {dialogPaths && (
        <DeleteDialog
          paths={dialogPaths}
          preview={storage.preview}
          remove={storage.remove}
          openUninstaller={storage.openUninstaller}
          openRecycleBin={storage.openRecycleBin}
          onClose={(deleted) => {
            setDialogPaths(null)
            if (deleted) {
              setChecked(new Set())
              setFocused(null)
            }
          }}
        />
      )}
    </div>
  )
}

function HogCard({ hog, index, total, onGo }: { hog: HogDto; index: number; total: number; onGo: (path: string) => void }) {
  return (
    <li className="hog">
      <div className="hog__head">
        <span className="hog__index" aria-hidden="true">
          {String(index).padStart(2, '0')}
        </span>
        <div>
          <h3 className="hog__title">{hog.title}</h3>
          <p className="hog__size num">
            {formatBytes(hog.sizeBytes)} · {formatFraction(hog.sizeBytes / Math.max(1, total))}
          </p>
        </div>
      </div>
      <p className="hog__text">{hog.explanation}</p>
      <p className="hog__text hog__text--advice">{hog.advice}</p>
      <ul className="hog__places">
        {hog.locations.map((place) => (
          <li key={place.path}>
            <button type="button" className="hog__place" onClick={() => onGo(place.path)} title={place.path}>
              <span className="hog__path">{shortPath(place.path)}</span>
              <span className="num">{formatBytes(place.sizeBytes)}</span>
            </button>
          </li>
        ))}
        {hog.moreCount > 0 && <li className="hog__more">… et {hog.moreCount} autres emplacements</li>}
      </ul>
    </li>
  )
}
