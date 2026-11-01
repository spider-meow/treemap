import { useSize } from '../hooks/useSize'
import { lastPoint, linePath } from '../lib/chart'
import './line-chart.css'

export interface ChartSeries {
  label: string
  values: readonly number[]
  /** Second series of a pair: drawn dashed so the two stay apart without relying on colour. */
  dashed?: boolean
}

interface LineChartProps {
  series: ChartSeries[]
  /** Top of the scale. */
  max: number
  /** Number of readings the window holds; the newest sits at the right edge. */
  capacity: number
  /** Seconds covered by the window, for the time axis. */
  windowSeconds: number
  formatTick: (value: number) => string
  /** What a reader who cannot see the chart gets: the current reading in words. */
  summary: string
}

const GUTTER = 56
const BOTTOM = 22
const TOP = 8
const RIGHT = 10

/**
 * A ruled time series: three hairlines (0, half, full scale) with their values, a time axis, one line per series
 * and a square marker on the newest reading. No fill, no gradient: the line is the content.
 */
export function LineChart({ series, max, capacity, windowSeconds, formatTick, summary }: LineChartProps) {
  const [ref, size] = useSize<HTMLDivElement>()
  const plotW = Math.max(0, size.w - GUTTER - RIGHT)
  const plotH = Math.max(0, size.h - TOP - BOTTOM)
  const ticks = [1, 0.5, 0]
  const minutes = windowSeconds / 60

  return (
    <div ref={ref} className="line-chart" role="img" aria-label={summary}>
      {size.w > 0 && (
        <svg width={size.w} height={size.h} viewBox={`0 0 ${size.w} ${size.h}`} aria-hidden="true">
          <g transform={`translate(${GUTTER} ${TOP})`}>
            {ticks.map((t) => (
              <g key={t} transform={`translate(0 ${plotH * (1 - t)})`}>
                <line className="line-chart__grid" x1={0} x2={plotW} />
                <text className="line-chart__tick" x={-8} y={4} textAnchor="end">
                  {formatTick(max * t)}
                </text>
              </g>
            ))}

            <text className="line-chart__tick" x={0} y={plotH + 17} textAnchor="start">
              {`−${minutes} min`}
            </text>
            <text className="line-chart__tick" x={plotW / 2} y={plotH + 17} textAnchor="middle">
              {`−${minutes / 2} min`}
            </text>
            <text className="line-chart__tick" x={plotW} y={plotH + 17} textAnchor="end">
              maintenant
            </text>

            {series.map((s) => (
              <path
                key={s.label}
                className={`line-chart__line${s.dashed ? ' line-chart__line--dashed' : ''}`}
                d={linePath(s.values, capacity, max, plotW, plotH)}
              />
            ))}

            {series.map((s) => {
              const point = lastPoint(s.values, capacity, max, plotW, plotH)
              return (
                point && (
                  <rect
                    key={s.label}
                    className={`line-chart__marker${s.dashed ? ' line-chart__marker--hollow' : ''}`}
                    x={point.x - 4}
                    y={point.y - 4}
                    width={8}
                    height={8}
                  />
                )
              )
            })}
          </g>
        </svg>
      )}
    </div>
  )
}

/** Legend for charts with two series: text and line style, never colour alone. */
export function ChartLegend({ items }: { items: ReadonlyArray<{ label: string; dashed?: boolean }> }) {
  return (
    <ul className="chart-legend" aria-hidden="true">
      {items.map((item) => (
        <li key={item.label}>
          <svg width="28" height="8" viewBox="0 0 28 8">
            <line x1="0" x2="28" y1="4" y2="4" className={`line-chart__line${item.dashed ? ' line-chart__line--dashed' : ''}`} />
          </svg>
          {item.label}
        </li>
      ))}
    </ul>
  )
}
