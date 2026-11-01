import type { MetricsSnapshot, SystemSpecs } from './types'

const GiB = 1024 ** 3

/**
 * Stand-in for the C# engine when the UI runs in a plain browser (design work, tests). The values are a
 * snapshot of a real gaming PC, so layouts are judged against realistic content, not lorem ipsum.
 */
export const mockSpecs: SystemSpecs = {
  os: {
    name: 'Microsoft Windows 11 Famille',
    version: '10.0.26200',
    build: '26200',
    architecture: '64 bits',
    lastBoot: new Date(Date.now() - (8 * 24 + 20) * 3600_000).toISOString(),
  },
  machine: {
    manufacturer: 'Micro-Star International Co., Ltd.',
    model: 'MS-7C89',
    boardManufacturer: 'Micro-Star International Co., Ltd.',
    boardProduct: 'H410M-A PRO (MS-7C89)',
    biosVendor: 'American Megatrends Inc.',
    biosVersion: '1.80',
    biosDate: '2020-11-16T00:00:00',
  },
  cpu: {
    name: 'Intel(R) Core(TM) i5-10400F CPU @ 2.90GHz',
    manufacturer: 'GenuineIntel',
    cores: 6,
    logicalProcessors: 12,
    maxClockMHz: 2904,
    socket: 'U3E1',
    l2CacheKb: 1536,
    l3CacheKb: 12288,
  },
  gpus: [{ name: 'NVIDIA GeForce RTX 3060', driverVersion: '32.0.16.1692', videoMemoryBytes: 12 * GiB }],
  memory: {
    totalBytes: 15.9 * GiB,
    modules: [
      { slot: 'ChannelA-DIMM0', capacityBytes: 8 * GiB, speedMHz: 2133, manufacturer: null, partNumber: '8GBF1X08QFHH38-135-K', type: 'DDR4' },
      { slot: 'ChannelB-DIMM0', capacityBytes: 8 * GiB, speedMHz: 2133, manufacturer: null, partNumber: '8GBF1X08QFHH38-135-K', type: 'DDR4' },
    ],
  },
  disks: [{ name: 'CT1000P2SSD8', kind: 'SSD NVMe', bus: 'NVMe', sizeBytes: 932 * GiB, health: 'Sain' }],
  volumes: [
    { name: 'C:\\', label: null, fileSystem: 'NTFS', totalBytes: 930 * GiB, freeBytes: 104 * GiB, usedBytes: 826 * GiB, usedFraction: 826 / 930 },
  ],
  warnings: [],
}

/** A slow random walk, so graphs and rolling numbers have something to do during design work. */
export function createMockMetrics(): () => MetricsSnapshot {
  let cpu = 14
  let gpu = 6
  let temperature = 48
  const walk = (value: number, step: number, min: number, max: number) =>
    Math.min(max, Math.max(min, value + (Math.random() - 0.5) * step))

  return () => {
    cpu = walk(cpu, 12, 3, 90)
    gpu = walk(gpu, 10, 0, 95)
    temperature = walk(temperature, 2, 40, 78)
    const used = 12.9 * GiB
    return {
      timestamp: new Date().toISOString(),
      cpuLoadPercent: cpu,
      memoryUsedBytes: used,
      memoryTotalBytes: 15.9 * GiB,
      memoryPercent: (100 * used) / (15.9 * GiB),
      diskReadBytesPerSecond: Math.random() < 0.3 ? Math.random() * 40e6 : Math.random() * 2e5,
      diskWriteBytesPerSecond: Math.random() < 0.2 ? Math.random() * 20e6 : Math.random() * 1e5,
      diskActivePercent: Math.random() * 6,
      networkDownBytesPerSecond: Math.random() * 3e5,
      networkUpBytesPerSecond: Math.random() * 6e4,
      gpu: {
        name: 'NVIDIA GeForce RTX 3060',
        loadPercent: gpu,
        memoryUsedBytes: 1.7 * GiB,
        memoryTotalBytes: 12 * GiB,
        memoryPercent: (100 * 1.7) / 12,
        temperatureCelsius: Math.round(temperature),
        powerWatts: 13 + gpu * 1.4,
        clockMHz: 210 + gpu * 14,
        fanPercent: 0,
      },
    }
  }
}
