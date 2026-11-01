// Mirrors the JSON the C# engine sends (System.Text.Json, camelCase). Keep in sync with Treemap.Core.

export interface OsInfo {
  name: string
  version: string
  build: string
  architecture: string
  lastBoot: string | null
}

export interface MachineInfo {
  manufacturer: string | null
  model: string | null
  boardManufacturer: string | null
  boardProduct: string | null
  biosVendor: string | null
  biosVersion: string | null
  biosDate: string | null
}

export interface CpuInfo {
  name: string
  manufacturer: string | null
  cores: number
  logicalProcessors: number
  maxClockMHz: number
  socket: string | null
  l2CacheKb: number
  l3CacheKb: number
}

export interface GpuInfo {
  name: string
  driverVersion: string | null
  videoMemoryBytes: number | null
}

export interface MemoryModule {
  slot: string | null
  capacityBytes: number
  speedMHz: number
  manufacturer: string | null
  partNumber: string | null
  type: string
}

export interface MemoryInfo {
  totalBytes: number
  modules: MemoryModule[]
}

export interface PhysicalDiskInfo {
  name: string
  kind: string
  bus: string | null
  sizeBytes: number
  health: string | null
}

export interface VolumeInfo {
  name: string
  label: string | null
  fileSystem: string
  totalBytes: number
  freeBytes: number
  usedBytes: number
  usedFraction: number
}

export interface SystemSpecs {
  os: OsInfo | null
  machine: MachineInfo | null
  cpu: CpuInfo | null
  gpus: GpuInfo[]
  memory: MemoryInfo | null
  disks: PhysicalDiskInfo[]
  volumes: VolumeInfo[]
  warnings: string[]
}

export interface GpuMetrics {
  name: string
  loadPercent: number
  memoryUsedBytes: number
  memoryTotalBytes: number
  temperatureCelsius: number | null
  powerWatts: number | null
  clockMHz: number | null
  fanPercent: number | null
  memoryPercent: number
}

export interface MetricsSnapshot {
  timestamp: string
  cpuLoadPercent: number
  memoryUsedBytes: number
  memoryTotalBytes: number
  memoryPercent: number
  diskReadBytesPerSecond: number
  diskWriteBytesPerSecond: number
  diskActivePercent: number
  networkDownBytesPerSecond: number
  networkUpBytesPerSecond: number
  gpu: GpuMetrics | null
}
