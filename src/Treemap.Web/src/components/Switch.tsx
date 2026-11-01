import './switch.css'

interface SwitchProps {
  checked: boolean
  onChange: (checked: boolean) => void
  children: string
  /** Smaller line under the label. */
  hint?: string
}

/** An on/off control drawn as a ruled slot with a square slider; the state is always also written (aria and shape). */
export function Switch({ checked, onChange, children, hint }: SwitchProps) {
  return (
    <button type="button" role="switch" aria-checked={checked} className="switch" onClick={() => onChange(!checked)}>
      <span className="switch__track" aria-hidden="true">
        <span className="switch__thumb" />
      </span>
      <span className="switch__text">
        <span className="switch__label">{children}</span>
        {hint && <span className="switch__hint">{hint}</span>}
      </span>
    </button>
  )
}
