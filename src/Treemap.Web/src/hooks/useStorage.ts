import { useCallback, useEffect, useRef, useState } from 'react'
import { request, subscribe } from '../bridge/bridge'
import type {
  DeleteReplyDto,
  DeletionPreview,
  DriveDto,
  HogDto,
  NodeDto,
  ScanProgressDto,
  ScanReplyDto,
  RecycleBinInfo,
  ScanSummaryDto,
} from '../bridge/types'

export type StoragePhase = 'idle' | 'scanning' | 'ready'

export interface StorageState {
  phase: StoragePhase
  drives: DriveDto[]
  progress: ScanProgressDto | null
  scanTarget: string | null
  summary: ScanSummaryDto | null
  hogs: HogDto[]
  node: NodeDto | null
  error: string | null
}

const INITIAL: StorageState = {
  phase: 'idle',
  drives: [],
  progress: null,
  scanTarget: null,
  summary: null,
  hogs: [],
  node: null,
  error: null,
}

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error)
}

/** Everything the Stockage screen needs from the engine: drives, one scan at a time, browsing, and checked deletion. */
export function useStorage() {
  const [state, setState] = useState<StorageState>(INITIAL)
  const [bin, setBin] = useState<RecycleBinInfo | null>(null)
  const nodeRef = useRef<NodeDto | null>(null)
  const rootRef = useRef<string | null>(null)

  useEffect(() => {
    let cancelled = false
    request<DriveDto[]>('storage.drives')
      .then((drives) => !cancelled && setState((s) => ({ ...s, drives })))
      .catch((error) => !cancelled && setState((s) => ({ ...s, error: messageOf(error) })))
    void refreshBin()
    const unsubscribe = subscribe<ScanProgressDto>('storage.progress', (progress) =>
      setState((s) => (s.phase === 'scanning' ? { ...s, progress } : s)),
    )
    return () => {
      cancelled = true
      unsubscribe()
    }
  }, [])

  const refreshBin = useCallback(async () => {
    try {
      setBin(await request<RecycleBinInfo>('storage.recycleBin'))
    } catch {
      setBin(null)
    }
  }, [])

  const emptyBin = useCallback(async (confirmation: string) => {
    setBin(await request<RecycleBinInfo>('storage.emptyRecycleBin', { confirmation }))
  }, [])

  /** Shows a folder. Returns false (and says why when asked to) if the engine no longer has it. */
  const open = useCallback(async (path: string, report = true) => {
    try {
      const node = await request<NodeDto>('storage.node', { path })
      nodeRef.current = node
      setState((s) => ({ ...s, node, error: null }))
      return true
    } catch (error) {
      if (report) setState((s) => ({ ...s, error: messageOf(error) }))
      return false
    }
  }, [])

  const scan = useCallback(
    async (path: string) => {
      setState((s) => ({ ...s, phase: 'scanning', scanTarget: path, progress: null, error: null }))
      try {
        const reply = await request<ScanReplyDto>('storage.scan', { path })
        if (reply.cancelled || !reply.summary) {
          setState((s) => ({ ...s, phase: 'idle' }))
          return
        }
        rootRef.current = reply.summary.rootPath
        setState((s) => ({ ...s, phase: 'ready', summary: reply.summary, hogs: reply.hogs }))
        await open(reply.summary.rootPath)
      } catch (error) {
        setState((s) => ({ ...s, phase: 'idle', error: messageOf(error) }))
      }
    },
    [open],
  )

  const cancel = useCallback(() => {
    void request('storage.cancel')
  }, [])

  const backToDrives = useCallback(() => {
    nodeRef.current = null
    rootRef.current = null
    setState((s) => ({ ...s, phase: 'idle', node: null, summary: null, hogs: [] }))
  }, [])

  const preview = useCallback(
    (paths: string[], permanent = false) => request<DeletionPreview>('storage.preview', { paths, permanent }),
    [],
  )

  const remove = useCallback(
    async (paths: string[], confirmation: string | null, permanent = false) => {
      const reply = await request<DeleteReplyDto>('storage.delete', { paths, confirmation, permanent })
      if (!reply.refused) {
        void refreshBin()
        setState((s) => ({
          ...s,
          hogs: reply.hogs,
          summary: s.summary ? { ...s.summary, sizeBytes: reply.rootSizeBytes } : s.summary,
        }))
        // The folder on screen may be the one just deleted: fall back to its parent, then to the scan root.
        const here = nodeRef.current
        const candidates = [here?.path, here?.parentPath, rootRef.current].filter((p): p is string => !!p)
        for (const path of candidates) {
          if (await open(path, false)) break
        }
      }
      return reply
    },
    [open, refreshBin],
  )

  const reveal = useCallback((path: string) => request('storage.reveal', { path }), [])

  const openUninstaller = useCallback(
    (path: string, launcher: string) => request('storage.openUninstaller', { path, launcher }),
    [],
  )

  const openRecycleBin = useCallback(() => request('storage.openRecycleBin'), [])

  const pickFolder = useCallback(() => request<string | null>('storage.pickFolder'), [])

  return { state, bin, emptyBin, scan, cancel, open, backToDrives, preview, remove, reveal, openUninstaller, openRecycleBin, pickFolder }
}
