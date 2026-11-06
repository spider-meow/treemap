import { useCallback, useEffect, useRef, useState } from 'react'
import { request, subscribe } from '../bridge/bridge'
import type { BoosterSettings, BoosterState } from '../bridge/types'

const REFRESH_MS = 4000

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error)
}

/** The game booster as the interface sees it: what is running, the settings, the session. Refreshed every few seconds. */
export function useBooster() {
  const [state, setState] = useState<BoosterState | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const latest = useRef<BoosterState | null>(null)

  const adopt = useCallback((next: BoosterState) => {
    latest.current = next
    setState(next)
  }, [])

  useEffect(() => {
    let cancelled = false
    const load = () =>
      request<BoosterState>('booster.get')
        .then((next) => !cancelled && adopt(next))
        .catch((e) => !cancelled && setError(messageOf(e)))
    void load()
    const timer = setInterval(load, REFRESH_MS)
    const off = subscribe<BoosterState>('booster.changed', adopt)
    return () => {
      cancelled = true
      clearInterval(timer)
      off()
    }
  }, [adopt])

  const run = useCallback(
    async (type: string, payload?: unknown) => {
      setBusy(true)
      try {
        adopt(await request<BoosterState>(type, payload))
        setError(null)
      } catch (e) {
        setError(messageOf(e))
      } finally {
        setBusy(false)
      }
    },
    [adopt],
  )

  /** Changes some settings: shown at once, then confirmed by what the engine kept (it drops protected names). */
  const update = useCallback(
    async (patch: Partial<BoosterSettings>) => {
      const current = latest.current
      if (!current) return
      const settings = { ...current.settings, ...patch }
      adopt({ ...current, settings })
      try {
        adopt(await request<BoosterState>('booster.update', settings))
        setError(null)
      } catch (e) {
        adopt(current)
        setError(messageOf(e))
      }
    },
    [adopt],
  )

  return {
    state,
    error,
    busy,
    update,
    start: (pid: number) => run('booster.start', { pid }),
    stop: () => run('booster.stop'),
    relaunch: () => run('booster.relaunch'),
  }
}
