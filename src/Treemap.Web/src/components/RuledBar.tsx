import { levelOf } from '../lib/format'
import './ruled-bar.css'

interface RuledBarProps {
  /** 0 to 1 */
  fraction: number
  label: string
  /** Drawn on a green field (the rail) instead of on cream. */
  onForest?: boolean
}

/** A usage bar drawn like a ruler: outlined track, tick every 10 %, orange fill once usage is high. */
export function RuledBar({ fraction, label, onForest = false }: RuledBarProps) {
  const clamped = Math.min(1, Math.max(0, fraction))
  return (
    <div
      className={`ruled-bar${onForest ? ' ruled-bar--on-forest' : ''}`}
      role="meter"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={Math.round(clamped * 100)}
      data-level={levelOf(clamped)}
    >
      <div className="ruled-bar__fill" style={{ transform: `scaleX(${clamped})` }} />
      <div className="ruled-bar__ticks" aria-hidden="true" />
    </div>
  )
}
