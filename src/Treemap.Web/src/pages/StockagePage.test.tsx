import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { StockagePage } from './StockagePage'

/** The whole screen against the mock engine: the same messages the C# engine answers. */

async function scanSystemDrive() {
  const user = userEvent.setup()
  render(<StockagePage />)
  await user.click(await screen.findByRole('button', { name: 'Analyser C:' }))
  await screen.findByRole('heading', { name: 'À examiner' }, { timeout: 5000 })
  return user
}

/** Picks a row by its name, then goes inside through the detail band (a single click only selects). */
async function enterFolder(user: ReturnType<typeof userEvent.setup>, name: string) {
  await user.click(await screen.findByRole('button', { name }))
  const detail = await screen.findByRole('region', { name: `Détail de ${name}` })
  await user.click(within(detail).getByRole('button', { name: 'Ouvrir' }))
}

describe('Stockage screen', () => {
  it('offers the drives first and explains nothing is deleted without confirmation', async () => {
    render(<StockagePage />)

    expect(await screen.findByRole('button', { name: 'Analyser C:' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Analyser D:' })).toBeInTheDocument()
    expect(screen.getByText(/Rien n’est supprimé sans que tu aies vu la liste/)).toBeInTheDocument()
  })

  it('scans, then shows the folder, the suspects and the contents', async () => {
    await scanSystemDrive()

    expect(screen.getByRole('navigation', { name: 'Dossier affiché' })).toBeInTheDocument()
    expect(screen.getByRole('table')).toBeInTheDocument()
    expect(screen.getByText('Bibliothèques de jeux')).toBeInTheDocument()
    expect(screen.getByText(/Windows compte/)).toBeInTheDocument()
  }, 10000)

  it('never lets a protected or uninstall-only item be ticked for deletion', async () => {
    await scanSystemDrive()

    expect(screen.getByRole('checkbox', { name: 'Sélectionner Windows' })).toBeDisabled()
    expect(screen.getByRole('checkbox', { name: 'Sélectionner Jeux' })).toBeEnabled()
  }, 10000)

  it('previews, then sends the ticked item to the Recycle Bin and updates the suspects', async () => {
    const user = await scanSystemDrive()

    await user.click(screen.getByRole('checkbox', { name: 'Sélectionner Jeux' }))
    const bar = await screen.findByRole('region', { name: 'Sélection à supprimer' })
    await user.click(within(bar).getByRole('button', { name: /Supprimer/ }))

    const dialog = await screen.findByRole('dialog', { hidden: true })
    expect(await within(dialog).findByText(/Les éléments vont dans la Corbeille/)).toBeInTheDocument()
    expect(within(dialog).getByText(/tant que tu ne la vides pas/)).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: /Envoyer .* à la corbeille/ }))

    expect(await within(dialog).findByText(/partis dans la Corbeille/)).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Terminé' }))

    await waitFor(() => expect(screen.queryByRole('checkbox', { name: 'Sélectionner Jeux' })).not.toBeInTheDocument())
    expect(screen.queryByText('Bibliothèques de jeux')).not.toBeInTheDocument()
  }, 15000)

  it('forces a permanent deletion only after the warning and the typed word', async () => {
    const user = await scanSystemDrive()

    await user.click(screen.getByRole('checkbox', { name: 'Sélectionner Jeux' }))
    const bar = await screen.findByRole('region', { name: 'Sélection à supprimer' })
    await user.click(within(bar).getByRole('button', { name: /Supprimer/ }))
    const dialog = await screen.findByRole('dialog', { hidden: true })

    await user.click(await within(dialog).findByRole('switch', { name: /Supprimer définitivement/ }))
    expect(await within(dialog).findByText(/Irréversible : ces éléments ne pourront pas être restaurés/)).toBeInTheDocument()

    const confirm = await within(dialog).findByRole('button', { name: /définitivement$/ })
    expect(confirm).toBeDisabled()
    await user.type(await within(dialog).findByRole('textbox'), 'supprimer')
    await waitFor(() => expect(confirm).toBeEnabled())
    await user.click(confirm)

    expect(await within(dialog).findByText(/supprimés définitivement : l’espace est libéré/)).toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: 'Ouvrir la Corbeille' })).not.toBeInTheDocument()
  }, 15000)

  it('shows what the Recycle Bin holds and empties it behind the typed word', async () => {
    const user = userEvent.setup()
    render(<StockagePage />)

    const strip = await screen.findByRole('region', { name: 'Corbeille' })
    expect(within(strip).getByText(/L’espace n’est libéré qu’en la vidant/)).toBeInTheDocument()

    await user.click(within(strip).getByRole('button', { name: /Vider la Corbeille/ }))
    const dialog = await screen.findByRole('dialog', { hidden: true })
    const confirm = within(dialog).getByRole('button', { name: 'Vider la Corbeille' })
    expect(confirm).toBeDisabled()

    await user.type(within(dialog).getByRole('textbox'), 'SUPPRIMER')
    await user.click(confirm)

    await waitFor(() => expect(screen.queryByRole('region', { name: 'Corbeille' })).not.toBeInTheDocument())
  }, 15000)

  it('refuses to proceed until the typed word is entered for personal data', async () => {
    const user = await scanSystemDrive()

    await enterFolder(user, 'Users')
    await enterFolder(user, 'arthu')
    await user.click(await screen.findByRole('checkbox', { name: 'Sélectionner Documents' }))
    await user.click(within(await screen.findByRole('region', { name: 'Sélection à supprimer' })).getByRole('button', { name: /Supprimer/ }))

    const dialog = await screen.findByRole('dialog', { hidden: true })
    const confirm = await within(dialog).findByRole('button', { name: /Envoyer .* à la corbeille/ })
    expect(confirm).toBeDisabled()

    await user.type(within(dialog).getByRole('textbox'), 'supprimer')
    expect(confirm).toBeEnabled()
  }, 15000)

  it('points an installed game to its uninstaller instead of offering deletion', async () => {
    const user = await scanSystemDrive()

    await enterFolder(user, 'Jeux')
    await enterFolder(user, 'steamapps')
    await enterFolder(user, 'common')
    await user.click(await screen.findByRole('button', { name: 'Hades' }))

    const detail = await screen.findByRole('region', { name: 'Détail de Hades' })
    expect(await within(detail).findByRole('button', { name: 'Désinstaller dans Steam' })).toBeInTheDocument()
    expect(within(detail).queryByRole('button', { name: /Supprimer/ })).not.toBeInTheDocument()
  }, 15000)
})
