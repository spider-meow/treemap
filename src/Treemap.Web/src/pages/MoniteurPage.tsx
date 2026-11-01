import { motion } from 'motion/react'
import { useEffect, useState } from 'react'
import type { FpsReading, MetricsSnapshot, OverlayState } from '../bridge/types'
import { Button } from '../components/Button'
import { ChartLegend, LineChart } from '../components/LineChart'
import { CornerPicker } from '../components/CornerPicker'
import { PageHeader } from '../components/PageHeader'
import { Plate, SpecList } from '../components/Plate'
import { RollingNumber } from '../components/RollingNumber'
import { Switch } from '../components/Switch'
import { HISTORY_LENGTH } from '../hooks/useMetrics'
import { useOverlay } from '../hooks/useOverlay'
import { niceByteCeiling, statsOf } from '../lib/chart'
import { formatBytes, formatPercent, formatRate } from '../lib/format'
import { CATALOG } from '../lib/names'
import './moniteur.css'

interface MoniteurPageProps {
  latest: MetricsSnapshot | null
  history: MetricsSnapshot[]
}

const sheet = { hidden: {}, show: { transition: { staggerChildren: 0.06, delayChildren: 0.08 } } }
const WINDOW_SECONDS = HISTORY_LENGTH

const percent = (v: number) => formatPercent(v)
const rate = (v: number) => (v === 0 ? '0' : formatRate(v))

function pickRates(history: MetricsSnapshot[], a: keyof MetricsSnapshot, b: keyof MetricsSnapshot) {
  return [history.map((h) => h[a] as number), history.map((h) => h[b] as number)] as const
}

export function MoniteurPage({ latest, history }: MoniteurPageProps) {
  const overlay = useOverlay()

  if (!latest) {
    return (
      <>
        <PageHeader title="Moniteur" summary="L’état du PC, seconde par seconde." />
        <p className="status" role="status">
          En attente des premières mesures…
        </p>
      </>
    )
  }

  const cpu = history.map((h) => h.cpuLoadPercent)
  const memory = history.map((h) => h.memoryPercent)
  const gpuLoad = history.map((h) => h.gpu?.loadPercent ?? 0)
  const [diskRead, diskWrite] = pickRates(history, 'diskReadBytesPerSecond', 'diskWriteBytesPerSecond')
  const [netDown, netUp] = pickRates(history, 'networkDownBytesPerSecond', 'networkUpBytesPerSecond')

  const cpuStats = statsOf(cpu)
  const memoryStats = statsOf(memory)
  const gpu = latest.gpu

  return (
    <>
      <PageHeader title="Moniteur" summary="L’état du PC, seconde par seconde, sur les deux dernières minutes." />

      <motion.div className="sheet" variants={sheet} initial="hidden" animate="show">
        <Plate index={CATALOG.cpu} title="Processeur" span={6}>
          <SpecList
            rows={[
              { label: 'Maintenant', value: <RollingNumber value={latest.cpuLoadPercent} format={percent} /> },
              { label: 'Moyenne', value: percent(cpuStats.average) },
              { label: 'Pic', value: percent(cpuStats.peak) },
            ]}
          />
          <LineChart
            series={[{ label: 'Charge', values: cpu }]}
            max={100}
            capacity={HISTORY_LENGTH}
            windowSeconds={WINDOW_SECONDS}
            formatTick={percent}
            summary={`Charge du processeur : ${percent(latest.cpuLoadPercent)} maintenant, pic à ${percent(cpuStats.peak)}.`}
          />
        </Plate>

        <Plate index={CATALOG.gpu} title="Carte graphique" tone="forest" span={6}>
          {gpu ? (
            <>
              <SpecList
                rows={[
                  { label: 'Charge', value: <RollingNumber value={gpu.loadPercent} format={percent} /> },
                  ...(gpu.temperatureCelsius !== null
                    ? [
                        {
                          label: 'Température',
                          value: (
                            <span className="live">
                              <RollingNumber value={gpu.temperatureCelsius} format={(v) => `${Math.round(v)} °C`} />
                            </span>
                          ),
                        },
                      ]
                    : []),
                  ...(gpu.powerWatts !== null ? [{ label: 'Puissance', value: `${Math.round(gpu.powerWatts)} W` }] : []),
                  ...(gpu.clockMHz !== null ? [{ label: 'Fréquence', value: `${Math.round(gpu.clockMHz)} MHz` }] : []),
                  ...(gpu.fanPercent !== null ? [{ label: 'Ventilateur', value: percent(gpu.fanPercent) }] : []),
                  { label: 'Mémoire vidéo', value: `${formatBytes(gpu.memoryUsedBytes)} sur ${formatBytes(gpu.memoryTotalBytes)}` },
                ]}
              />
              <LineChart
                series={[{ label: 'Charge', values: gpuLoad }]}
                max={100}
                capacity={HISTORY_LENGTH}
                windowSeconds={WINDOW_SECONDS}
                formatTick={percent}
                summary={`Charge de la carte graphique : ${percent(gpu.loadPercent)} maintenant.`}
              />
            </>
          ) : (
            <p>Aucune carte NVIDIA lue : les cartes AMD et Intel ne sont pas encore prises en charge.</p>
          )}
        </Plate>

        <Plate index={CATALOG.memory} title="Mémoire" span={4}>
          <SpecList
            rows={[
              { label: 'Utilisée', value: `${formatBytes(latest.memoryUsedBytes)} sur ${formatBytes(latest.memoryTotalBytes)}` },
              { label: 'Occupation', value: <RollingNumber value={latest.memoryPercent} format={percent} /> },
              { label: 'Pic', value: percent(memoryStats.peak) },
            ]}
          />
          <LineChart
            series={[{ label: 'Occupation', values: memory }]}
            max={100}
            capacity={HISTORY_LENGTH}
            windowSeconds={WINDOW_SECONDS}
            formatTick={percent}
            summary={`Occupation de la mémoire : ${percent(latest.memoryPercent)}.`}
          />
        </Plate>

        <Plate index={CATALOG.storage} title="Disque" span={4}>
          <SpecList
            rows={[
              { label: 'Lecture', value: formatRate(latest.diskReadBytesPerSecond) },
              { label: 'Écriture', value: formatRate(latest.diskWriteBytesPerSecond) },
              { label: 'Activité', value: percent(latest.diskActivePercent) },
            ]}
          />
          <LineChart
            series={[
              { label: 'Lecture', values: diskRead },
              { label: 'Écriture', values: diskWrite, dashed: true },
            ]}
            max={niceByteCeiling(Math.max(...diskRead, ...diskWrite, 0))}
            capacity={HISTORY_LENGTH}
            windowSeconds={WINDOW_SECONDS}
            formatTick={rate}
            summary={`Disque : lecture ${formatRate(latest.diskReadBytesPerSecond)}, écriture ${formatRate(latest.diskWriteBytesPerSecond)}.`}
          />
          <ChartLegend items={[{ label: 'Lecture' }, { label: 'Écriture', dashed: true }]} />
        </Plate>

        <Plate index={CATALOG.network} title="Réseau" span={4}>
          <SpecList
            rows={[
              { label: 'Réception', value: formatRate(latest.networkDownBytesPerSecond) },
              { label: 'Émission', value: formatRate(latest.networkUpBytesPerSecond) },
            ]}
          />
          <LineChart
            series={[
              { label: 'Réception', values: netDown },
              { label: 'Émission', values: netUp, dashed: true },
            ]}
            max={niceByteCeiling(Math.max(...netDown, ...netUp, 0))}
            capacity={HISTORY_LENGTH}
            windowSeconds={WINDOW_SECONDS}
            formatTick={rate}
            summary={`Réseau : réception ${formatRate(latest.networkDownBytesPerSecond)}, émission ${formatRate(latest.networkUpBytesPerSecond)}.`}
          />
          <ChartLegend items={[{ label: 'Réception' }, { label: 'Émission', dashed: true }]} />
        </Plate>

        <Plate index={CATALOG.overlay} title="Overlay en jeu" tone="crest" span={12}>
          <OverlayPanel overlay={overlay} />
        </Plate>
      </motion.div>
    </>
  )
}

// ---- Overlay settings ----------------------------------------------------------------------------------------------

const LINES = [
  ['showFps', 'FPS'],
  ['showCpu', 'Processeur'],
  ['showGpu', 'Carte graphique'],
  ['showGpuTemperature', 'Température de la carte'],
  ['showMemory', 'Mémoire'],
  ['showVideoMemory', 'Mémoire vidéo'],
] as const

function fpsText(state: OverlayState): string {
  switch (state.fpsStatus) {
    case 'running':
      return 'Mesure active pour les jeux DirectX 9 à 12 (Vulkan et OpenGL ne sont pas couverts).'
    case 'needsAdministrator':
      return 'Windows réserve cette mesure aux administrateurs. Relance l’appli en administrateur pour l’activer.'
    case 'failed':
      return `Mesure impossible${state.fpsMessage ? ` (${state.fpsMessage})` : ''}.`
    default:
      return 'La mesure démarre quand l’overlay est affiché.'
  }
}

function OverlayPanel({ overlay }: { overlay: ReturnType<typeof useOverlay> }) {
  const { state, fps, error, update, relaunchAsAdmin } = overlay

  if (!state) {
    return <p role="status">Lecture des réglages…</p>
  }

  const { settings } = state

  return (
    <div className="overlay-panel">
      <div className="overlay-panel__main">
        <Switch
          checked={settings.enabled}
          onChange={(enabled) => void update({ enabled })}
          hint={
            state.hotkey.registered
              ? `Raccourci pour l’afficher ou le masquer : ${state.hotkey.description}.`
              : `Le raccourci ${state.hotkey.description} est déjà pris par un autre programme.`
          }
        >
          Afficher l’overlay par-dessus les jeux
        </Switch>

        <div className="overlay-panel__lines" role="group" aria-labelledby="overlay-lines-title">
          <p id="overlay-lines-title" className="overlay-panel__lines-title">
            Lignes affichées
          </p>
          {LINES.map(([key, label]) => (
            <Switch key={key} checked={settings[key]} onChange={(checked) => void update({ [key]: checked })}>
              {label}
            </Switch>
          ))}
        </div>

        <div className="overlay-panel__fps" role="status">
          <p className="overlay-panel__fps-title">FPS</p>
          <p>{fpsText(state)}</p>
          {state.fpsStatus === 'running' && <FpsLine reading={fps} />}
          {state.canRelaunchAsAdmin && (
            <Button variant="primary" onClick={() => void relaunchAsAdmin()}>
              Relancer en administrateur
            </Button>
          )}
        </div>
        {error && (
          <p className="status status--error" role="alert">
            {error}
          </p>
        )}
      </div>

      <div className="overlay-panel__side">
        <div>
          <p className="overlay-panel__label">Coin de l’écran</p>
          <CornerPicker value={settings.corner} onChange={(corner) => void update({ corner })} />
        </div>
        <OpacityControl value={settings.opacity} min={state.minOpacity} onCommit={(opacity) => void update({ opacity })} />
      </div>
    </div>
  )
}

function FpsLine({ reading }: { reading: FpsReading | null }) {
  return (
    <p className="overlay-panel__reading num">
      {reading ? (
        <>
          <strong>{Math.round(reading.fps)} FPS</strong> dans {reading.processName}
        </>
      ) : (
        'En attente d’une application qui affiche des images au premier plan.'
      )}
    </p>
  )
}

function OpacityControl({ value, min, onCommit }: { value: number; min: number; onCommit: (value: number) => void }) {
  const [local, setLocal] = useState(value)

  useEffect(() => setLocal(value), [value])
  useEffect(() => {
    if (local === value) return
    const timer = setTimeout(() => onCommit(local), 200)
    return () => clearTimeout(timer)
  }, [local, value, onCommit])

  return (
    <label className="overlay-panel__opacity">
      <span className="overlay-panel__label">
        Opacité <span className="num">{Math.round(local * 100)} %</span>
      </span>
      <input type="range" min={min} max={1} step={0.05} value={local} onChange={(e) => setLocal(Number(e.target.value))} />
    </label>
  )
}
