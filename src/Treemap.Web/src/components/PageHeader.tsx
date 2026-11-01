import type { ReactNode } from 'react'
import './page-header.css'

interface PageHeaderProps {
  title: string
  summary?: string
  actions?: ReactNode
}

export function PageHeader({ title, summary, actions }: PageHeaderProps) {
  return (
    <header className="page-header">
      <div className="page-header__text">
        <h1 className="page-header__title">{title}</h1>
        {summary && <p className="page-header__summary">{summary}</p>}
      </div>
      {actions && <div className="page-header__actions">{actions}</div>}
    </header>
  )
}
