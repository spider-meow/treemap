import { describe, expect, it } from 'vitest'
import { lastPoint, linePath, niceByteCeiling, niceCeiling, statsOf } from './chart'

describe('niceCeiling', () => {
  it('rounds up to 1, 2, 5 or 10 times a power of ten', () => {
    expect(niceCeiling(0.9)).toBe(1)
    expect(niceCeiling(1.2)).toBe(2)
    expect(niceCeiling(3)).toBe(5)
    expect(niceCeiling(7)).toBe(10)
    expect(niceCeiling(70)).toBe(100)
    expect(niceCeiling(0)).toBe(1)
  })
})

describe('niceByteCeiling', () => {
  it('uses round figures in binary units', () => {
    expect(niceByteCeiling(1.9 * 1024 ** 2)).toBe(2 * 1024 ** 2)
    expect(niceByteCeiling(33 * 1024 ** 2)).toBe(50 * 1024 ** 2)
    expect(niceByteCeiling(300 * 1024)).toBe(500 * 1024)
  })

  it('does not zoom in on a nearly idle link', () => {
    expect(niceByteCeiling(200)).toBeGreaterThanOrEqual(100 * 1024)
  })
})

describe('statsOf', () => {
  it('gives the latest, the mean and the peak', () => {
    expect(statsOf([10, 40, 20])).toEqual({ last: 20, average: (10 + 40 + 20) / 3, peak: 40 })
    expect(statsOf([])).toEqual({ last: 0, average: 0, peak: 0 })
  })
})

describe('linePath', () => {
  it('maps the newest reading to the right edge and 0 to the bottom', () => {
    const path = linePath([0, 50, 100], 3, 100, 200, 100)

    expect(path).toBe('M0.0 100.0 L100.0 50.0 L200.0 0.0')
  })

  it('right-aligns a window that is not full yet', () => {
    const path = linePath([100], 5, 100, 400, 80)

    expect(path).toBe('M400.0 0.0')
  })

  it('clamps readings outside the scale and ignores an empty plot', () => {
    expect(linePath([250], 2, 100, 100, 100)).toBe('M100.0 0.0')
    expect(linePath([], 5, 100, 100, 100)).toBe('')
    expect(linePath([1], 5, 100, 0, 100)).toBe('')
  })
})

describe('lastPoint', () => {
  it('is the end of the line', () => {
    expect(lastPoint([20, 50], 10, 100, 90, 100)).toEqual({ x: 90, y: 50 })
    expect(lastPoint([], 10, 100, 90, 100)).toBeNull()
  })
})
