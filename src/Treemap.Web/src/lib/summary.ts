import type { SystemSpecs } from '../bridge/types'
import { formatBytes, formatGigahertz } from './format'

/** Plain-text description of the machine, for pasting into a message or a listing. */
export function buildSummary(specs: SystemSpecs): string {
  const lines: string[] = []
  const join = (...parts: Array<string | null | undefined>) => parts.filter(Boolean).join(' ')

  if (specs.os) lines.push(`Système : ${specs.os.name} ${specs.os.version} (build ${specs.os.build}, ${specs.os.architecture})`)
  if (specs.machine) {
    lines.push(`Machine : ${join(specs.machine.manufacturer, specs.machine.model)}`)
    lines.push(`Carte mère : ${join(specs.machine.boardManufacturer, specs.machine.boardProduct)}`)
    lines.push(`BIOS : ${join(specs.machine.biosVendor, specs.machine.biosVersion)}`)
  }
  if (specs.cpu) {
    lines.push(
      `Processeur : ${specs.cpu.name} — ${specs.cpu.cores} cœurs / ${specs.cpu.logicalProcessors} threads, ${formatGigahertz(specs.cpu.maxClockMHz)}`,
    )
  }
  for (const gpu of specs.gpus) {
    const memory = gpu.videoMemoryBytes ? `, ${formatBytes(gpu.videoMemoryBytes)}` : ''
    lines.push(`Carte graphique : ${gpu.name}${memory}`)
  }
  if (specs.memory) {
    lines.push(`Mémoire : ${formatBytes(specs.memory.totalBytes)}`)
    for (const m of specs.memory.modules) {
      lines.push(`  - ${m.slot ?? 'Barrette'} : ${formatBytes(m.capacityBytes)} ${m.type} ${m.speedMHz} MHz`)
    }
  }
  for (const disk of specs.disks) lines.push(`Disque : ${disk.name} — ${disk.kind}, ${formatBytes(disk.sizeBytes)}`)
  for (const volume of specs.volumes) {
    lines.push(`Volume ${volume.name} : ${formatBytes(volume.freeBytes)} libres sur ${formatBytes(volume.totalBytes)} (${volume.fileSystem})`)
  }

  return lines.join('\n')
}
