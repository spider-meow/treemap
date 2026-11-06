import { motion } from 'motion/react'
import { useEffect, useState } from 'react'
import type { BoostReport, BoostSnapshot, BoosterApp, BoosterState } from '../bridge/types'
import { Button } from '../components/Button'
import { PageHeader } from '../components/PageHeader'
import { Plate, SpecList } from '../components/Plate'
import { Switch } from '../components/Switch'
import { useBooster } from '../hooks/useBooster'
import { formatBytes, formatPercent } from '../lib/format'
import { CATALOG } from '../lib/names'
import './jeu.css'

const sheet = { hidden: {}, show: { transition: { staggerChildren: 0.06, delayChildren: 0.08 } } }

function useNow(active: boolean): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!active) return
    const timer = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [active])
  return now
}

function duration(from: string, to: number): string {
  const seconds = Math.max(0, Math.floor((to - new Date(from).getTime()) / 1000))
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)
  const two = (n: number) => String(n).padStart(2, '0')
  return hours > 0 ? `${hours} h ${two(minutes % 60)} min` : `${minutes} min ${two(seconds % 60)} s`
}

/** "12,9 Go, puis 10,7 Go (−2,2 Go)": what changed, with its sign, never a promise. */
function change(before: number, after: number, format: (v: number) => string): string {
  const delta = after - before
  const sign = delta === 0 ? '±' : delta < 0 ? '−' : '+'
  return `${format(before)} → ${format(after)} (${sign}${format(Math.abs(delta))})`
}

function measureRows(before: BoostSnapshot, after: BoostSnapshot | null) {
  if (!after) {
    return [{ label: 'Mesure', value: 'Résultat dans quelques secondes, le temps que Windows libère la mémoire…' }]
  }
  return [
    { label: 'Mémoire utilisée', value: change(before.ramUsedBytes, after.ramUsedBytes, formatBytes) },
    { label: 'Programmes lancés', value: change(before.processCount, after.processCount, (v) => String(Math.round(v))) },
    { label: 'Processeur', value: change(before.cpuPercent, after.cpuPercent, (v) => formatPercent(v)) },
  ]
}

export function JeuPage() {
  const booster = useBooster()
  const { state, error } = booster

  if (!state) {
    return (
      <>
        <PageHeader title="Jeu" summary="Préparer le PC avant de jouer, et mesurer ce que ça change." />
        <p className="status" role="status">
          Lecture des programmes ouverts…
        </p>
        {error && (
          <p className="status status--error" role="alert">
            {error}
          </p>
        )}
      </>
    )
  }

  return (
    <>
      <PageHeader title="Jeu" summary="Préparer le PC avant de jouer, et mesurer ce que ça change. Rien n’est forcé : tout est remis en état à la fin." />
      {error && (
        <p className="status status--error" role="alert">
          {error}
        </p>
      )}

      <motion.div className="sheet" variants={sheet} initial="hidden" animate="show">
        <SessionPlate booster={booster} state={state} />
        <SettingsPlate booster={booster} state={state} />
        <AppsPlate booster={booster} state={state} />
      </motion.div>
    </>
  )
}

type Booster = ReturnType<typeof useBooster>

function SessionPlate({ booster, state }: { booster: Booster; state: BoosterState }) {
  const report = state.session
  const live = state.sessionActive && report
  const now = useNow(!!live)

  return (
    <Plate index={CATALOG.session} title="Session de jeu" tone={live ? 'forest' : 'paper'} span={7}>
      {live ? (
        <ActiveSession report={report} now={now} onStop={() => void booster.stop()} busy={booster.busy} />
      ) : report ? (
        <EndedSession report={report} onRelaunch={() => void booster.relaunch()} busy={booster.busy} />
      ) : null}

      {!live && (
        <>
          {state.games.length > 0 ? (
            <ul className="games">
              {state.games.map((game, index) => (
                <li key={game.id} className="games__item">
                  <span className="games__name">
                    <strong>{game.title || game.name}</strong>
                    <span className="games__sub">{game.name}.exe</span>
                  </span>
                  <Button variant={index === 0 ? 'primary' : 'secondary'} disabled={booster.busy} onClick={() => void booster.start(game.id)}>
                    {`Booster ${game.name}`}
                  </Button>
                  {!game.known && (
                    <Button variant="quiet" onClick={() => void booster.update({ knownGames: [...state.settings.knownGames, game.name] })}>
                      Retenir ce jeu
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          ) : (
            <p className="session__empty">
              Aucun jeu ouvert. Lance ton jeu : il apparaît ici (les jeux installés par Steam, Epic ou Xbox sont reconnus tout seuls). Pour un autre
              programme, choisis « C’est un jeu » dans la liste des applications.
            </p>
          )}
        </>
      )}
    </Plate>
  )
}

function ActiveSession({ report, now, onStop, busy }: { report: BoostReport; now: number; onStop: () => void; busy: boolean }) {
  return (
    <>
      <p className="session__lead">
        <strong>{report.gameName}</strong> tourne depuis <span className="num">{duration(report.startedAt, now)}</span>.
      </p>
      <SpecList
        rows={[
          ...report.steps.map((step) => ({ label: step.title, value: `${step.done ? 'Fait' : 'Non'} · ${step.detail}` })),
          ...measureRows(report.before, report.after),
        ]}
      />
      <div className="session__actions">
        <Button variant="primary" onClick={onStop} disabled={busy}>
          Terminer la session
        </Button>
      </div>
    </>
  )
}

function EndedSession({ report, onRelaunch, busy }: { report: BoostReport; onRelaunch: () => void; busy: boolean }) {
  const closed = report.closedApps.filter((a) => a.closed)
  const waiting = closed.filter((a) => a.relaunchable && !a.relaunched)
  return (
    <>
      <p className="session__lead">
        Dernière session : <strong>{report.gameName}</strong>
        {report.endedAt ? `, ${duration(report.startedAt, new Date(report.endedAt).getTime())}` : ''}.{' '}
        {report.restoredPowerPlan ? `Plan d’alimentation remis sur « ${report.restoredPowerPlan} ».` : ''}
      </p>
      <SpecList rows={measureRows(report.before, report.after)} />
      {closed.length > 0 && (
        <div className="session__closed">
          <p>
            Fermées pour la partie : {closed.map((a) => a.name).join(', ')}.
            {closed.some((a) => !a.relaunchable) ? ' Certaines ne peuvent pas être relancées d’ici.' : ''}
          </p>
          {waiting.length > 0 && (
            <Button onClick={onRelaunch} disabled={busy}>
              Relancer les applications fermées
            </Button>
          )}
        </div>
      )}
    </>
  )
}

function SettingsPlate({ booster, state }: { booster: Booster; state: BoosterState }) {
  const { settings } = state
  const { update } = booster

  return (
    <Plate index={CATALOG.boosterSettings} title="Ce que fait le booster" span={5}>
      <div className="settings">
        <Switch checked={settings.closeApps} onChange={(closeApps) => void update({ closeApps })} hint="Poliment : une application avec du travail non enregistré le demande, rien n’est forcé.">
          Fermer les applications cochées
        </Switch>
        <Switch checked={settings.highPriority} onChange={(highPriority) => void update({ highPriority })} hint="Le jeu passe devant les autres programmes pour le processeur.">
          Priorité haute pour le jeu
        </Switch>
        <Switch
          checked={settings.highPerformancePlan}
          onChange={(highPerformancePlan) => void update({ highPerformancePlan })}
          hint={
            state.highPerformanceAvailable
              ? `Plan actuel : ${state.powerPlan?.name ?? 'inconnu'}. Il est remis à la fin de la partie.`
              : 'Le plan Hautes performances n’existe pas sur ce PC : cette action sera ignorée.'
          }
        >
          Plan Hautes performances pendant la partie
        </Switch>
        <Switch checked={settings.auto} onChange={(auto) => void update({ auto })} hint="Éteint par défaut : le booster ne démarre que si tu le demandes.">
          Démarrer tout seul quand un jeu s’ouvre
        </Switch>
      </div>

      {settings.knownGames.length > 0 && (
        <div className="known">
          <p className="known__title">Jeux ajoutés à la main</p>
          <ul>
            {settings.knownGames.map((name) => (
              <li key={name} className="known__item">
                <span>{name}.exe</span>
                <Button variant="quiet" onClick={() => void update({ knownGames: settings.knownGames.filter((n) => n !== name) })}>
                  Retirer
                </Button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </Plate>
  )
}

function AppsPlate({ booster, state }: { booster: Booster; state: BoosterState }) {
  const { settings } = state
  const picked = state.apps.filter((a) => a.picked)
  const pickedBytes = picked.reduce((sum, a) => sum + a.memoryBytes, 0)

  const toggle = (app: BoosterApp) => {
    const others = settings.closeList.filter((n) => n.toLowerCase() !== app.name.toLowerCase())
    void booster.update({ closeList: app.picked ? others : [...others, app.name] })
  }

  return (
    <Plate index={CATALOG.boosterApps} title="Applications ouvertes" span={12}>
      <p className="apps__lead">
        Seules les applications cochées sont fermées au lancement d’une session. Windows, la sécurité, les lanceurs de jeux (Steam, Epic…) et les anti-triche
        n’apparaissent pas : ils ne sont jamais touchés.{' '}
        <span className="num">
          {picked.length === 0 ? 'Aucune cochée.' : `${picked.length} cochée${picked.length > 1 ? 's' : ''}, environ ${formatBytes(pickedBytes)} de mémoire.`}
        </span>
      </p>
      {state.apps.length === 0 ? (
        <p>Aucune application avec une fenêtre n’est ouverte.</p>
      ) : (
        <ul className="apps">
          {state.apps.map((app) => (
            <li key={app.name} className="apps__item">
              <label className="apps__pick">
                <input type="checkbox" checked={app.picked} onChange={() => toggle(app)} aria-label={`Fermer ${app.name} avant de jouer`} />
                <span className="apps__name">{app.name}</span>
                <span className="apps__title">{app.title}</span>
              </label>
              <span className="apps__memory num">{formatBytes(app.memoryBytes)}</span>
              <Button
                variant="quiet"
                onClick={() => void booster.update({ knownGames: [...settings.knownGames, app.name], closeList: settings.closeList.filter((n) => n.toLowerCase() !== app.name.toLowerCase()) })}
              >
                C’est un jeu
              </Button>
            </li>
          ))}
        </ul>
      )}
    </Plate>
  )
}
