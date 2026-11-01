import type { OverlayCorner } from '../bridge/types'
import './corner-picker.css'

const CORNERS: ReadonlyArray<{ value: OverlayCorner; label: string }> = [
  { value: 'topLeft', label: 'En haut à gauche' },
  { value: 'topRight', label: 'En haut à droite' },
  { value: 'bottomLeft', label: 'En bas à gauche' },
  { value: 'bottomRight', label: 'En bas à droite' },
]

interface CornerPickerProps {
  value: OverlayCorner
  onChange: (corner: OverlayCorner) => void
}

/** The screen in miniature: pick the corner where the overlay sits by pressing that corner. */
export function CornerPicker({ value, onChange }: CornerPickerProps) {
  return (
    <div className="corner-picker" role="radiogroup" aria-label="Coin de l’écran où s’affiche l’overlay">
      {CORNERS.map((corner) => (
        <button
          key={corner.value}
          type="button"
          role="radio"
          aria-checked={value === corner.value}
          aria-label={corner.label}
          className={`corner-picker__spot corner-picker__spot--${corner.value}`}
          onClick={() => onChange(corner.value)}
        />
      ))}
    </div>
  )
}
