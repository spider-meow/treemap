import { describe, expect, it } from 'vitest'
import { mockSpecs } from '../bridge/mock'
import type { SystemSpecs } from '../bridge/types'
import { buildSummary } from './summary'

describe('buildSummary', () => {
  it('lists every present section in plain French', () => {
    const text = buildSummary(mockSpecs).replace(/\s/g, ' ')

    expect(text).toContain('Processeur : Intel(R) Core(TM) i5-10400F')
    expect(text).toContain('6 cœurs / 12 threads')
    expect(text).toContain('Carte graphique : NVIDIA GeForce RTX 3060, 12,0 Go')
    expect(text).toContain('DDR4 2133 MHz')
    expect(text).toContain('104 Go libres sur 930 Go')
  })

  it('skips sections that could not be read', () => {
    const empty: SystemSpecs = { os: null, machine: null, cpu: null, gpus: [], memory: null, disks: [], volumes: [], warnings: [] }

    expect(buildSummary(empty)).toBe('')
  })
})
