import { OrbitControls, OrthographicCamera } from '@react-three/drei'
import { Canvas, useThree } from '@react-three/fiber'
import { useMemo } from 'react'
import type { SystemSpecs } from '../../bridge/types'
import { CATALOG } from '../../lib/names'
import type { PartId } from '../../lib/parts'
import { BoardModel, COLOR, CpuModel, GpuModel, MemoryModel, PartGroup, StorageModel, type PartState } from './parts3d'

interface PcSceneProps {
  specs: SystemSpecs
  exploded: boolean
  selected: PartId | null
  hovered: PartId | null
  reducedMotion: boolean
  onHover: (id: PartId | null) => void
  onSelect: (id: PartId | null) => void
}

/** Drive positions on the board (x, z): M.2 slots first, then free spots. */
const DRIVE_SPOTS: ReadonlyArray<[number, number]> = [
  [-1.25, -0.45],
  [-1.25, 0.05],
  [-0.6, 1.1],
]

function supportsWebGL(): boolean {
  try {
    const canvas = document.createElement('canvas')
    return !!(canvas.getContext('webgl2') ?? canvas.getContext('webgl'))
  } catch {
    return false
  }
}

/** Camera and controls, sized so the whole exploded drawing fits the stage whatever its size. */
function Rig({ reducedMotion }: { reducedMotion: boolean }) {
  const size = useThree((state) => state.size)
  const base = Math.max(30, Math.min(size.width / 5.2, size.height / 6.6))

  return (
    <>
      <OrthographicCamera makeDefault position={[7, 5.2, 7]} zoom={base} near={-50} far={50} />
      <OrbitControls
        makeDefault
        target={[0, 1.9, 0]}
        enablePan={false}
        enableDamping={!reducedMotion}
        minPolarAngle={0.55}
        maxPolarAngle={1.45}
        minZoom={base * 0.6}
        maxZoom={base * 2}
      />
    </>
  )
}

/**
 * The PC as an exploded axonometric drawing: parts at their real place on the board, lifted apart on dashed drop lines.
 * Flat fills and ink edges only; the camera is orthographic like a catalogue plate. Frames are drawn on demand,
 * so the scene costs nothing while nothing moves.
 */
export function PcScene({ specs, exploded, selected, hovered, reducedMotion, onHover, onSelect }: PcSceneProps) {
  const webgl = useMemo(supportsWebGL, [])

  if (!webgl) {
    return (
      <p className="exploded__nogl" role="status">
        Ton navigateur ne peut pas afficher la 3D ici. La liste des pièces reste disponible.
      </p>
    )
  }

  const stateOf = (id: PartId): PartState => (selected === id ? 'selected' : hovered === id ? 'hovered' : 'idle')
  const common = { exploded, reducedMotion, onHover, onSelect: (id: PartId) => onSelect(id) }
  const drives = specs.disks.slice(0, DRIVE_SPOTS.length)

  return (
    <Canvas frameloop="demand" dpr={[1, 2]} onPointerMissed={() => onSelect(null)} aria-label="Vue 3D éclatée des composants du PC">
      <color attach="background" args={[COLOR.cream]} />
      <ambientLight intensity={1.1} />
      <directionalLight position={[4, 8, 5]} intensity={1.2} />
      <Rig reducedMotion={reducedMotion} />

      {specs.machine && (
        <PartGroup id="board" at={[0, 0]} assembledY={0} explodedY={0} tagY={0.5} index={CATALOG.board} title="Carte mère" {...common}>
          <BoardModel state={stateOf('board')} />
        </PartGroup>
      )}

      {specs.disks.length > 0 &&
        drives.map((disk, i) => (
          <PartGroup
            key={disk.name + i}
            id="storage"
            at={DRIVE_SPOTS[i]}
            assembledY={0.075}
            explodedY={2.0 + i * 0.5}
            tagY={0.35}
            index={CATALOG.storage}
            title="Stockage"
            showTag={i === 0}
            {...common}
          >
            <StorageModel state={stateOf('storage')} compact={/nvme|m\.?2|ssd/i.test(`${disk.kind} ${disk.bus ?? ''}`)} />
          </PartGroup>
        ))}

      {specs.gpus[0] && (
        <PartGroup id="gpu" at={[-0.1, 0.55]} assembledY={0.27} explodedY={1.2} tagY={0.5} index={CATALOG.gpu} title="Carte graphique" {...common}>
          <GpuModel state={stateOf('gpu')} />
        </PartGroup>
      )}

      {specs.memory && (
        <PartGroup
          id="memory"
          at={[0.7 + ((Math.max(1, Math.min(4, specs.memory.modules.length)) - 1) * 0.14) / 2, -0.45]}
          assembledY={0.39}
          explodedY={2.9}
          tagY={0.6}
          index={CATALOG.memory}
          title="Mémoire"
          {...common}
        >
          <MemoryModel state={stateOf('memory')} count={Math.max(1, Math.min(4, specs.memory.modules.length))} />
        </PartGroup>
      )}

      {specs.cpu && (
        <PartGroup id="cpu" at={[-0.35, -0.45]} assembledY={0.15} explodedY={3.8} tagY={1.05} index={CATALOG.cpu} title="Processeur" {...common}>
          <CpuModel state={stateOf('cpu')} />
        </PartGroup>
      )}
    </Canvas>
  )
}
