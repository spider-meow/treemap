import { useEffect, useState } from 'react'
import { request } from '../bridge/bridge'
import type { SystemSpecs } from '../bridge/types'

interface SpecsState {
  specs: SystemSpecs | null
  loading: boolean
  error: string | null
}

/** Reads the machine description from the engine once. */
export function useSpecs(): SpecsState {
  const [state, setState] = useState<SpecsState>({ specs: null, loading: true, error: null })

  useEffect(() => {
    let cancelled = false
    request<SystemSpecs>('specs.get')
      .then((specs) => !cancelled && setState({ specs, loading: false, error: null }))
      .catch((error: Error) => !cancelled && setState({ specs: null, loading: false, error: error.message }))
    return () => {
      cancelled = true
    }
  }, [])

  return state
}
