import { Maximize2, Minimize2 } from 'lucide-react'
import { AnimatePresence, motion } from 'motion/react'
import { useState } from 'react'
import type { SystemSpecs } from '../../bridge/types'
import type { Part, PartId } from '../../lib/parts'
import { Button } from '../Button'
import { SpecList } from '../Plate'
import { PcScene } from './PcScene'
import './exploded-view.css'

interface ExplodedViewProps {
  specs: SystemSpecs
  parts: Part[]
}

const reducedMotion = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false

/**
 * The PC taken apart on one drawing. The drawing is for looking and picking; the list beside it does the same job for
 * keyboards and screen readers, and the plate shows what the engine knows about the picked part.
 */
export default function ExplodedView({ specs, parts }: ExplodedViewProps) {
  const [exploded, setExploded] = useState(true)
  const [selectedId, setSelectedId] = useState<PartId | null>(null)
  const [hoveredId, setHoveredId] = useState<PartId | null>(null)
  const selected = parts.find((p) => p.id === selectedId) ?? null

  return (
    <div className="exploded-host">
    <div className="exploded">
      <div className="exploded__stage">
        <PcScene
          specs={specs}
          exploded={exploded}
          selected={selectedId}
          hovered={hoveredId}
          reducedMotion={reducedMotion()}
          onHover={setHoveredId}
          onSelect={setSelectedId}
        />
        <div className="exploded__tools">
          <Button
            icon={exploded ? <Minimize2 aria-hidden /> : <Maximize2 aria-hidden />}
            aria-pressed={exploded}
            onClick={() => setExploded((v) => !v)}
          >
            {exploded ? 'Assembler' : 'Éclater'}
          </Button>
        </div>
        <p className="exploded__hint">Glisse pour tourner, molette pour zoomer, clique une pièce pour la lire.</p>
      </div>

      <aside className="exploded__side">
        <ul className="parts-nav" aria-label="Pièces du PC">
          {parts.map((part) => (
            <li key={part.id}>
              <button
                type="button"
                className={`parts-nav__item${part.id === selectedId ? ' is-selected' : ''}${part.id === hoveredId ? ' is-hovered' : ''}`}
                aria-pressed={part.id === selectedId}
                onClick={() => setSelectedId(part.id === selectedId ? null : part.id)}
                onMouseEnter={() => setHoveredId(part.id)}
                onMouseLeave={() => setHoveredId(null)}
                onFocus={() => setHoveredId(part.id)}
                onBlur={() => setHoveredId(null)}
              >
                <span className="parts-nav__index">{part.index}</span>
                <span className="parts-nav__title">{part.title}</span>
              </button>
            </li>
          ))}
        </ul>

        <AnimatePresence mode="wait" initial={false}>
          {selected ? (
            <motion.article
              key={selected.id}
              className={`plate plate--${selected.tone}${selected.tone === 'forest' ? ' on-forest' : ''}`}
              aria-label={`Détail : ${selected.title}`}
              initial={{ opacity: 0, x: 28 }}
              animate={{ opacity: 1, x: 0, transition: { duration: 0.3, ease: [0.25, 1, 0.5, 1] } }}
              exit={{ opacity: 0, x: -14, transition: { duration: 0.14 } }}
            >
              <header className="plate__head">
                <h2 className="plate__title">{selected.title}</h2>
                <motion.span
                  className="plate__index"
                  aria-hidden="true"
                  initial={{ scale: 0.6, opacity: 0 }}
                  animate={{ scale: 1, opacity: 1, transition: { duration: 0.34, ease: [0.25, 1, 0.5, 1] } }}
                  style={{ transformOrigin: 'right bottom' }}
                >
                  {selected.index}
                </motion.span>
              </header>
              <div className="plate__body">
                <SpecList rows={selected.rows} />
              </div>
            </motion.article>
          ) : (
            <motion.p key="none" className="exploded__empty" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}>
              Choisis une pièce sur le dessin ou dans la liste : sa fiche s’affiche ici.
            </motion.p>
          )}
        </AnimatePresence>
      </aside>
    </div>
    </div>
  )
}
