import { describe, expect, it } from 'vitest'
import { cleanCpuName, friendlySlot, joinName } from './names'

describe('names', () => {
  it('cleans the processor name', () => {
    expect(cleanCpuName('Intel(R) Core(TM) i5-10400F CPU @ 2.90GHz')).toBe('Intel Core i5-10400F')
    expect(cleanCpuName('AMD Ryzen 7 5800X 8-Core Processor')).toBe('AMD Ryzen 7 5800X 8-Core Processor')
  })

  it('turns memory slot ids into readable names', () => {
    expect(friendlySlot('ChannelA-DIMM0', 0)).toBe('Barrette A1')
    expect(friendlySlot('ChannelB-DIMM1', 1)).toBe('Barrette B2')
    expect(friendlySlot('BANK 3', 2)).toBe('BANK 3')
    expect(friendlySlot(null, 2)).toBe('Barrette 3')
  })

  it('joins names skipping empty parts', () => {
    expect(joinName('American Megatrends Inc.', '1.80')).toBe('American Megatrends Inc. 1.80')
    expect(joinName(null, 'x', undefined)).toBe('x')
  })
})
