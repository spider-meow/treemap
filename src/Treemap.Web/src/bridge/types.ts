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

// ---- Storage -------------------------------------------------------------------------------------------------------

export type NodeKind = 'folder' | 'file' | 'otherFiles'

/** What the engine allows for an item: delete (Recycle Bin), only through the uninstaller, or nothing. */
export type Verdict = 'allowed' | 'uninstallOnly' | 'blocked'

export interface DriveDto {
  path: string
  label: string
  totalBytes: number
  freeBytes: number
}

export interface ChildDto {
  path: string
  name: string
  kind: NodeKind
  sizeBytes: number
  fileCount: number
  hasChildren: boolean
  verdict: Verdict
  title: string
}

export interface NodeDto {
  path: string
  name: string
  kind: NodeKind
  sizeBytes: number
  fileCount: number
  parentPath: string | null
  verdict: Verdict
  title: string
  children: ChildDto[]
  hiddenCount: number
  hiddenBytes: number
}

export interface HogLocationDto {
  path: string
  sizeBytes: number
  verdict: Verdict
}

export interface HogDto {
  ruleId: string
  title: string
  explanation: string
  advice: string
  sizeBytes: number
  locations: HogLocationDto[]
  moreCount: number
}

export interface ScanSummaryDto {
  rootPath: string
  rootName: string
  sizeBytes: number
  fileCount: number
  folderCount: number
  elapsedSeconds: number
  notes: string[]
}

export interface ScanReplyDto {
  cancelled: boolean
  summary: ScanSummaryDto | null
  hogs: HogDto[]
}

export interface ScanProgressDto {
  filesScanned: number
  bytesScanned: number
  currentPath: string
}

export interface UninstallHint {
  launcher: string
  label: string
  uri: string
}

export interface DeletionAssessment {
  path: string
  verdict: Verdict
  confirmation: 'standard' | 'typed'
  title: string
  note: string
  uninstall: UninstallHint[]
}

export interface DeletionPreviewItem {
  path: string
  name: string
  sizeBytes: number
  fileCount: number
  isFolder: boolean
  assessment: DeletionAssessment
}

export interface DeletionPreview {
  items: DeletionPreviewItem[]
  totalBytes: number
  canProceed: boolean
  /** The word to type before deleting, when the engine asks for it. */
  confirmationPhrase: string | null
}

export interface DeletionResult {
  path: string
  ok: boolean
  error: string | null
}

export interface DeleteReplyDto {
  results: DeletionResult[]
  freedBytes: number
  refused: boolean
  reason: string | null
  hogs: HogDto[]
  rootSizeBytes: number
}

// ---- Overlay -------------------------------------------------------------------------------------------------------

export type OverlayCorner = 'topLeft' | 'topRight' | 'bottomLeft' | 'bottomRight'

export interface OverlaySettings {
  enabled: boolean
  showFps: boolean
  showCpu: boolean
  showGpu: boolean
  showGpuTemperature: boolean
  showMemory: boolean
  showVideoMemory: boolean
  corner: OverlayCorner
  opacity: number
}

export type FpsStatus = 'notStarted' | 'running' | 'needsAdministrator' | 'failed'

export interface OverlayState {
  settings: OverlaySettings
  minOpacity: number
  fpsStatus: FpsStatus
  fpsMessage: string | null
  canRelaunchAsAdmin: boolean
  hotkey: { registered: boolean; description: string }
}

export interface FpsReading {
  processName: string
  fps: number
}

export interface RecycleBinInfo {
  sizeBytes: number
  itemCount: number
}

// ---- Game booster -------------------------------------------------------------------------------------------------

export interface BoosterSettings {
  closeApps: boolean
  highPriority: boolean
  highPerformancePlan: boolean
  auto: boolean
  closeList: string[]
  knownGames: string[]
  pendingPowerPlan: string | null
}

export interface BoostSnapshot {
  ramUsedBytes: number
  processCount: number
  cpuPercent: number
}

export interface BoostStep {
  id: 'priority' | 'power' | 'close'
  title: string
  done: boolean
  detail: string
}

export interface ClosedApp {
  name: string
  path: string | null
  closed: boolean
  relaunchable: boolean
  relaunched: boolean
}

export interface BoostReport {
  gameName: string
  gameProcessId: number
  startedAt: string
  endedAt: string | null
  before: BoostSnapshot
  after: BoostSnapshot | null
  steps: BoostStep[]
  closedApps: ClosedApp[]
  restoredPowerPlan: string | null
}

export interface BoosterGame {
  id: number
  name: string
  title: string | null
  known: boolean
}

export interface BoosterApp {
  id: number
  name: string
  title: string | null
  memoryBytes: number
  picked: boolean
}

export interface BoosterState {
  settings: BoosterSettings
  games: BoosterGame[]
  apps: BoosterApp[]
  sessionActive: boolean
  session: BoostReport | null
  powerPlan: { guid: string; name: string } | null
  highPerformanceAvailable: boolean
}
