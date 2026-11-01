import { afterEach, describe, expect, it, vi } from 'vitest'
import { isHosted, request, subscribe } from './bridge'
import type { MetricsSnapshot, SystemSpecs } from './types'

afterEach(() => {
  vi.useRealTimers()
})

describe('bridge outside the app (mock engine)', () => {
  it('is not hosted in a plain browser', () => {
    expect(isHosted()).toBe(false)
  })

  it('answers specs.get with realistic specs', async () => {
    const specs = await request<SystemSpecs>('specs.get')
    expect(specs.cpu?.cores).toBeGreaterThan(0)
    expect(specs.gpus[0].videoMemoryBytes).toBeGreaterThan(0)
    expect(specs.volumes[0].usedFraction).toBeCloseTo(specs.volumes[0].usedBytes / specs.volumes[0].totalBytes, 3)
  })

  it('rejects requests it cannot answer instead of hanging', async () => {
    await expect(request('storage.delete')).rejects.toThrow(/indisponible/)
  })

  it('pushes metrics to subscribers and stops after unsubscribe', () => {
    vi.useFakeTimers()
    const received: MetricsSnapshot[] = []
    const unsubscribe = subscribe<MetricsSnapshot>('metrics', (m) => received.push(m))

    expect(received).toHaveLength(1) // immediate first reading
    vi.advanceTimersByTime(3000)
    expect(received).toHaveLength(4)

    unsubscribe()
    vi.advanceTimersByTime(3000)
    expect(received).toHaveLength(4)

    expect(received[0].gpu?.temperatureCelsius).not.toBeNull()
    expect(received.every((m) => m.cpuLoadPercent >= 0 && m.cpuLoadPercent <= 100)).toBe(true)
  })
})
