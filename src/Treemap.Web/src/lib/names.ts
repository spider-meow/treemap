/** Catalogue numbers: each object keeps the same number on every screen. */
export const CATALOG = {
  cpu: '01',
  gpu: '02',
  memory: '03',
  board: '04',
  storage: '05',
  system: '06',
} as const

/** "Intel(R) Core(TM) i5-10400F CPU @ 2.90GHz" -> "Intel Core i5-10400F" (the clock has its own row). */
export function cleanCpuName(name: string): string {
  return name
    .replace(/\((R|TM)\)/gi, '')
    .replace(/\s+CPU\b/i, '')
    .replace(/\s*@\s*[\d.,]+\s*GHz/i, '')
    .replace(/\s{2,}/g, ' ')
    .trim()
}

/** Memory slot ids like "ChannelA-DIMM0" are not meant for people. */
export function friendlySlot(slot: string | null, index: number): string {
  const match = slot?.match(/Channel([A-Z])-DIMM(\d+)/i)
  if (match) return `Barrette ${match[1].toUpperCase()}${Number(match[2]) + 1}`
  return slot ?? `Barrette ${index + 1}`
}

/** Joins the manufacturer and model without repeating the manufacturer. */
export function joinName(...parts: Array<string | null | undefined>): string {
  return parts.filter((p): p is string => Boolean(p)).join(' ')
}
