import { createMockMetrics, mockSpecs } from './mock'
import { createMockOverlay } from './mockOverlay'
import { createMockStorage } from './mockStorage'
import type { MetricsSnapshot, OverlaySettings, SystemSpecs } from './types'

/**
 * Message bridge to the C# engine. Inside the app (WebView2) messages travel as JSON through
 * `window.chrome.webview`; in a plain browser a mock answers, so the interface can be designed and tested alone.
 *
 * Wire format
 *   web -> host   { id, type, payload }
 *   host -> web   { kind: 'response', id, ok, payload | error }
 *                 { kind: 'event', type, payload }
 */

interface WebViewHost {
  postMessage(message: unknown): void
  addEventListener(type: 'message', listener: (event: { data: unknown }) => void): void
}

type Handler = (payload: unknown) => void

interface Envelope {
  kind: 'response' | 'event'
  id?: number
  type?: string
  ok?: boolean
  payload?: unknown
  error?: string
}

const listeners = new Map<string, Set<Handler>>()
const pending = new Map<number, { resolve: (value: unknown) => void; reject: (reason: Error) => void }>()
let nextId = 1
let wired = false
let mockTimer: ReturnType<typeof setInterval> | undefined

function host(): WebViewHost | undefined {
  return (window as unknown as { chrome?: { webview?: WebViewHost } }).chrome?.webview
}

export function isHosted(): boolean {
  return host() !== undefined
}

function emit(type: string, payload: unknown): void {
  listeners.get(type)?.forEach((handler) => handler(payload))
}

function wire(): void {
  const webview = host()
  if (wired || !webview) return
  wired = true
  webview.addEventListener('message', (event) => {
    const message = (typeof event.data === 'string' ? JSON.parse(event.data) : event.data) as Envelope
    if (message.kind === 'event' && message.type) {
      emit(message.type, message.payload)
    } else if (message.kind === 'response' && message.id !== undefined) {
      const call = pending.get(message.id)
      pending.delete(message.id)
      if (message.ok) call?.resolve(message.payload)
      else call?.reject(new Error(message.error ?? 'Erreur inconnue du moteur'))
    }
  })
}

/** Asks the engine for something and waits for the answer. */
export function request<T>(type: string, payload?: unknown): Promise<T> {
  const webview = host()
  if (!webview) return mockRequest(type, payload) as Promise<T>

  wire()
  return new Promise<T>((resolve, reject) => {
    const id = nextId++
    pending.set(id, { resolve: resolve as (value: unknown) => void, reject })
    webview.postMessage({ id, type, payload })
  })
}

/** Listens to events pushed by the engine (live metrics, scan progress...). Returns the unsubscribe function. */
export function subscribe<T>(type: string, handler: (payload: T) => void): () => void {
  wire()
  const set = listeners.get(type) ?? new Set<Handler>()
  set.add(handler as Handler)
  listeners.set(type, set)
  if (!host()) startMockEvents()

  return () => {
    set.delete(handler as Handler)
    if (!host() && ![...listeners.values()].some((s) => s.size > 0) && mockTimer) {
      clearInterval(mockTimer)
      mockTimer = undefined
    }
  }
}

function startMockEvents(): void {
  if (mockTimer) return
  const next = createMockMetrics()
  const tick = () => {
    emit('metrics', next() satisfies MetricsSnapshot)
    emit('fps', { processName: 'FlightSimulator.exe', fps: 52 + Math.random() * 14 })
  }
  mockTimer = setInterval(tick, 1000)
  tick()
}

const mockStorage = createMockStorage(emit)
const mockOverlay = createMockOverlay()

async function mockRequest(type: string, payload: unknown): Promise<unknown> {
  await new Promise((resolve) => setTimeout(resolve, 120))
  const body = (payload ?? {}) as { path?: string; paths?: string[]; confirmation?: string | null; permanent?: boolean }
  switch (type) {
    case 'specs.get':
      return mockSpecs satisfies SystemSpecs
    case 'overlay.get':
      return mockOverlay.get()
    case 'overlay.update':
      return mockOverlay.update(payload as OverlaySettings)
    case 'overlay.relaunchAsAdmin':
      return mockOverlay.relaunchAsAdmin()
    case 'storage.drives':
      return mockStorage.drives()
    case 'storage.scan':
      return mockStorage.scan()
    case 'storage.cancel':
    case 'storage.reveal':
    case 'storage.openUninstaller':
    case 'storage.openRecycleBin':
      return null
    case 'storage.pickFolder':
      return null
    case 'storage.node':
      return mockStorage.node(body.path ?? '')
    case 'storage.preview':
      return mockStorage.preview(body.paths ?? [], body.permanent ?? false)
    case 'storage.delete':
      return mockStorage.delete(body.paths ?? [], body.confirmation ?? null, body.permanent ?? false)
    case 'storage.recycleBin':
      return mockStorage.recycleBin()
    case 'storage.emptyRecycleBin':
      return mockStorage.emptyRecycleBin(body.confirmation ?? null)
    default:
      throw new Error(`Moteur indisponible hors de l'application : ${type}`)
  }
}
