import { describe, expect, it } from 'vitest'
import { mockSpecs } from '../bridge/mock'
import type { SystemSpecs } from '../bridge/types'
import { listParts } from './parts'

describe('listParts', () => {
  it('lists the parts the machine has, each with its stable catalogue number', () => {
    const parts = listParts(mockSpecs, null)

    expect(parts.map((p) => [p.id, p.index])).toEqual([
      ['cpu', '01'],
      ['gpu', '02'],
      ['memory', '03'],
      ['board', '04'],
      ['storage', '05'],
    ])
  })

  it('leaves out what the engine could not read', () => {
    const partial: SystemSpecs = { ...mockSpecs, gpus: [], machine: null, disks: [] }

    expect(listParts(partial, null).map((p) => p.id)).toEqual(['cpu', 'memory'])
  })

  it('adds the live load and temperature to the graphics card when readings exist', () => {
    const withoutLive = listParts(mockSpecs, null).find((p) => p.id === 'gpu')!
    const live = {
      name: 'GPU',
      loadPercent: 30,
      memoryUsedBytes: 1,
      memoryTotalBytes: 2,
      temperatureCelsius: 55,
      powerWatts: null,
      clockMHz: null,
      fanPercent: null,
      memoryPercent: 50,
    }
    const withLive = listParts(mockSpecs, live).find((p) => p.id === 'gpu')!

    expect(withLive.rows.length).toBe(withoutLive.rows.length + 2)
    expect(withLive.rows.map((r) => r.label)).toContain('Température')
  })
})
