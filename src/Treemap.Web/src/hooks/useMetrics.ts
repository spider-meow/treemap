import { useEffect, useState } from 'react'
import { subscribe } from '../bridge/bridge'
import type { MetricsSnapshot } from '../bridge/types'

export const HISTORY_LENGTH = 120

interface MetricsState {
  latest: MetricsSnapshot | null
  /** Oldest first, at most {@link HISTORY_LENGTH} readings (two minutes at one reading per second). */
  history: MetricsSnapshot[]
}

/** Live readings pushed by the engine, with a short history for graphs. */
export function useMetrics(): MetricsState {
  const [state, setState] = useState<MetricsState>({ latest: null, history: [] })

  useEffect(
    () =>
      subscribe<MetricsSnapshot>('metrics', (snapshot) =>
        setState((previous) => ({
          latest: snapshot,
          history: [...previous.history, snapshot].slice(-HISTORY_LENGTH),
        })),
      ),
    [],
  )

  return state
}
