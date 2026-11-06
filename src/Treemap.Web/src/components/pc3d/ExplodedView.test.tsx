import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { mockSpecs } from '../../bridge/mock'
import { listParts } from '../../lib/parts'
import { ComposantsPage } from '../../pages/ComposantsPage'
import ExplodedView from './ExplodedView'

// jsdom has no WebGL: the drawing is replaced by a stub that reports what the view tells it.
vi.mock('./PcScene', () => ({
  PcScene: (props: { exploded: boolean; selected: string | null; hovered: string | null }) => (
    <div data-testid="scene" data-exploded={String(props.exploded)} data-selected={props.selected ?? ''} data-hovered={props.hovered ?? ''} />
  ),
}))

const parts = listParts(mockSpecs, null)

describe('Exploded 3D view', () => {
  it('lists every part with its catalogue number and starts exploded with nothing picked', () => {
    render(<ExplodedView specs={mockSpecs} parts={parts} />)

    const list = screen.getByRole('list', { name: 'Pièces du PC' })
    expect(within(list).getAllByRole('button')).toHaveLength(parts.length)
    expect(within(list).getByRole('button', { name: /01\s*Processeur/ })).toBeInTheDocument()
    expect(screen.getByTestId('scene')).toHaveAttribute('data-exploded', 'true')
    expect(screen.getByTestId('scene')).toHaveAttribute('data-selected', '')
    expect(screen.getByText(/Choisis une pièce/)).toBeInTheDocument()
  })

  it('shows the plate of the picked part and passes the pick to the drawing', async () => {
    const user = userEvent.setup()
    render(<ExplodedView specs={mockSpecs} parts={parts} />)

    await user.click(screen.getByRole('button', { name: /02\s*Carte graphique/ }))

    const plate = await screen.findByRole('article', { name: 'Détail : Carte graphique' })
    expect(within(plate).getByText('NVIDIA GeForce RTX 3060')).toBeInTheDocument()
    expect(screen.getByTestId('scene')).toHaveAttribute('data-selected', 'gpu')
    expect(screen.getByRole('button', { name: /02\s*Carte graphique/ })).toHaveAttribute('aria-pressed', 'true')
  })

  it('puts the pick back when the same part is chosen again', async () => {
    const user = userEvent.setup()
    render(<ExplodedView specs={mockSpecs} parts={parts} />)
    const item = screen.getByRole('button', { name: /03\s*Mémoire/ })

    await user.click(item)
    await user.click(item)

    expect(screen.getByTestId('scene')).toHaveAttribute('data-selected', '')
  })

  it('tells the drawing which part is under the pointer or keyboard focus', async () => {
    const user = userEvent.setup()
    render(<ExplodedView specs={mockSpecs} parts={parts} />)

    await user.hover(screen.getByRole('button', { name: /05\s*Stockage/ }))
    expect(screen.getByTestId('scene')).toHaveAttribute('data-hovered', 'storage')

    await user.unhover(screen.getByRole('button', { name: /05\s*Stockage/ }))
    expect(screen.getByTestId('scene')).toHaveAttribute('data-hovered', '')
  })

  it('assembles and explodes the drawing', async () => {
    const user = userEvent.setup()
    render(<ExplodedView specs={mockSpecs} parts={parts} />)

    await user.click(screen.getByRole('button', { name: 'Assembler' }))
    expect(screen.getByTestId('scene')).toHaveAttribute('data-exploded', 'false')
    expect(screen.getByRole('button', { name: 'Éclater' })).toBeInTheDocument()
  })
})

describe('Composants page view switch', () => {
  it('shows the specimen sheet first and loads the 3D view on demand', async () => {
    const user = userEvent.setup()
    render(<ComposantsPage specs={mockSpecs} metrics={null} onNavigate={() => undefined} />)

    expect(screen.getByRole('button', { name: 'Fiche' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.queryByTestId('scene')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Vue 3D' }))
    expect(await screen.findByTestId('scene')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Vue 3D' })).toHaveAttribute('aria-pressed', 'true')

    await user.click(screen.getByRole('button', { name: 'Fiche' }))
    expect(screen.queryByTestId('scene')).not.toBeInTheDocument()
    expect(screen.getByText('Carte graphique')).toBeInTheDocument()
  })
})
