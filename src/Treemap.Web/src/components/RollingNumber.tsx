import { animate, motion, useMotionValue, useTransform } from 'motion/react'
import { useEffect } from 'react'

interface RollingNumberProps {
  value: number
  format?: (value: number) => string
}

/** A figure that rolls to its new value instead of jumping, so a change is seen rather than missed. */
export function RollingNumber({ value, format = (v) => String(Math.round(v)) }: RollingNumberProps) {
  const motionValue = useMotionValue(value)
  const text = useTransform(motionValue, format)

  useEffect(() => {
    const controls = animate(motionValue, value, { duration: 0.7, ease: [0.25, 1, 0.5, 1] })
    return () => controls.stop()
  }, [motionValue, value])

  return <motion.span className="num">{text}</motion.span>
}
