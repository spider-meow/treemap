import type { ButtonHTMLAttributes, ReactNode } from 'react'
import './button.css'

type Variant = 'primary' | 'secondary' | 'quiet'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  icon?: ReactNode
}

/** One primary (orange) button per screen; everything else is secondary or quiet. */
export function Button({ variant = 'secondary', icon, children, className, ...rest }: ButtonProps) {
  return (
    <button type="button" className={`button button--${variant}${className ? ` ${className}` : ''}`} {...rest}>
      {icon}
      <span>{children}</span>
    </button>
  )
}
