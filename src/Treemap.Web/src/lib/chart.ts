/** Smallest "round" ceiling (1, 2, 5, 10, 20, 50… times a power of ten) that is at least `value`. */
export function niceCeiling(value: number): number {
  if (!(value > 0)) return 1
  const power = 10 ** Math.floor(Math.log10(value))
  const scaled = value / power
  const step = scaled <= 1 ? 1 : scaled <= 2 ? 2 : scaled <= 5 ? 5 : 10
  return step * power
}

/**
 * Same idea for byte rates, in 1024-based steps so the axis reads "512 Ko", "2 Mo", "50 Mo" rather than "1,9 Mo".
 * `floor` keeps a quiet link from zooming in on noise.
 */
export function niceByteCeiling(bytes: number, floor = 100 * 1024): number {
  const value = Math.max(bytes, floor)
  const unit = 1024 ** Math.max(0, Math.floor(Math.log(value) / Math.log(1024)))
  return niceCeiling(value / unit) * unit
}

export interface Stats {
  last: number
  average: number
  peak: number
}

export function statsOf(values: readonly number[]): Stats {
  if (values.length === 0) return { last: 0, average: 0, peak: 0 }
  const sum = values.reduce((a, b) => a + b, 0)
  return { last: values[values.length - 1], average: sum / values.length, peak: Math.max(...values) }
}

/**
 * SVG path of a series drawn into a plot of `width` x `height`, right-aligned so a window that is still filling up
 * grows from the right edge (the newest reading always sits at "maintenant").
 */
export function linePath(values: readonly number[], capacity: number, max: number, width: number, height: number): string {
  if (values.length === 0 || width <= 0 || height <= 0 || !(max > 0)) return ''
  const offset = capacity - values.length
  return values
    .map((value, i) => {
      const x = ((offset + i) / Math.max(1, capacity - 1)) * width
      const y = height - Math.min(1, Math.max(0, value / max)) * height
      return `${i === 0 ? 'M' : 'L'}${x.toFixed(1)} ${y.toFixed(1)}`
    })
    .join(' ')
}

/** Where the newest reading lands, for the marker at the end of the line. */
export function lastPoint(values: readonly number[], capacity: number, max: number, width: number, height: number) {
  if (values.length === 0 || !(max > 0)) return null
  const x = ((capacity - 1) / Math.max(1, capacity - 1)) * width
  const y = height - Math.min(1, Math.max(0, values[values.length - 1] / max)) * height
  return { x, y }
}
