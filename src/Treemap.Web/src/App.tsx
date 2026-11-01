import { AnimatePresence, MotionConfig, motion } from 'motion/react'
import { useState } from 'react'
import { Rail, type PageId } from './components/Rail'
import { useMetrics } from './hooks/useMetrics'
import { useSpecs } from './hooks/useSpecs'
import { ComingPage } from './pages/ComingPage'
import { ComposantsPage } from './pages/ComposantsPage'
import './shell.css'

const EASE = [0.25, 1, 0.5, 1] as const
const PAGES: readonly PageId[] = ['composants', 'stockage', 'moniteur', 'jeu']

/** The host can open a given screen through the URL hash (#stockage); anything else lands on Composants. */
function pageFromHash(): PageId {
  const wanted = window.location.hash.replace('#', '')
  return PAGES.find((id) => id === wanted) ?? 'composants'
}

export default function App() {
  const [page, setPage] = useState<PageId>(pageFromHash)
  const { specs, loading, error } = useSpecs()
  const { latest } = useMetrics()

  const systemVolume = specs?.volumes.find((v) => v.name.toUpperCase().startsWith('C:')) ?? specs?.volumes[0]

  return (
    <MotionConfig reducedMotion="user">
      <div className="app">
        <Rail page={page} onNavigate={setPage} volume={systemVolume} />
        <main className="workspace">
          {/* One sheet at a time: the old page steps out, the next slides in from the right. */}
          <AnimatePresence mode="wait" initial={false}>
            <motion.div
              key={page}
              className="workspace__page"
              initial={{ opacity: 0, x: 28 }}
              animate={{ opacity: 1, x: 0, transition: { duration: 0.28, ease: EASE } }}
              exit={{ opacity: 0, x: -16, transition: { duration: 0.16, ease: EASE } }}
            >
              {page === 'composants' && (
                <>
                  {loading && <p className="status" role="status">Lecture des composants…</p>}
                  {error && <p className="status status--error" role="alert">Lecture impossible : {error}</p>}
                  {specs && <ComposantsPage specs={specs} metrics={latest} onNavigate={setPage} />}
                </>
              )}
              {page === 'stockage' && (
                <ComingPage
                  title="Stockage"
                  summary="Où partent les Go, et comment les récupérer sans risque."
                  note="Cet écran est en cours de refonte dans le nouveau design. L'ancien écran Stockage reste disponible dans la version actuelle de l'application."
                />
              )}
              {page === 'moniteur' && (
                <ComingPage
                  title="Moniteur"
                  summary="L'état du PC en direct."
                  note="Cet écran est en cours de refonte dans le nouveau design. L'ancien écran Moniteur reste disponible dans la version actuelle de l'application."
                />
              )}
              {page === 'jeu' && (
                <ComingPage
                  title="Jeu"
                  summary="Préparer le PC avant de jouer, et mesurer ce que ça change."
                  note="Cet écran n'existe pas encore : le booster de jeu est la prochaine grande étape."
                />
              )}
            </motion.div>
          </AnimatePresence>
        </main>
      </div>
    </MotionConfig>
  )
}
