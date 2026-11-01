import { describe, expect, it } from 'vitest'
import { squarify } from './squarify'

const area = (r: { w: number; h: number }) => r.w * r.h

describe('squarify', () => {
  const bounds = { x: 10, y: 20, w: 400, h: 300 }
  const items = [60, 25, 10, 3, 2].map((weight, i) => ({ item: `n${i}`, weight }))

  it('fills the bounds exactly with areas proportional to the weights', () => {
    const cells = squarify(items, bounds)

    expect(cells).toHaveLength(5)
    expect(cells.reduce((sum, c) => sum + area(c.rect), 0)).toBeCloseTo(area(bounds), 6)
    cells.forEach((c, i) => {
      expect(area(c.rect) / area(bounds)).toBeCloseTo(items[i].weight / 100, 6)
    })
  })

  it('keeps every cell inside the bounds without overlap', () => {
    const cells = squarify(items, bounds)

    for (const { rect } of cells) {
      expect(rect.x).toBeGreaterThanOrEqual(bounds.x - 1e-6)
      expect(rect.y).toBeGreaterThanOrEqual(bounds.y - 1e-6)
      expect(rect.x + rect.w).toBeLessThanOrEqual(bounds.x + bounds.w + 1e-6)
      expect(rect.y + rect.h).toBeLessThanOrEqual(bounds.y + bounds.h + 1e-6)
    }
    for (let i = 0; i < cells.length; i++) {
      for (let j = i + 1; j < cells.length; j++) {
        const a = cells[i].rect
        const b = cells[j].rect
        const overlapW = Math.min(a.x + a.w, b.x + b.w) - Math.max(a.x, b.x)
        const overlapH = Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y)
        expect(overlapW > 1e-6 && overlapH > 1e-6).toBe(false)
      }
    }
  })

  it('gives a single item the whole rectangle', () => {
    const [cell] = squarify([{ item: 'only', weight: 5 }], bounds)

    expect(cell.rect).toEqual(bounds)
  })

  it('ignores empty items and empty bounds', () => {
    expect(squarify([{ item: 'a', weight: 0 }], bounds)).toEqual([])
    expect(squarify(items, { x: 0, y: 0, w: 0, h: 100 })).toEqual([])
    expect(squarify([], bounds)).toEqual([])
  })

  it('keeps cells reasonably square for similar weights', () => {
    const cells = squarify(Array.from({ length: 12 }, (_, i) => ({ item: i, weight: 10 })), { x: 0, y: 0, w: 600, h: 400 })

    for (const { rect } of cells) {
      expect(Math.max(rect.w / rect.h, rect.h / rect.w)).toBeLessThan(3)
    }
  })
})
