import { useCallback, useEffect, useRef, useState } from 'react'
import { request, subscribe } from '../bridge/bridge'
import type { FpsReading, OverlaySettings, OverlayState } from '../bridge/types'

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error)
}

/** The overlay settings and the frame rate, kept in step with the engine (the hotkey can change them behind our back). */
export function useOverlay() {
  const [state, setState] = useState<OverlayState | null>(null)
  const [fps, setFps] = useState<FpsReading | null>(null)
  const [error, setError] = useState<string | null>(null)
  const latest = useRef<OverlayState | null>(null)

  const adopt = useCallback((next: OverlayState) => {
    latest.current = next
    setState(next)
  }, [])

  useEffect(() => {
    let cancelled = false
    request<OverlayState>('overlay.get')
      .then((initial) => !cancelled && adopt(initial))
      .catch((e) => !cancelled && setError(messageOf(e)))
    const offChanged = subscribe<OverlayState>('overlay.changed', adopt)
    const offFps = subscribe<FpsReading | null>('fps', setFps)
    return () => {
      cancelled = true
      offChanged()
      offFps()
    }
  }, [adopt])

  /** Changes some settings: shown at once, then confirmed (or corrected) by what the engine kept. */
  const update = useCallback(
    async (patch: Partial<OverlaySettings>) => {
      const current = latest.current
      if (!current) return
      const settings = { ...current.settings, ...patch }
      adopt({ ...current, settings })
      try {
        adopt(await request<OverlayState>('overlay.update', settings))
        setError(null)
      } catch (e) {
        adopt(current)
        setError(messageOf(e))
      }
    },
    [adopt],
  )

  const relaunchAsAdmin = useCallback(async () => {
    try {
      const started = await request<boolean>('overlay.relaunchAsAdmin')
      if (!started) setError('Le démarrage en administrateur a été refusé : l’appli continue comme avant.')
    } catch (e) {
      setError(messageOf(e))
    }
  }, [])

  return { state, fps, error, update, relaunchAsAdmin }
}
