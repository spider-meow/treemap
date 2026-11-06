import type { ReactNode } from 'react'
import type { GpuMetrics, SystemSpecs } from '../bridge/types'
import type { PlateTone } from '../components/Plate'
import { RollingNumber } from '../components/RollingNumber'
import { formatBytes, formatGigahertz, formatPercent } from './format'
import { CATALOG, cleanCpuName, friendlySlot, joinName } from './names'

export type PartId = 'cpu' | 'gpu' | 'memory' | 'board' | 'storage'

export interface PartRow {
  label: string
  value: ReactNode
}

/** One hardware object of the PC: its catalogue number, its plate colour and the facts shown on every screen. */
export interface Part {
  id: PartId
  index: string
  title: string
  tone: PlateTone
  rows: PartRow[]
}

/** The parts the machine actually has, in catalogue order. The same list feeds the plates and the 3D view. */
export function listParts(specs: SystemSpecs, live: GpuMetrics | null): Part[] {
  const parts: Part[] = []

  if (specs.cpu) {
    parts.push({
      id: 'cpu',
      index: CATALOG.cpu,
      title: 'Processeur',
      tone: 'paper',
      rows: [
        { label: 'Modèle', value: cleanCpuName(specs.cpu.name) },
        { label: 'Cœurs / threads', value: `${specs.cpu.cores} / ${specs.cpu.logicalProcessors}` },
        { label: 'Fréquence max', value: formatGigahertz(specs.cpu.maxClockMHz) },
        { label: 'Cache L2 / L3', value: `${formatBytes(specs.cpu.l2CacheKb * 1024)} / ${formatBytes(specs.cpu.l3CacheKb * 1024)}` },
      ],
    })
  }

  const gpu = specs.gpus[0]
  if (gpu) {
    parts.push({
      id: 'gpu',
      index: CATALOG.gpu,
      title: 'Carte graphique',
      tone: 'forest',
      rows: [
        { label: 'Modèle', value: gpu.name },
        ...(gpu.videoMemoryBytes ? [{ label: 'Mémoire vidéo', value: formatBytes(gpu.videoMemoryBytes) }] : []),
        ...(gpu.driverVersion ? [{ label: 'Pilote', value: gpu.driverVersion }] : []),
        ...(live
          ? [
              { label: 'Charge', value: <RollingNumber value={live.loadPercent} format={(v) => formatPercent(v)} /> },
              ...(live.temperatureCelsius !== null
                ? [
                    {
                      label: 'Température',
                      value: (
                        <span className="live">
                          <RollingNumber value={live.temperatureCelsius} format={(v) => `${Math.round(v)} °C`} />
                        </span>
                      ),
                    },
                  ]
                : []),
            ]
          : []),
      ],
    })
  }

  if (specs.memory) {
    parts.push({
      id: 'memory',
      index: CATALOG.memory,
      title: 'Mémoire',
      tone: 'paper',
      rows: [
        { label: 'Total', value: formatBytes(specs.memory.totalBytes) },
        ...specs.memory.modules.map((m, i) => ({
          label: friendlySlot(m.slot, i),
          value: `${formatBytes(m.capacityBytes)} ${m.type}${m.speedMHz ? ` · ${m.speedMHz} MHz` : ''}`,
        })),
      ],
    })
  }

  if (specs.machine) {
    parts.push({
      id: 'board',
      index: CATALOG.board,
      title: 'Carte mère',
      tone: 'paper',
      rows: [
        { label: 'Modèle', value: specs.machine.boardProduct ?? '—' },
        { label: 'Fabricant', value: specs.machine.boardManufacturer ?? '—' },
        { label: 'BIOS', value: joinName(specs.machine.biosVendor, specs.machine.biosVersion) || '—' },
        ...(specs.machine.biosDate
          ? [{ label: 'Date du BIOS', value: new Date(specs.machine.biosDate).toLocaleDateString('fr-FR') }]
          : []),
      ],
    })
  }

  if (specs.disks.length > 0) {
    parts.push({
      id: 'storage',
      index: CATALOG.storage,
      title: 'Stockage',
      tone: 'crest',
      rows: specs.disks.map((d) => ({
        label: d.name,
        value: [d.kind, formatBytes(d.sizeBytes), d.health].filter(Boolean).join(' · '),
      })),
    })
  }

  return parts
}
