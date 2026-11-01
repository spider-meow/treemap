const NBSP = ' '
const UNITS = ['o', 'Ko', 'Mo', 'Go', 'To', 'Po']

const oneDecimal = new Intl.NumberFormat('fr-FR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })
const integer = new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 0 })

/**
 * Sizes in 1024-based steps with French unit names, matching what Windows Explorer shows and what the
 * C# engine formats ("930 Go" in both places).
 */
export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes)) return '—'
  if (bytes < 0) return `-${formatBytes(-bytes)}`

  let value = bytes
  let unit = 0
  while (value >= 1024 && unit < UNITS.length - 1) {
    value /= 1024
    unit++
  }

  const text = unit === 0 || value >= 100 ? integer.format(value) : oneDecimal.format(value)
  return `${text}${NBSP}${UNITS[unit]}`
}

export function formatRate(bytesPerSecond: number): string {
  return `${formatBytes(bytesPerSecond)}/s`
}

export function formatPercent(percent: number): string {
  return `${integer.format(percent)}${NBSP}%`
}

export function formatFraction(fraction: number): string {
  return formatPercent(fraction * 100)
}

export function formatGigahertz(megahertz: number): string {
  return `${new Intl.NumberFormat('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(megahertz / 1000)}${NBSP}GHz`
}

export function formatUptime(milliseconds: number): string {
  const totalMinutes = Math.floor(milliseconds / 60000)
  const days = Math.floor(totalMinutes / 1440)
  const hours = Math.floor((totalMinutes % 1440) / 60)
  const minutes = totalMinutes % 60
  if (days >= 1) return `${days}${NBSP}j ${hours}${NBSP}h`
  if (totalMinutes >= 60) return `${hours}${NBSP}h ${minutes}${NBSP}min`
  return `${minutes}${NBSP}min`
}

/** Usage level of a volume or a resource, used to decide when a bar turns orange. */
export type Level = 'ok' | 'high'

export function levelOf(fraction: number): Level {
  return fraction >= 0.8 ? 'high' : 'ok'
}
