import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { createMockMetrics } from '../bridge/mock'
import type { MetricsSnapshot } from '../bridge/types'
import { MoniteurPage } from './MoniteurPage'

function readings(count: number): MetricsSnapshot[] {
  const next = createMockMetrics()
  return Array.from({ length: count }, () => next())
}

describe('Moniteur screen', () => {
  it('waits for the first reading instead of showing empty charts', () => {
    render(<MoniteurPage latest={null} history={[]} />)

    expect(screen.getByRole('status')).toHaveTextContent('En attente des premières mesures')
  })

  it('draws one chart per resource, each readable without seeing it', () => {
    const history = readings(30)
    render(<MoniteurPage latest={history.at(-1)!} history={history} />)

    expect(screen.getByRole('img', { name: /Charge du processeur/ })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /Charge de la carte graphique/ })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /Occupation de la mémoire/ })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /Disque : lecture/ })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /Réseau : réception/ })).toBeInTheDocument()
  })

  it('explains when no supported graphics card could be read', () => {
    const history = readings(3).map((h) => ({ ...h, gpu: null }))
    render(<MoniteurPage latest={history.at(-1)!} history={history} />)

    expect(screen.getByText(/Aucune carte NVIDIA lue/)).toBeInTheDocument()
  })

  it('shows the overlay settings and applies a change through the engine', async () => {
    const user = userEvent.setup()
    const history = readings(5)
    render(<MoniteurPage latest={history.at(-1)!} history={history} />)

    const main = await screen.findByRole('switch', { name: /Afficher l’overlay par-dessus les jeux/ })
    expect(main).toHaveAttribute('aria-checked', 'false')
    expect(screen.getByText(/Raccourci pour l’afficher ou le masquer : Ctrl\+Alt\+F10/)).toBeInTheDocument()

    await user.click(main)
    await waitFor(() => expect(main).toHaveAttribute('aria-checked', 'true'))

    const lines = screen.getByRole('group', { name: 'Lignes affichées' })
    const memoryLine = within(lines).getByRole('switch', { name: 'Mémoire vidéo' })
    expect(memoryLine).toHaveAttribute('aria-checked', 'false')
    await user.click(memoryLine)
    await waitFor(() => expect(memoryLine).toHaveAttribute('aria-checked', 'true'))
  })

  it('moves the overlay to another corner of the screen', async () => {
    const user = userEvent.setup()
    const history = readings(5)
    render(<MoniteurPage latest={history.at(-1)!} history={history} />)

    const corners = await screen.findByRole('radiogroup', { name: /Coin de l’écran/ })
    expect(within(corners).getByRole('radio', { name: 'En haut à gauche' })).toBeChecked()

    await user.click(within(corners).getByRole('radio', { name: 'En bas à droite' }))
    await waitFor(() => expect(within(corners).getByRole('radio', { name: 'En bas à droite' })).toBeChecked())
    expect(within(corners).getByRole('radio', { name: 'En haut à gauche' })).not.toBeChecked()
  })

  it('shows the frame rate of the foreground application when the counter runs', async () => {
    const history = readings(5)
    render(<MoniteurPage latest={history.at(-1)!} history={history} />)

    expect(await screen.findByText(/dans FlightSimulator\.exe/)).toBeInTheDocument()
    expect(screen.getByText(/Mesure active pour les jeux DirectX 9 à 12/)).toBeInTheDocument()
  })
})
