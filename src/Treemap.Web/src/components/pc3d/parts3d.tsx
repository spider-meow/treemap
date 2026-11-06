import { Edges, Html } from '@react-three/drei'
import { useFrame, type ThreeEvent } from '@react-three/fiber'
import { useEffect, useMemo, useRef, type ReactNode } from 'react'
import { BufferGeometry, Group, Line, LineDashedMaterial, Vector3 } from 'three'
import type { PartId } from '../../lib/parts'

/** The palette of the interface, in the three.js world (kept in step with styles/tokens.css). */
export const COLOR = {
  ink: '#0d2b24',
  cream: '#f4f1e1',
  paper: '#e8e4cc',
  forest: '#015d47',
  crest: '#e0f354',
  mandarin: '#fd550a',
} as const

export const BOARD_TOP = 0.03

export type PartState = 'idle' | 'hovered' | 'selected'

/** Fill of a part's body: calm by default, crest under the pointer, mandarin once picked (like the treemap). */
export function fillFor(base: string, state: PartState): string {
  return state === 'selected' ? COLOR.mandarin : state === 'hovered' ? COLOR.crest : base
}

interface BlockProps {
  size: [number, number, number]
  position?: [number, number, number]
  rotation?: [number, number, number]
  color: string
}

/** A flat-coloured box with ink edges: the whole drawing vocabulary of the scene. */
export function Block({ size, position = [0, 0, 0], rotation, color }: BlockProps) {
  return (
    <mesh position={position} rotation={rotation}>
      <boxGeometry args={size} />
      <meshLambertMaterial color={color} />
      <Edges color={COLOR.ink} threshold={20} />
    </mesh>
  )
}

export function Disc({ radius, height, position, rotation, color }: { radius: number; height: number; position: [number, number, number]; rotation?: [number, number, number]; color: string }) {
  return (
    <mesh position={position} rotation={rotation}>
      <cylinderGeometry args={[radius, radius, height, 28]} />
      <meshLambertMaterial color={color} />
      <Edges color={COLOR.ink} threshold={30} />
    </mesh>
  )
}

interface PartGroupProps {
  id: PartId
  /** Position on the board (x, z). */
  at: [number, number]
  /** Height of the part's origin once assembled and once exploded. */
  assembledY: number
  explodedY: number
  exploded: boolean
  reducedMotion: boolean
  /** Where the catalogue tag sits above the part's origin. */
  tagY: number
  index: string
  title: string
  onHover: (id: PartId | null) => void
  onSelect: (id: PartId) => void
  /** Several objects can share one catalogue number (two drives): only one carries the tag. */
  showTag?: boolean
  children: ReactNode
}

/**
 * Wraps one hardware object: moves it between assembled and exploded, draws the dashed drop line down to the board
 * (the exploded-view convention) and its catalogue tag, and reports pointer events.
 */
export function PartGroup({ id, at, assembledY, explodedY, exploded, reducedMotion, tagY, index, title, onHover, onSelect, showTag = true, children }: PartGroupProps) {
  const group = useRef<Group>(null)
  const progress = useRef(exploded ? 1 : 0)
  const leader = useMemo(() => {
    const line = new Line(new BufferGeometry(), new LineDashedMaterial({ color: COLOR.ink, dashSize: 0.08, gapSize: 0.07 }))
    line.frustumCulled = false
    return line
  }, [])

  useEffect(() => () => {
    leader.geometry.dispose()
    ;(leader.material as LineDashedMaterial).dispose()
  }, [leader])

  useFrame((state, delta) => {
    const target = exploded ? 1 : 0
    if (reducedMotion) {
      progress.current = target
    } else {
      progress.current += (target - progress.current) * (1 - Math.exp(-delta * 8))
      if (Math.abs(target - progress.current) < 0.002) progress.current = target
      else state.invalidate()
    }

    const y = assembledY + (explodedY - assembledY) * progress.current
    group.current?.position.set(at[0], y, at[1])

    // Local drop line: from the part's origin straight down to the board.
    const drop = BOARD_TOP - y
    leader.geometry.setFromPoints([new Vector3(0, 0, 0), new Vector3(0, drop, 0)])
    leader.computeLineDistances()
    leader.visible = Math.abs(drop) > 0.15
  })

  const over = (e: ThreeEvent<PointerEvent>) => {
    e.stopPropagation()
    document.body.style.cursor = 'pointer'
    onHover(id)
  }
  const out = () => {
    document.body.style.cursor = ''
    onHover(null)
  }

  return (
    <group ref={group} position={[at[0], assembledY, at[1]]}>
      <primitive object={leader} />
      <group
        onPointerOver={over}
        onPointerOut={out}
        onClick={(e) => {
          e.stopPropagation()
          onSelect(id)
        }}
      >
        {children}
      </group>
      {showTag && (
        <Html position={[0, tagY, 0]} center zIndexRange={[8, 0]} style={{ pointerEvents: 'none' }}>
          <div className="pc-tag" aria-hidden="true">
            <span className="pc-tag__index">{index}</span>
            <span className="pc-tag__title">{title}</span>
          </div>
        </Html>
      )}
    </group>
  )
}

// ---- The parts ------------------------------------------------------------------------------------------------------

/** Motherboard: a slab with the socket, DIMM slots and the PCIe slot drawn on it. */
export function BoardModel({ state }: { state: PartState }) {
  return (
    <>
      <Block size={[3.2, 0.06, 2.5]} color={fillFor(COLOR.paper, state)} />
      <Block size={[0.55, 0.02, 0.55]} position={[-0.35, 0.04, -0.45]} color={COLOR.ink} />
      {[0, 1, 2, 3].map((i) => (
        <Block key={i} size={[0.06, 0.05, 1.1]} position={[0.7 + i * 0.14, 0.045, -0.45]} color={COLOR.ink} />
      ))}
      <Block size={[2.4, 0.04, 0.1]} position={[-0.1, 0.04, 0.55]} color={COLOR.ink} />
      <Block size={[0.5, 0.03, 0.5]} position={[-1.1, 0.045, 0.95]} color={COLOR.forest} />
    </>
  )
}

/** Tower cooler over the processor: base, a stack of fins and a fan on one side. */
export function CpuModel({ state }: { state: PartState }) {
  const fill = fillFor(COLOR.paper, state)
  return (
    <>
      <Block size={[0.9, 0.12, 0.9]} color={fill} />
      {Array.from({ length: 9 }, (_, i) => (
        <Block key={i} size={[0.03, 0.7, 0.8]} position={[-0.36 + i * 0.09, 0.41, 0]} color={fill} />
      ))}
      <Disc radius={0.33} height={0.04} position={[0.5, 0.41, 0]} rotation={[0, 0, Math.PI / 2]} color={COLOR.cream} />
      <Disc radius={0.09} height={0.06} position={[0.5, 0.41, 0]} rotation={[0, 0, Math.PI / 2]} color={COLOR.ink} />
    </>
  )
}

/** Memory sticks, one per module the machine reports. */
export function MemoryModel({ state, count }: { state: PartState; count: number }) {
  const fill = fillFor(COLOR.paper, state)
  return (
    <>
      {Array.from({ length: count }, (_, i) => {
        const x = (i - (count - 1) / 2) * 0.14
        return (
          <group key={i} position={[x, 0, 0]}>
            <Block size={[0.05, 0.7, 1.05]} color={fill} />
            {[-0.36, -0.12, 0.12, 0.36].map((z) => (
              <Block key={z} size={[0.07, 0.2, 0.18]} position={[0, 0.12, z]} color={COLOR.ink} />
            ))}
          </group>
        )
      })}
    </>
  )
}

/** Graphics card: a long shroud with two fans, the I/O bracket and the PCIe edge connector. */
export function GpuModel({ state }: { state: PartState }) {
  return (
    <>
      <Block size={[2.6, 0.3, 0.95]} color={fillFor(COLOR.forest, state)} />
      <Disc radius={0.36} height={0.03} position={[-0.6, 0.165, 0]} color={COLOR.cream} />
      <Disc radius={0.1} height={0.05} position={[-0.6, 0.17, 0]} color={COLOR.ink} />
      <Disc radius={0.36} height={0.03} position={[0.45, 0.165, 0]} color={COLOR.cream} />
      <Disc radius={0.1} height={0.05} position={[0.45, 0.17, 0]} color={COLOR.ink} />
      <Block size={[0.04, 0.75, 0.95]} position={[-1.32, -0.05, 0]} color={COLOR.ink} />
      <Block size={[1.2, 0.05, 0.08]} position={[-0.1, -0.175, -0.2]} color={COLOR.ink} />
    </>
  )
}

/** One drive: an M.2 stick for NVMe, a 2.5-inch slab otherwise. */
export function StorageModel({ state, compact }: { state: PartState; compact: boolean }) {
  const fill = fillFor(COLOR.crest, state)
  return compact ? (
    <>
      <Block size={[0.7, 0.03, 0.22]} color={fill} />
      <Block size={[0.18, 0.045, 0.16]} position={[-0.17, 0.01, 0]} color={COLOR.ink} />
      <Block size={[0.18, 0.045, 0.16]} position={[0.1, 0.01, 0]} color={COLOR.ink} />
    </>
  ) : (
    <>
      <Block size={[0.9, 0.1, 0.65]} color={fill} />
      <Block size={[0.5, 0.11, 0.3]} position={[0, 0, 0]} color={COLOR.ink} />
    </>
  )
}
