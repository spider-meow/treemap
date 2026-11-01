import { Activity, Cpu, Gamepad2, HardDrive } from 'lucide-react'
import { motion } from 'motion/react'
import type { ComponentType } from 'react'
import { formatBytes, formatFraction } from '../lib/format'
import type { VolumeInfo } from '../bridge/types'
import { RuledBar } from './RuledBar'
import './rail.css'

export type PageId = 'composants' | 'stockage' | 'moniteur' | 'jeu'

const ITEMS: ReadonlyArray<{ id: PageId; label: string; Icon: ComponentType<{ 'aria-hidden'?: boolean }> }> = [
  { id: 'composants', label: 'Composants', Icon: Cpu },
  { id: 'stockage', label: 'Stockage', Icon: HardDrive },
  { id: 'moniteur', label: 'Moniteur', Icon: Activity },
  { id: 'jeu', label: 'Jeu', Icon: Gamepad2 },
]

interface RailProps {
  page: PageId
  onNavigate: (page: PageId) => void
  /** The system drive, shown as a ruled bar at the foot of the rail. */
  volume: VolumeInfo | undefined
}

export function Rail({ page, onNavigate, volume }: RailProps) {
  return (
    <aside className="rail">
      <p className="rail__brand">treemap</p>

      <nav aria-label="Écrans" className="rail__nav">
        {ITEMS.map(({ id, label, Icon }) => (
          <button
            key={id}
            type="button"
            className="rail__item"
            aria-current={page === id ? 'page' : undefined}
            onClick={() => onNavigate(id)}
          >
            {page === id && (
              <motion.span
                layoutId="rail-tab"
                className="rail__tab"
                transition={{ duration: 0.28, ease: [0.25, 1, 0.5, 1] }}
              />
            )}
            <Icon aria-hidden />
            <span className="rail__label">{label}</span>
          </button>
        ))}
      </nav>

      {volume && (
        <div className="rail__foot">
          <p className="rail__volume">
            <span>{volume.name.replace('\\', '')}</span>
            <span className="num">{formatFraction(volume.usedFraction)}</span>
          </p>
          <RuledBar fraction={volume.usedFraction} label={`Occupation du disque ${volume.name}`} onForest />
          <p className="rail__free num">{formatBytes(volume.freeBytes)} libres</p>
        </div>
      )}
    </aside>
  )
}
