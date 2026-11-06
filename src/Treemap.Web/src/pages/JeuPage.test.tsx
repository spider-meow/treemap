import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { resetMockBooster } from '../bridge/bridge'
import { JeuPage } from './JeuPage'

/** The whole screen against the mock engine: the same messages the C# booster answers. */

describe('Jeu screen', () => {
  beforeEach(resetMockBooster)

  it('lists the running game and the applications, with the safe defaults', async () => {
    render(<JeuPage />)

    expect(await screen.findByRole('button', { name: 'Booster eldenring' })).toBeInTheDocument()
    expect(screen.getByRole('switch', { name: /Fermer les applications cochées/ })).toHaveAttribute('aria-checked', 'true')
    expect(screen.getByRole('switch', { name: /Démarrer tout seul/ })).toHaveAttribute('aria-checked', 'false')
    expect(screen.getByText(/ne sont jamais touchés/)).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Fermer Discord avant de jouer' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Fermer Obsidian avant de jouer' })).not.toBeChecked()
  })

  it('shows how much memory the ticked applications hold', async () => {
    const user = userEvent.setup()
    render(<JeuPage />)
    await screen.findByRole('button', { name: 'Booster eldenring' })

    expect(screen.getByText(/2 cochées, environ/)).toBeInTheDocument()

    await user.click(screen.getByRole('checkbox', { name: 'Fermer Obsidian avant de jouer' }))
    await waitFor(() => expect(screen.getByText(/3 cochées, environ/)).toBeInTheDocument())
  })

  it('runs a session, measures the effect and ends it with the previous plan back', async () => {
    const user = userEvent.setup()
    render(<JeuPage />)

    await user.click(await screen.findByRole('button', { name: 'Booster eldenring' }))

    expect(await screen.findByText(/tourne depuis/)).toBeInTheDocument()
    expect(screen.getByText(/Priorité haute pour le jeu pendant la partie/)).toBeInTheDocument()
    expect(screen.getByText(/2 sur 2 fermées/)).toBeInTheDocument()
    expect(screen.getByText(/Résultat dans quelques secondes/)).toBeInTheDocument()

    expect(await screen.findByText(/12,9 Go → 10,7 Go \(−2,2 Go\)/, undefined, { timeout: 6000 })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Terminer la session' }))

    expect(await screen.findByText(/Plan d’alimentation remis sur « Équilibré »/)).toBeInTheDocument()
    expect(screen.getByText(/Fermées pour la partie : chrome, Discord/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Relancer les applications fermées' }))
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Relancer les applications fermées' })).not.toBeInTheDocument())
  }, 15000)

  it('turns the booster actions off from the settings', async () => {
    const user = userEvent.setup()
    render(<JeuPage />)
    const priority = await screen.findByRole('switch', { name: /Priorité haute pour le jeu/ })

    await user.click(priority)
    await waitFor(() => expect(priority).toHaveAttribute('aria-checked', 'false'))

    await user.click(screen.getByRole('button', { name: 'Booster eldenring' }))
    const line = (await screen.findByText(/Priorité du jeu/)).closest('div')!
    expect(within(line).getByText(/Non · Désactivé dans les réglages/)).toBeInTheDocument()
  })

  it('remembers a program as a game and lets it be removed again', async () => {
    const user = userEvent.setup()
    render(<JeuPage />)

    const row = (await screen.findByRole('checkbox', { name: 'Fermer Obsidian avant de jouer' })).closest('li')!
    await user.click(within(row).getByRole('button', { name: 'C’est un jeu' }))

    expect(await screen.findByText('Obsidian.exe')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Retirer' }))
    await waitFor(() => expect(screen.queryByText('Obsidian.exe')).not.toBeInTheDocument())
  })
})
