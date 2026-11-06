import type { BoostReport, BoosterSettings, BoosterState } from './types'

const MIB = 1024 ** 2
const GIB = 1024 ** 3

/** A stand-in for the booster engine, so the Jeu screen can be designed and tested in a plain browser. */
export function createMockBooster(emit: (type: string, payload: unknown) => void) {
  let settings: BoosterSettings = {
    closeApps: true,
    highPriority: true,
    highPerformancePlan: true,
    auto: false,
    closeList: ['Discord', 'chrome'],
    knownGames: [],
    pendingPowerPlan: null,
  }
  let session: BoostReport | null = null

  const apps = () => [
    { id: 201, name: 'chrome', title: 'Nouvel onglet', memoryBytes: 1300 * MIB },
    { id: 202, name: 'Obsidian', title: 'Notes', memoryBytes: 420 * MIB },
    { id: 200, name: 'Discord', title: 'Discord', memoryBytes: 640 * MIB },
    { id: 203, name: 'Spotify', title: 'Spotify', memoryBytes: 380 * MIB },
  ]

  const isPicked = (name: string) => settings.closeList.some((n) => n.toLowerCase() === name.toLowerCase())

  const state = (): BoosterState => ({
    settings,
    games: [{ id: 100, name: 'eldenring', title: 'ELDEN RING', known: false }],
    apps: apps().map((a) => ({ ...a, picked: isPicked(a.name) })),
    sessionActive: session !== null && session.endedAt === null,
    session,
    powerPlan: { guid: '381b4222-f694-41f0-9685-ff5bb260df2e', name: 'Équilibré' },
    highPerformanceAvailable: true,
  })

  return {
    get: state,

    update(next: BoosterSettings): BoosterState {
      // Like the engine: protected names (launchers, Windows…) never stay on the close list.
      settings = { ...next, closeList: next.closeList.filter((n) => n.toLowerCase() !== 'steam'), pendingPowerPlan: settings.pendingPowerPlan }
      return state()
    },

    start(pid: number): BoosterState {
      if (pid !== 100) throw new Error("Ce programme n'est pas un jeu connu : ajoute-le d'abord à la liste des jeux.")
      const closed = settings.closeApps
        ? apps()
            .filter((a) => isPicked(a.name))
            .map((a) => ({ name: a.name, path: `C:\\Apps\\${a.name}.exe`, closed: true, relaunchable: true, relaunched: false }))
        : []
      session = {
        gameName: 'eldenring',
        gameProcessId: 100,
        startedAt: new Date().toISOString(),
        endedAt: null,
        before: { ramUsedBytes: 12.9 * GIB, processCount: 214, cpuPercent: 22 },
        after: null,
        steps: [
          {
            id: 'priority',
            title: 'Priorité du jeu',
            done: settings.highPriority,
            detail: settings.highPriority ? 'Priorité haute pour le jeu pendant la partie.' : 'Désactivé dans les réglages.',
          },
          {
            id: 'power',
            title: "Plan d'alimentation",
            done: settings.highPerformancePlan,
            detail: settings.highPerformancePlan
              ? 'Plan Hautes performances pendant la partie (retour à « Équilibré » ensuite).'
              : 'Désactivé dans les réglages.',
          },
          {
            id: 'close',
            title: 'Applications fermées',
            done: closed.length > 0,
            detail: closed.length ? `${closed.length} sur ${closed.length} fermées.` : "Aucune application de la liste n'est ouverte.",
          },
        ],
        closedApps: closed,
        restoredPowerPlan: null,
      }
      setTimeout(() => {
        if (session && session.endedAt === null) {
          session = { ...session, after: { ramUsedBytes: 10.7 * GIB, processCount: 201, cpuPercent: 9 } }
          emit('booster.changed', state())
        }
      }, 3000)
      return state()
    },

    stop(): BoosterState {
      if (session && session.endedAt === null) {
        session = { ...session, endedAt: new Date().toISOString(), restoredPowerPlan: 'Équilibré' }
      }
      return state()
    },

    relaunch(): BoosterState {
      if (session) {
        session = { ...session, closedApps: session.closedApps.map((a) => ({ ...a, relaunched: a.closed && a.relaunchable })) }
      }
      return state()
    },
  }
}
