import { Box, Check, Copy, LayoutGrid } from 'lucide-react'
import { motion } from 'motion/react'
import { Suspense, lazy, useEffect, useMemo, useState } from 'react'
import type { MetricsSnapshot, SystemSpecs } from '../bridge/types'
import { Button } from '../components/Button'
import { PageHeader } from '../components/PageHeader'
import { Plate, SpecList } from '../components/Plate'
import type { PageId } from '../components/Rail'
import { RuledBar } from '../components/RuledBar'
import { formatBytes, formatFraction, formatUptime } from '../lib/format'
import { CATALOG } from '../lib/names'
import { listParts, type Part, type PartId } from '../lib/parts'
import { buildSummary } from '../lib/summary'
import './composants.css'

// Three.js is large and only needed for the 3D view: it loads when that view is first opened.
const ExplodedView = lazy(() => import('../components/pc3d/ExplodedView'))

interface ComposantsPageProps {
  specs: SystemSpecs
  metrics: MetricsSnapshot | null
  onNavigate: (page: PageId) => void
}

type View = 'fiche' | 'vue3d'

const sheet = { hidden: {}, show: { transition: { staggerChildren: 0.06, delayChildren: 0.08 } } }

function describeMachine(specs: SystemSpecs): string {
  const parts: string[] = []
  if (specs.cpu) parts.push(`un processeur à ${specs.cpu.cores} cœurs`)
  if (specs.memory) parts.push(`${formatBytes(specs.memory.totalBytes)} de mémoire`)
  if (specs.gpus[0]) parts.push(`une ${specs.gpus[0].name}`)
  const storage = specs.disks.reduce((sum, d) => sum + d.sizeBytes, 0)
  if (storage > 0) parts.push(`${formatBytes(storage)} de stockage`)
  const text = parts.length > 1 ? `${parts.slice(0, -1).join(', ')} et ${parts.at(-1)}` : (parts[0] ?? '')
  return text ? `Ce PC embarque ${text}.` : ''
}

const SPANS: Record<PartId, 4 | 5 | 7> = { gpu: 7, cpu: 5, memory: 4, board: 4, storage: 4 }
/** Catalogue order on the sheet: the big plates first. */
const SHEET_ORDER: PartId[] = ['gpu', 'cpu', 'memory', 'board', 'storage']

export function ComposantsPage({ specs, metrics, onNavigate }: ComposantsPageProps) {
  const [copied, setCopied] = useState(false)
  const [view, setView] = useState<View>(() => (window.location.hash.includes('view=3d') ? 'vue3d' : 'fiche'))

  useEffect(() => {
    if (!copied) return
    const timer = setTimeout(() => setCopied(false), 2200)
    return () => clearTimeout(timer)
  }, [copied])

  const copySummary = async () => {
    await navigator.clipboard.writeText(buildSummary(specs))
    setCopied(true)
  }

  const gpuLive = metrics?.gpu ?? null
  const parts = useMemo(() => listParts(specs, gpuLive), [specs, gpuLive])
  const sheetParts = SHEET_ORDER.map((id) => parts.find((p) => p.id === id)).filter((p): p is Part => p !== undefined)

  return (
    <>
      <PageHeader
        title="Composants"
        summary={describeMachine(specs)}
        actions={
          <>
            <div className="view-switch" role="group" aria-label="Affichage des composants">
              <Button
                icon={<LayoutGrid aria-hidden />}
                aria-pressed={view === 'fiche'}
                className={view === 'fiche' ? 'is-active' : undefined}
                onClick={() => setView('fiche')}
              >
                Fiche
              </Button>
              <Button
                icon={<Box aria-hidden />}
                aria-pressed={view === 'vue3d'}
                className={view === 'vue3d' ? 'is-active' : undefined}
                onClick={() => setView('vue3d')}
              >
                Vue 3D
              </Button>
            </div>
            <Button variant="primary" icon={copied ? <Check aria-hidden /> : <Copy aria-hidden />} onClick={copySummary}>
              {copied ? 'Résumé copié' : 'Copier le résumé'}
            </Button>
          </>
        }
      />
      <span className="visually-hidden" role="status">
        {copied ? 'Résumé copié dans le presse-papiers' : ''}
      </span>

      {specs.volumes.map((volume) => (
        <section className="volume" key={volume.name} aria-label={`Disque ${volume.name}`}>
          <h2 className="volume__name">{volume.name.replace('\\', '')}</h2>
          <RuledBar fraction={volume.usedFraction} label={`Occupation du disque ${volume.name}`} />
          <p className="volume__detail num">
            {formatBytes(volume.freeBytes)} libres sur {formatBytes(volume.totalBytes)}
            <span className="volume__share">{formatFraction(volume.usedFraction)} occupés</span>
          </p>
          <Button variant="quiet" onClick={() => onNavigate('stockage')}>
            Voir où partent les Go
          </Button>
        </section>
      ))}

      {view === 'vue3d' ? (
        <Suspense fallback={<p className="status" role="status">Chargement de la vue 3D…</p>}>
          <ExplodedView specs={specs} parts={parts} />
        </Suspense>
      ) : (
        <motion.div className="sheet" variants={sheet} initial="hidden" animate="show">
          {sheetParts.map((part) => (
            <Plate key={part.id} index={part.index} title={part.title} tone={part.tone} span={SPANS[part.id]}>
              <SpecList rows={part.rows} />
            </Plate>
          ))}

          {specs.os && (
            <Plate index={CATALOG.system} title="Système" tone="paper" span={12}>
              <SpecList
                columns
                rows={[
                  { label: 'Windows', value: specs.os.name.replace('Microsoft ', '') },
                  { label: 'Version', value: `${specs.os.version} (build ${specs.os.build})` },
                  { label: 'Architecture', value: specs.os.architecture },
                  ...(specs.os.lastBoot
                    ? [{ label: 'Allumé depuis', value: formatUptime(Date.now() - new Date(specs.os.lastBoot).getTime()) }]
                    : []),
                ]}
              />
            </Plate>
          )}
        </motion.div>
      )}

      {specs.warnings.length > 0 && (
        <ul className="warnings">
          {specs.warnings.map((w) => (
            <li key={w}>{w}</li>
          ))}
        </ul>
      )}
    </>
  )
}
