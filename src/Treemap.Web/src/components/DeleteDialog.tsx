import { useEffect, useRef, useState } from 'react'
import type { DeleteReplyDto, DeletionPreview } from '../bridge/types'
import { formatBytes } from '../lib/format'
import { Button } from './Button'
import './delete-dialog.css'

interface DeleteDialogProps {
  paths: string[]
  preview: (paths: string[]) => Promise<DeletionPreview>
  remove: (paths: string[], confirmation: string | null) => Promise<DeleteReplyDto>
  openUninstaller: (path: string, launcher: string) => Promise<unknown>
  openRecycleBin: () => Promise<unknown>
  /** `deleted` is true when something was sent to the Recycle Bin, so the screen can clear its selection. */
  onClose: (deleted: boolean) => void
}

type Phase = 'loading' | 'ready' | 'working' | 'done' | 'failed'

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error)
}

/**
 * The only way out of the Stockage screen towards deletion. It shows what will happen before anything does:
 * every item with its size and what the engine says about it, the Recycle Bin guarantee, and a typed word when asked.
 */
export function DeleteDialog({ paths, preview, remove, openUninstaller, openRecycleBin, onClose }: DeleteDialogProps) {
  const dialog = useRef<HTMLDialogElement>(null)
  const [phase, setPhase] = useState<Phase>('loading')
  const [plan, setPlan] = useState<DeletionPreview | null>(null)
  const [reply, setReply] = useState<DeleteReplyDto | null>(null)
  const [typed, setTyped] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    dialog.current?.showModal()
  }, [])

  useEffect(() => {
    let cancelled = false
    preview(paths)
      .then((result) => {
        if (cancelled) return
        setPlan(result)
        setPhase('ready')
      })
      .catch((e) => {
        if (cancelled) return
        setError(messageOf(e))
        setPhase('failed')
      })
    return () => {
      cancelled = true
    }
  }, [paths, preview])

  const phrase = plan?.confirmationPhrase ?? null
  const confirmed = !phrase || typed.trim().toUpperCase() === phrase
  const busy = phase === 'working'
  const deletedSomething = !!reply && reply.results.some((r) => r.ok)

  const close = () => onClose(deletedSomething)

  const run = async () => {
    if (!plan?.canProceed || !confirmed) return
    setPhase('working')
    try {
      const result = await remove(paths, phrase ? typed.trim() : null)
      setReply(result)
      if (result.refused) {
        setError(result.reason)
        setPhase('failed')
      } else {
        setPhase('done')
      }
    } catch (e) {
      setError(messageOf(e))
      setPhase('failed')
    }
  }

  const blocked = plan?.items.filter((i) => i.assessment.verdict !== 'allowed') ?? []
  const failures = reply?.results.filter((r) => !r.ok) ?? []

  return (
    <dialog
      ref={dialog}
      className="delete-dialog"
      aria-labelledby="delete-title"
      onCancel={(e) => {
        if (busy) e.preventDefault()
        else close()
      }}
    >
      <div className="delete-dialog__body">
        <h2 id="delete-title" className="delete-dialog__title">
          {phase === 'done' ? 'Envoyé à la corbeille' : 'Envoyer à la corbeille ?'}
        </h2>

        {phase === 'loading' && <p role="status">Vérification de ce que contient la sélection…</p>}

        {plan && phase !== 'done' && (
          <>
            <ul className="delete-list">
              {plan.items.map((item) => (
                <li key={item.path} className={`delete-item delete-item--${item.assessment.verdict}`}>
                  <div className="delete-item__head">
                    <span className="delete-item__name">{item.name}</span>
                    <span className="delete-item__size num">{formatBytes(item.sizeBytes)}</span>
                  </div>
                  <p className="delete-item__path">{item.path}</p>
                  <p className="delete-item__note">
                    <strong>{item.assessment.title}.</strong> {item.assessment.note}
                  </p>
                  {item.assessment.uninstall.length > 0 && (
                    <div className="delete-item__actions">
                      {item.assessment.uninstall.map((hint) => (
                        <Button key={hint.uri} onClick={() => void openUninstaller(item.path, hint.launcher)}>
                          {hint.label}
                        </Button>
                      ))}
                    </div>
                  )}
                </li>
              ))}
            </ul>

            {plan.canProceed ? (
              <p className="delete-dialog__assurance">
                Les éléments vont dans la Corbeille de Windows : tu peux les y restaurer tant que tu ne la vides pas.
                L’espace n’est réellement libéré qu’une fois la Corbeille vidée.
              </p>
            ) : (
              <p className="delete-dialog__assurance delete-dialog__assurance--stop" role="alert">
                {blocked.length > 0
                  ? 'Au moins un élément ne peut pas être supprimé d’ici. Retire-le de la sélection, ou passe par son désinstalleur.'
                  : 'Rien à supprimer.'}
              </p>
            )}

            {phrase && plan.canProceed && (
              <label className="delete-dialog__confirm">
                <span>
                  Ce sont des données personnelles ou volumineuses. Tape <strong>{phrase}</strong> pour confirmer.
                </span>
                <input
                  value={typed}
                  onChange={(e) => setTyped(e.target.value)}
                  autoComplete="off"
                  spellCheck={false}
                  disabled={busy}
                />
              </label>
            )}
          </>
        )}

        {phase === 'done' && reply && (
          <>
            <p className="delete-dialog__done">
              <span className="num">{formatBytes(reply.freedBytes)}</span> partis dans la Corbeille. Ils ne libéreront de la place
              qu’une fois la Corbeille vidée.
            </p>
            {failures.length > 0 && (
              <ul className="delete-list" role="alert">
                {failures.map((f) => (
                  <li key={f.path} className="delete-item delete-item--blocked">
                    <p className="delete-item__path">{f.path}</p>
                    <p className="delete-item__note">Non supprimé : {f.error}</p>
                  </li>
                ))}
              </ul>
            )}
          </>
        )}

        {phase === 'failed' && error && (
          <p className="delete-dialog__assurance delete-dialog__assurance--stop" role="alert">
            {error}
          </p>
        )}

        <div className="delete-dialog__footer">
          {phase === 'done' ? (
            <>
              <Button onClick={() => void openRecycleBin()}>Ouvrir la Corbeille</Button>
              <Button variant="primary" onClick={close}>
                Terminé
              </Button>
            </>
          ) : (
            <>
              <Button onClick={close} disabled={busy}>
                Annuler
              </Button>
              <Button variant="primary" onClick={run} disabled={!plan?.canProceed || !confirmed || busy}>
                {busy ? 'Envoi en cours…' : plan ? `Envoyer ${formatBytes(plan.totalBytes)} à la corbeille` : 'Envoyer à la corbeille'}
              </Button>
            </>
          )}
        </div>
      </div>
    </dialog>
  )
}
