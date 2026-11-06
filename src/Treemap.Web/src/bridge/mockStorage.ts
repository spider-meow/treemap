import type {
  ChildDto,
  DeleteReplyDto,
  DeletionAssessment,
  DeletionPreview,
  DriveDto,
  HogDto,
  NodeDto,
  ScanProgressDto,
  ScanReplyDto,
  Verdict,
} from './types'

/** A stand-in for the C# storage engine, so the screen can be designed and tested in a plain browser. */

const GO = 1024 ** 3

interface MockNode {
  name: string
  path: string
  size: number
  files: number
  isFolder: boolean
  children: MockNode[]
}

function file(parent: string, name: string, size: number): MockNode {
  return { name, path: `${parent}\\${name}`, size, files: 1, isFolder: false, children: [] }
}

function folder(parent: string, name: string, build: (path: string) => MockNode[]): MockNode {
  const path = parent === '' ? name : `${parent.replace(/\\$/, '')}\\${name}`
  const children = build(path).sort((a, b) => b.size - a.size)
  return {
    name,
    path,
    size: children.reduce((s, c) => s + c.size, 0),
    files: children.reduce((s, c) => s + c.files, 0),
    isFolder: true,
    children,
  }
}

function sized(parent: string, name: string, bytes: number): MockNode {
  return folder(parent, name, (p) => [file(p, 'données.bin', bytes)])
}

function buildTree(): MockNode {
  return folder('', 'C:\\', (root) => [
    sized(root, 'Windows', 28 * GO),
    sized(root, 'Program Files', 41 * GO),
    folder(root, 'Jeux', (jeux) => [
      folder(jeux, 'steamapps', (s) => [
        folder(s, 'common', (c) => [sized(c, 'Elden Ring', 49 * GO), sized(c, 'Baldurs Gate 3', 122 * GO), sized(c, 'Hades', 14 * GO)]),
      ]),
    ]),
    folder(root, 'Users', (users) => [
      folder(users, 'arthu', (me) => [
        folder(me, 'AppData', (a) => [
          folder(a, 'Local', (l) => [
            folder(l, 'Packages', (pk) => [
              folder(pk, 'Microsoft.FlightSimulator_8wekyb3d8bbwe', (m) => [
                folder(m, 'LocalCache', (lc) => [
                  folder(lc, 'Packages', (p) => [
                    sized(p, 'Official', 118 * GO),
                    folder(p, 'Community', (cm) => [
                      sized(cm, 'orbx-scenery-france', 18.2 * GO),
                      sized(cm, 'pmdg-737-700', 6.3 * GO),
                      sized(cm, 'livery-pack-airlines', 5.4 * GO),
                      sized(cm, 'fenix-a320', 4.1 * GO),
                      sized(cm, 'navigraph-data', 0.4 * GO),
                    ]),
                    file(p, 'ROLLINGCACHE.CCC', 8 * GO),
                  ]),
                ]),
              ]),
            ]),
            sized(l, 'Temp', 3.2 * GO),
          ]),
        ]),
        folder(me, 'Downloads', (d) => [file(d, 'installeur-cuda.exe', 3.1 * GO), file(d, 'archive-photos.zip', 6.4 * GO), file(d, 'notes.pdf', 0.002 * GO)]),
        sized(me, 'Documents', 6 * GO),
      ]),
    ]),
  ])
}

function verdictOf(node: MockNode): { verdict: Verdict; title: string } {
  const p = node.path
  if (/^C:\\(Windows|Program Files)/i.test(p) || /^C:\\$/.test(p) || /\\(Users|arthu|AppData|Local)$/i.test(p) || /\\Packages$/i.test(p)) {
    return { verdict: 'blocked', title: 'Dossier système' }
  }
  if (/\\Official/i.test(p) || /\\steamapps\\common\\[^\\]+/i.test(p)) {
    return { verdict: 'uninstallOnly', title: /Official/i.test(p) ? 'Microsoft Flight Simulator' : p.split('\\').at(-1) ?? '' }
  }
  if (/\\Community/i.test(p)) return { verdict: 'allowed', title: 'Add-on de Microsoft Flight Simulator' }
  return { verdict: 'allowed', title: 'Élément' }
}

function assess(node: MockNode): DeletionAssessment {
  const { verdict, title } = verdictOf(node)
  const notes: Record<Verdict, string> = {
    allowed: 'Aucune particularité connue pour cet élément.',
    uninstallOnly: 'Ce sont les fichiers du jeu lui-même : désinstalle-le depuis son lanceur.',
    blocked: 'Géré par Windows : il ne se supprime pas à la main.',
  }
  const typed = /Documents|Downloads$/.test(node.path)
  return {
    path: node.path,
    verdict,
    confirmation: typed ? 'typed' : 'standard',
    title,
    note: verdict === 'allowed' && /Community/.test(node.path) ? 'Contenu additionnel installé à la main : le retirer ne touche pas au jeu.' : notes[verdict],
    uninstall:
      verdict === 'uninstallOnly'
        ? [{ launcher: 'steam', label: 'Désinstaller dans Steam', uri: 'steam://nav/games' }]
        : [],
  }
}

export function createMockStorage(emit: (type: string, payload: unknown) => void) {
  let root = buildTree()
  let bin = { sizeBytes: 2.4 * GO, itemCount: 18 }

  const find = (path: string, from: MockNode = root): MockNode | undefined => {
    if (from.path.toLowerCase() === path.toLowerCase()) return from
    return from.children.map((c) => (path.toLowerCase().startsWith(c.path.toLowerCase()) ? find(path, c) : undefined)).find(Boolean)
  }

  const parentOf = (path: string, from: MockNode = root): MockNode | undefined =>
    from.children.find((c) => c.path === path)
      ? from
      : from.children.map((c) => (path.startsWith(c.path) ? parentOf(path, c) : undefined)).find(Boolean)

  const recompute = (node: MockNode): void => {
    if (!node.isFolder) return
    node.children.forEach(recompute)
    node.size = node.children.reduce((s, c) => s + c.size, 0)
    node.files = node.children.reduce((s, c) => s + c.files, 0)
  }

  const hogs = (): HogDto[] => {
    const result: HogDto[] = []
    const games = find('C:\\Jeux\\steamapps\\common')
    if (games) {
      result.push({
        ruleId: 'games',
        title: 'Bibliothèques de jeux',
        explanation: 'Jeux installés (Steam, Epic Games, Xbox).',
        advice: 'Désinstalle depuis le lanceur les jeux que tu ne lances plus : ils pèsent souvent 50 à 150 Go chacun.',
        sizeBytes: games.size,
        locations: [{ path: games.path, sizeBytes: games.size, verdict: 'blocked' }],
        moreCount: 0,
      })
    }
    const downloads = find('C:\\Users\\arthu\\Downloads')
    if (downloads) {
      result.push({
        ruleId: 'downloads',
        title: 'Téléchargements',
        explanation: 'Dossier Téléchargements de ton profil.',
        advice: 'Trie ou supprime les installeurs et archives déjà utilisés.',
        sizeBytes: downloads.size,
        locations: [{ path: downloads.path, sizeBytes: downloads.size, verdict: 'allowed' }],
        moreCount: 0,
      })
    }
    return result.sort((a, b) => b.sizeBytes - a.sizeBytes)
  }

  const toChild = (c: MockNode): ChildDto => ({
    path: c.path,
    name: c.name,
    kind: c.isFolder ? 'folder' : 'file',
    sizeBytes: c.size,
    fileCount: c.files,
    hasChildren: c.isFolder && c.children.length > 0,
    ...verdictOf(c),
  })

  return {
    recycleBin: () => bin,

    emptyRecycleBin(confirmation: string | null) {
      if (confirmation?.trim().toUpperCase() !== 'SUPPRIMER') throw new Error('Confirmation manquante : tape SUPPRIMER pour vider la Corbeille.')
      bin = { sizeBytes: 0, itemCount: 0 }
      return bin
    },

    drives: (): DriveDto[] => [
      { path: 'C:\\', label: 'Windows', totalBytes: 930 * GO, freeBytes: 107 * GO },
      { path: 'D:\\', label: 'Jeux', totalBytes: 1862 * GO, freeBytes: 900 * GO },
    ],

    async scan(): Promise<ScanReplyDto> {
      root = buildTree()
      const steps = 8
      for (let i = 1; i <= steps; i++) {
        await new Promise((resolve) => setTimeout(resolve, 180))
        const progress: ScanProgressDto = {
          filesScanned: Math.round((root.files * i) / steps),
          bytesScanned: Math.round((root.size * i) / steps),
          currentPath: i % 2 ? 'C:\\Users\\arthu\\AppData\\Local\\Packages' : 'C:\\Jeux\\steamapps\\common',
        }
        emit('storage.progress', progress)
      }
      return {
        cancelled: false,
        summary: {
          rootPath: root.path,
          rootName: root.name,
          sizeBytes: root.size,
          fileCount: root.files,
          folderCount: 4210,
          elapsedSeconds: 1.4,
          notes: ['Windows compte 11,2 Go de plus que ce qui a pu être lu : points de restauration et dossiers réservés au système.'],
        },
        hogs: hogs(),
      }
    },

    node(path: string): NodeDto {
      const node = find(path)
      if (!node) throw new Error("Cet élément n'est pas dans l'analyse en cours.")
      const parent = parentOf(node.path)
      return {
        path: node.path,
        name: node.name,
        kind: node.isFolder ? 'folder' : 'file',
        sizeBytes: node.size,
        fileCount: node.files,
        parentPath: parent?.path ?? null,
        ...verdictOf(node),
        children: node.children.map(toChild),
        hiddenCount: 0,
        hiddenBytes: 0,
      }
    },

    preview(paths: string[], permanent = false): DeletionPreview {
      const items = paths
        .map((p) => find(p))
        .filter((n): n is MockNode => n !== undefined)
        .map((n) => ({ path: n.path, name: n.name, sizeBytes: n.size, fileCount: n.files, isFolder: n.isFolder, assessment: assess(n) }))
      const canProceed = items.length > 0 && items.every((i) => i.assessment.verdict === 'allowed')
      const typed = items.some((i) => i.assessment.confirmation === 'typed')
      return { items, totalBytes: items.reduce((s, i) => s + i.sizeBytes, 0), canProceed, confirmationPhrase: canProceed && (typed || permanent) ? 'SUPPRIMER' : null }
    },

    delete(paths: string[], confirmation: string | null, permanent = false): DeleteReplyDto {
      const preview = this.preview(paths, permanent)
      if (!preview.canProceed) return { results: [], freedBytes: 0, refused: true, reason: 'Au moins un élément ne peut pas être supprimé d\'ici : rien n\'a été touché.', hogs: hogs(), rootSizeBytes: root.size }
      if (preview.confirmationPhrase && confirmation?.trim().toUpperCase() !== preview.confirmationPhrase) {
        return { results: [], freedBytes: 0, refused: true, reason: `Confirmation manquante : tape ${preview.confirmationPhrase} pour continuer.`, hogs: hogs(), rootSizeBytes: root.size }
      }
      for (const item of preview.items) {
        const parent = parentOf(item.path)
        if (parent) parent.children = parent.children.filter((c) => c.path !== item.path)
      }
      recompute(root)
      if (!permanent) bin = { sizeBytes: bin.sizeBytes + preview.totalBytes, itemCount: bin.itemCount + preview.items.length }
      return { results: preview.items.map((i) => ({ path: i.path, ok: true, error: null })), freedBytes: preview.totalBytes, refused: false, reason: null, hogs: hogs(), rootSizeBytes: root.size }
    },
  }
}
