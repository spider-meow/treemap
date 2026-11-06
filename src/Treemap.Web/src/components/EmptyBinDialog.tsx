import { useEffect, useRef, useState } from 'react'
import type { RecycleBinInfo } from '../bridge/types'
import { formatBytes } from '../lib/format'
import { Button } from './Button'
import './delete-dialog.css'

/** Emptying the bin is permanent, so it asks for the same typed word as a forced deletion. */
export const CONFIRM_WORD = 'SUPPRIMER'

interface EmptyBinDialogProps {
  info: RecycleBinInfo
  onEmpty: (confirmation: string) => Promise<void>
  onClose: () => void
}

export function EmptyBinDialog({ info, onEmpty, onClose }: EmptyBinDialogProps) {
  const dialog = useRef<HTMLDialogElement>(null)
  const [typed, setTyped] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    dialog.current?.showModal()
  }, [])

  const confirmed = typed.trim().toUpperCase() === CONFIRM_WORD

  const run = async () => {
    if (!confirmed) return
    setBusy(true)
    try {
      await onEmpty(typed.trim())
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
      setBusy(false)
    }
  }

  return (
    <dialog
      ref={dialog}
      className="delete-dialog"
      aria-labelledby="empty-title"
      onCancel={(e) => {
        if (busy) e.preventDefault()
        else onClose()
      }}
    >
      <div className="delete-dialog__body">
        <h2 id="empty-title" className="delete-dialog__title">
          Vider la Corbeille ?
        </h2>
        <p className="delete-dialog__assurance delete-dialog__assurance--stop" role="alert">
          <span className="num">{info.itemCount.toLocaleString('fr-FR')}</span> élément{info.itemCount > 1 ? 's' : ''} (
          <span className="num">{formatBytes(info.sizeBytes)}</span>) seront supprimés définitivement, sur tous les disques. Impossible à annuler.
        </p>
        <label className="delete-dialog__confirm">
          <span>
            Pour confirmer, tape <strong>{CONFIRM_WORD}</strong>.
          </span>
          <input value={typed} onChange={(e) => setTyped(e.target.value)} autoComplete="off" spellCheck={false} disabled={busy} />
        </label>
        {error && (
          <p className="delete-dialog__assurance delete-dialog__assurance--stop" role="alert">
            {error}
          </p>
        )}
        <div className="delete-dialog__footer">
          <Button onClick={onClose} disabled={busy}>
            Annuler
          </Button>
          <Button variant="danger" onClick={run} disabled={!confirmed || busy}>
            {busy ? 'Vidage en cours…' : 'Vider la Corbeille'}
          </Button>
        </div>
      </div>
    </dialog>
  )
}
