import { useEffect, useRef, useState } from 'react'

/** The content size of an element, kept up to date as it is resized. */
export function useSize<T extends HTMLElement>() {
  const ref = useRef<T>(null)
  const [size, setSize] = useState({ w: 0, h: 0 })

  useEffect(() => {
    const element = ref.current
    if (!element) return
    const observer = new ResizeObserver(([entry]) => {
      setSize({ w: Math.floor(entry.contentRect.width), h: Math.floor(entry.contentRect.height) })
    })
    observer.observe(element)
    return () => observer.disconnect()
  }, [])

  return [ref, size] as const
}
