import { describe, expect, it } from 'vitest'
import { formatBytes, formatFraction, formatGigahertz, formatRate, formatUptime, levelOf } from './format'

const nbsp = (s: string) => s.replace(/\s/g, ' ')

describe('formatBytes', () => {
  it.each([
    [0, '0 o'],
    [512, '512 o'],
    [1024, '1,0 Ko'],
    [1536, '1,5 Ko'],
    [105 * 1024 ** 3, '105 Go'],
    [930 * 1024 ** 3, '930 Go'],
    [99.5 * 1024 ** 3, '99,5 Go'],
    [1024 ** 4, '1,0 To'],
  ])('formats %d as %s', (bytes, expected) => {
    expect(nbsp(formatBytes(bytes))).toBe(expected)
  })

  it('handles negatives and non-finite values', () => {
    expect(nbsp(formatBytes(-1024))).toBe('-1,0 Ko')
    expect(formatBytes(Number.NaN)).toBe('—')
  })
})

describe('other formatters', () => {
  it('formats rates, fractions and clocks', () => {
    expect(nbsp(formatRate(2048))).toBe('2,0 Ko/s')
    expect(nbsp(formatFraction(0.89))).toBe('89 %')
    expect(nbsp(formatGigahertz(2904))).toBe('2,90 GHz')
  })

  it('formats uptime in the largest sensible units', () => {
    expect(nbsp(formatUptime(8 * 86400000 + 20 * 3600000))).toBe('8 j 20 h')
    expect(nbsp(formatUptime(90 * 60000))).toBe('1 h 30 min')
    expect(nbsp(formatUptime(5 * 60000))).toBe('5 min')
  })

  it('flags usage from 80 % as high', () => {
    expect(levelOf(0.79)).toBe('ok')
    expect(levelOf(0.8)).toBe('high')
  })
})
