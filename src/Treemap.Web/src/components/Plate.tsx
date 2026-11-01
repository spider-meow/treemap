import { motion, type Variants } from 'motion/react'
import type { ReactNode } from 'react'
import './plate.css'

export type PlateTone = 'forest' | 'paper' | 'crest'

interface PlateProps {
  /** Stable catalogue number of the object. It follows the object across every screen. */
  index: string
  title: string
  tone?: PlateTone
  /** Grid span (of 12 columns) on the specimen sheet. */
  span?: 4 | 5 | 6 | 7 | 8 | 12
  children: ReactNode
}

export const plateVariants: Variants = {
  hidden: { opacity: 0, y: 14 },
  show: { opacity: 1, y: 0, transition: { duration: 0.36, ease: [0.25, 1, 0.5, 1] } },
}

/** A specimen plate: the object's catalogue number, its name, and ruled rows of facts. */
export function Plate({ index, title, tone = 'paper', span = 4, children }: PlateProps) {
  return (
    <motion.section
      className={`plate plate--${tone} plate--span-${span}${tone === 'forest' ? ' on-forest' : ''}`}
      variants={plateVariants}
      aria-labelledby={`plate-${index}`}
      data-index={index}
    >
      <header className="plate__head">
        <h2 id={`plate-${index}`} className="plate__title">
          {title}
        </h2>
        <span className="plate__index" aria-hidden="true">
          {index}
        </span>
      </header>
      <div className="plate__body">{children}</div>
    </motion.section>
  )
}

interface SpecListProps {
  rows: ReadonlyArray<{ label: string; value: ReactNode }>
  /** Lays the rows out in two columns (wide plates). */
  columns?: boolean
}

export function SpecList({ rows, columns = false }: SpecListProps) {
  return (
    <dl className={`specs${columns ? ' specs--columns' : ''}`}>
      {rows.map((row) => (
        <div className="specs__row" key={row.label}>
          <dt>{row.label}</dt>
          <dd>{row.value}</dd>
        </div>
      ))}
    </dl>
  )
}
