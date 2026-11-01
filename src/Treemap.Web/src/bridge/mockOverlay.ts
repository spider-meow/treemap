import type { OverlaySettings, OverlayState } from './types'

/** A stand-in for the overlay controller, so the settings can be designed and tested in a plain browser. */
export function createMockOverlay() {
  let state: OverlayState = {
    settings: {
      enabled: false,
      showFps: true,
      showCpu: true,
      showGpu: true,
      showGpuTemperature: true,
      showMemory: true,
      showVideoMemory: false,
      corner: 'topLeft',
      opacity: 0.85,
    },
    minOpacity: 0.3,
    fpsStatus: 'running',
    fpsMessage: null,
    canRelaunchAsAdmin: false,
    hotkey: { registered: true, description: 'Ctrl+Alt+F10' },
  }

  return {
    get: () => state,
    update(settings: OverlaySettings): OverlayState {
      state = { ...state, settings: { ...settings, opacity: Math.min(1, Math.max(state.minOpacity, settings.opacity)) } }
      return state
    },
    relaunchAsAdmin: () => true,
  }
}
