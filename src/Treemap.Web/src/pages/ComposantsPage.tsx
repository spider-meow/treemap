import { Check, Copy } from 'lucide-react'
import { motion } from 'motion/react'
import { useEffect, useState } from 'react'
import type { MetricsSnapshot, SystemSpecs } from '../bridge/types'
import { Button } from '../components/Button'
import { PageHeader } from '../components/PageHeader'
import { Plate, SpecList } from '../components/Plate'
import type { PageId } from '../components/Rail'
import { RollingNumber } from '../components/RollingNumber'
import { RuledBar } from '../components/RuledBar'
import { formatBytes, formatFraction, formatGigahertz, formatPercent, formatUptime } from '../lib/format'
import { CATALOG, cleanCpuName, friendlySlot, joinName } from '../lib/names'
import { buildSummary } from '../lib/summary'
import './composants.css'

interface ComposantsPageProps {
  specs: SystemSpecs
  metrics: MetricsSnapshot | null
  onNavigate: (page: PageId) => void
}

const sheet = { hidden: {}, show: { transition: { staggerChildren: 0.06, delayChildren: 0.08 } } }

function describeMachine(specs: SystemSpecs): string {
  const parts: string[] = []
  if (specs.cpu) parts.push(`un processeur à ${specs.cpu.cores} cœurs`)
  if (specs.memory) parts.push(`${formatBytes(specs.memory.totalBytes)} de mémoire`)
  if (specs.gpus[0]) parts.push(`une ${specs.gpus[0].name}`)
  const storage = specs.disks.reduce((sum, d) => sum + d.sizeBytes, 0)
  if (storage > 0) parts.push(`${formatBytes(storage)} de stockage`)
  const text = parts.length > 1 ? `${parts.slice(0, -1).join(', ')} et ${parts.at(-1)}` : (parts[0] ?? '')
  return text ? `Ce PC embarque ${text}.` : ''
}

export function ComposantsPage({ specs, metrics, onNavigate }: ComposantsPageProps) {
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!copied) return
    const timer = setTimeout(() => setCopied(false), 2200)
    return () => clearTimeout(timer)
  }, [copied])

  const copySummary = async () => {
    await navigator.clipboard.writeText(buildSummary(specs))
    setCopied(true)
  }

  const gpu = specs.gpus[0]
  const gpuLive = metrics?.gpu ?? null

  return (
    <>
      <PageHeader
        title="Composants"
        summary={describeMachine(specs)}
        actions={
          <Button variant="primary" icon={copied ? <Check aria-hidden /> : <Copy aria-hidden />} onClick={copySummary}>
            {copied ? 'Résumé copié' : 'Copier le résumé'}
          </Button>
        }
      />
      <span className="visually-hidden" role="status">
        {copied ? 'Résumé copié dans le presse-papiers' : ''}
      </span>

      {specs.volumes.map((volume) => (
        <section className="volume" key={volume.name} aria-label={`Disque ${volume.name}`}>
          <h2 className="volume__name">{volume.name.replace('\\', '')}</h2>
          <RuledBar fraction={volume.usedFraction} label={`Occupation du disque ${volume.name}`} />
          <p className="volume__detail num">
            {formatBytes(volume.freeBytes)} libres sur {formatBytes(volume.totalBytes)}
            <span className="volume__share">{formatFraction(volume.usedFraction)} occupés</span>
          </p>
          <Button variant="quiet" onClick={() => onNavigate('stockage')}>
            Voir où partent les Go
          </Button>
        </section>
      ))}

      <motion.div className="sheet" variants={sheet} initial="hidden" animate="show">
        {gpu && (
          <Plate index={CATALOG.gpu} title="Carte graphique" tone="forest" span={7}>
            <SpecList
              rows={[
                { label: 'Modèle', value: gpu.name },
                ...(gpu.videoMemoryBytes ? [{ label: 'Mémoire vidéo', value: formatBytes(gpu.videoMemoryBytes) }] : []),
                ...(gpu.driverVersion ? [{ label: 'Pilote', value: gpu.driverVersion }] : []),
                ...(gpuLive
                  ? [
                      { label: 'Charge', value: <RollingNumber value={gpuLive.loadPercent} format={(v) => formatPercent(v)} /> },
                      ...(gpuLive.temperatureCelsius !== null
                        ? [
                            {
                              label: 'Température',
                              value: (
                                <span className="live">
                                  <RollingNumber value={gpuLive.temperatureCelsius} format={(v) => `${Math.round(v)} °C`} />
                                </span>
                              ),
                            },
                          ]
                        : []),
                    ]
                  : []),
              ]}
            />
          </Plate>
        )}

        {specs.cpu && (
          <Plate index={CATALOG.cpu} title="Processeur" tone="paper" span={5}>
            <SpecList
              rows={[
                { label: 'Modèle', value: cleanCpuName(specs.cpu.name) },
                { label: 'Cœurs / threads', value: `${specs.cpu.cores} / ${specs.cpu.logicalProcessors}` },
                { label: 'Fréquence max', value: formatGigahertz(specs.cpu.maxClockMHz) },
                { label: 'Cache L2 / L3', value: `${formatBytes(specs.cpu.l2CacheKb * 1024)} / ${formatBytes(specs.cpu.l3CacheKb * 1024)}` },
              ]}
            />
          </Plate>
        )}

        {specs.memory && (
          <Plate index={CATALOG.memory} title="Mémoire" tone="paper" span={4}>
            <SpecList
              rows={[
                { label: 'Total', value: formatBytes(specs.memory.totalBytes) },
                ...specs.memory.modules.map((m, i) => ({
                  label: friendlySlot(m.slot, i),
                  value: `${formatBytes(m.capacityBytes)} ${m.type}${m.speedMHz ? ` · ${m.speedMHz} MHz` : ''}`,
                })),
              ]}
            />
          </Plate>
        )}

        {specs.machine && (
          <Plate index={CATALOG.board} title="Carte mère" tone="paper" span={4}>
            <SpecList
              rows={[
                { label: 'Modèle', value: specs.machine.boardProduct ?? '—' },
                { label: 'Fabricant', value: specs.machine.boardManufacturer ?? '—' },
                { label: 'BIOS', value: joinName(specs.machine.biosVendor, specs.machine.biosVersion) || '—' },
                ...(specs.machine.biosDate
                  ? [{ label: 'Date du BIOS', value: new Date(specs.machine.biosDate).toLocaleDateString('fr-FR') }]
                  : []),
              ]}
            />
          </Plate>
        )}

        {specs.disks.length > 0 && (
          <Plate index={CATALOG.storage} title="Stockage" tone="crest" span={4}>
            <SpecList
              rows={specs.disks.map((d) => ({
                label: d.name,
                value: [d.kind, formatBytes(d.sizeBytes), d.health].filter(Boolean).join(' · '),
              }))}
            />
          </Plate>
        )}

        {specs.os && (
          <Plate index={CATALOG.system} title="Système" tone="paper" span={12}>
            <SpecList
              columns
              rows={[
                { label: 'Windows', value: specs.os.name.replace('Microsoft ', '') },
                { label: 'Version', value: `${specs.os.version} (build ${specs.os.build})` },
                { label: 'Architecture', value: specs.os.architecture },
                ...(specs.os.lastBoot ? [{ label: 'Allumé depuis', value: formatUptime(Date.now() - new Date(specs.os.lastBoot).getTime()) }] : []),
              ]}
            />
          </Plate>
        )}
      </motion.div>

      {specs.warnings.length > 0 && (
        <ul className="warnings">
          {specs.warnings.map((w) => (
            <li key={w}>{w}</li>
          ))}
        </ul>
      )}
    </>
  )
}
