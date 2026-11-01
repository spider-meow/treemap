export interface Rect {
  x: number
  y: number
  w: number
  h: number
}

export interface Cell<T> {
  item: T
  rect: Rect
}

/** The worst aspect ratio a row would have if laid along `side`. Lower is squarer. */
function worst(row: number[], side: number): number {
  const sum = row.reduce((a, b) => a + b, 0)
  const max = Math.max(...row)
  const min = Math.min(...row)
  return Math.max((side * side * max) / (sum * sum), (sum * sum) / (side * side * min))
}

/**
 * Squarified treemap (Bruls, Huizing, van Wijk): areas proportional to weight, cells as close to square as
 * possible so they stay readable and clickable. Items with no weight get no cell. Same algorithm as the C# engine.
 */
export function squarify<T>(items: ReadonlyArray<{ item: T; weight: number }>, bounds: Rect): Cell<T>[] {
  const sorted = items.filter((i) => i.weight > 0).sort((a, b) => b.weight - a.weight)
  const total = sorted.reduce((sum, i) => sum + i.weight, 0)
  if (sorted.length === 0 || bounds.w <= 0 || bounds.h <= 0) return []

  const scale = (bounds.w * bounds.h) / total
  const areas = sorted.map((i) => i.weight * scale)
  const cells: Cell<T>[] = []

  let rect = { ...bounds }
  let index = 0

  while (index < sorted.length) {
    const lastRow = index === sorted.length - 1
    const side = Math.min(rect.w, rect.h)

    let end = index + 1
    while (end < sorted.length && worst(areas.slice(index, end + 1), side) <= worst(areas.slice(index, end), side)) {
      end++
    }

    const row = areas.slice(index, end)
    const rowArea = lastRow || end === sorted.length ? rect.w * rect.h : row.reduce((a, b) => a + b, 0)

    if (rect.w >= rect.h) {
      // A column on the left, as tall as the remaining rectangle.
      const width = rowArea / rect.h
      let y = rect.y
      row.forEach((area, i) => {
        const height = i === row.length - 1 ? rect.y + rect.h - y : area / width
        cells.push({ item: sorted[index + i].item, rect: { x: rect.x, y, w: width, h: height } })
        y += height
      })
      rect = { x: rect.x + width, y: rect.y, w: rect.w - width, h: rect.h }
    } else {
      // A row on top, as wide as the remaining rectangle.
      const height = rowArea / rect.w
      let x = rect.x
      row.forEach((area, i) => {
        const width = i === row.length - 1 ? rect.x + rect.w - x : area / height
        cells.push({ item: sorted[index + i].item, rect: { x, y: rect.y, w: width, h: height } })
        x += width
      })
      rect = { x: rect.x, y: rect.y + height, w: rect.w, h: rect.h - height }
    }

    index = end
  }

  return cells
}
