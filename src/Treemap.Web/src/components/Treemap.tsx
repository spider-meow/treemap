import { motion } from 'motion/react'
import { useMemo } from 'react'
import type { ChildDto } from '../bridge/types'
import { useSize } from '../hooks/useSize'
import { formatBytes } from '../lib/format'
import { squarify } from '../lib/squarify'
import './treemap.css'

/** More cells than this are unreadable and slow: the rest is folded into one inert cell. */
const MAX_CELLS = 90

interface TreemapProps {
  /** Changes when the folder shown changes, so the cells arrive again. */
  folderKey: string
  items: ChildDto[]
  focusedPath: string | null
  checked: ReadonlySet<string>
  onFocus: (path: string) => void
  onOpen: (item: ChildDto) => void
}

const VERDICT_LABEL = {
  allowed: 'supprimable',
  uninstallOnly: 'à désinstaller depuis son lanceur',
  blocked: 'protégé',
} as const

/**
 * The folder drawn as a catalogue plate: every entry a cell whose area is its share of the folder.
 * A click picks an entry, a double click (or Enter on the detail) goes inside.
 */
export function Treemap({ folderKey, items, focusedPath, checked, onFocus, onOpen }: TreemapProps) {
  const [ref, size] = useSize<HTMLDivElement>()

  const { cells, restBytes } = useMemo(() => {
    const shown = items.slice(0, MAX_CELLS)
    const rest = items.slice(MAX_CELLS).reduce((sum, i) => sum + i.sizeBytes, 0)
    const laid = squarify(
      shown.map((item) => ({ item, weight: item.sizeBytes })),
      { x: 0, y: 0, w: size.w, h: size.h },
    )
    return { cells: laid, restBytes: rest }
  }, [items, size.w, size.h])

  return (
    <div ref={ref} className="treemap" role="group" aria-label="Contenu du dossier, une case par élément">
      <motion.div key={folderKey} className="treemap__layer" initial="hidden" animate="shown" variants={{ shown: { transition: { staggerChildren: 0.012 } } }}>
        {cells.map(({ item, rect }) => {
          const isFocused = item.path === focusedPath
          const isChecked = checked.has(item.path)
          const roomy = rect.w >= 92 && rect.h >= 46
          return (
            <motion.button
              key={item.path}
              type="button"
              className={`tm-cell tm-cell--${item.verdict}${item.kind === 'folder' ? ' tm-cell--folder' : ''}${isFocused ? ' is-focused' : ''}${isChecked ? ' is-checked' : ''}`}
              style={{ left: rect.x, top: rect.y, width: rect.w, height: rect.h }}
              variants={{ hidden: { opacity: 0, scale: 0.94 }, shown: { opacity: 1, scale: 1, transition: { duration: 0.32, ease: [0.25, 1, 0.5, 1] } } }}
              onClick={() => onFocus(item.path)}
              onDoubleClick={() => item.hasChildren && onOpen(item)}
              aria-pressed={isFocused}
              aria-label={`${item.name}, ${formatBytes(item.sizeBytes)}, ${VERDICT_LABEL[item.verdict]}`}
              title={`${item.name} · ${formatBytes(item.sizeBytes)}`}
            >
              {roomy && (
                <span className="tm-cell__label">
                  <span className="tm-cell__name">{item.name}</span>
                  {rect.h >= 62 && <span className="tm-cell__size num">{formatBytes(item.sizeBytes)}</span>}
                </span>
              )}
            </motion.button>
          )
        })}
      </motion.div>
      {restBytes > 0 && (
        <p className="treemap__rest num">+ {items.length - MAX_CELLS} petits éléments, {formatBytes(restBytes)}</p>
      )}
    </div>
  )
}
