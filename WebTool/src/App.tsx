import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import JSZip from 'jszip'
import { saveAs } from 'file-saver'
import {
  AlertCircle,
  ArrowUp,
  Copy,
  Download,
  ExternalLink,
  FileJson,
  Fingerprint,
  HelpCircle,
  Key,
  Map,
  Menu,
  Package,
  Plus,
  RefreshCw,
  Store,
  Target,
  ScrollText,
  Search,
  Settings,
  SlidersHorizontal,
  Syringe,
  Trash2,
  Upload,
  Wrench,
  X,
} from 'lucide-react'
import {
  ItemPackDefinition,
  ItemDefinition,
  QuestItemDefinition,
  KeyDefinition,
  ContainerDefinition,
  StimBuff,
  StimDefinition,
  MedKitDefinition,
  EffectsHealthProperties,
  EffectsDamageProperties,
  TraderDefinition,
  TraderItemEntry,
  ValidationError,
  createDefaultPack,
  createDefaultQuestItem,
  createDefaultKey,
  createDefaultContainer,
  createDefaultStim,
  createDefaultStimBuff,
  createDefaultMedkit,
  createDefaultTraderEntry,
  generateMongoId,
  getParentName,
  ITEM_PARENT_NAMES,
  VANILLA_TRADERS,
} from './types'
import { QUEST_TEMPLATES } from './generated_quest_templates'
import { KEY_TEMPLATES } from './generated_key_templates'
import { CONTAINER_TEMPLATES } from './generated_container_templates'
import { STIM_TEMPLATES } from './generated_stim_templates'
import { MEDKIT_TEMPLATES } from './generated_medkit_templates'
import apiItemNames from '../api_item_names.json'

type Tab = 'quest' | 'key' | 'container' | 'stim' | 'medkit'
type Mode = 'items' | 'traders' | 'bundles'
type RightPanel = 'editor' | 'json'

const RARITY_PVE = ['Not_exist', 'Common', 'Rare', 'Superrare', 'Legendary']
const HEX24 = /^[0-9a-fA-F]{24}$/

const SPT_COLOR_HEX: Record<string, string> = {
  default: '#ffffff',
  yellow: '#ffff00',
  blue: '#0000ff',
  green: '#00ff00',
  red: '#ff0000',
  violet: '#ee82ee',
  black: '#000000',
  grey: '#808080',
  white: '#ffffff',
  orange: '#ffa500',
}

const SPT_COLOR_NAMES = Object.keys(SPT_COLOR_HEX)

function colorToHex(color: string): string {
  const named = color.toLowerCase()
  if (SPT_COLOR_HEX[named]) return SPT_COLOR_HEX[named]
  if (/^#[0-9a-fA-F]{6}$/.test(color)) return color.toLowerCase()
  return '#ffffff'
}

function BackgroundColorPicker({
  value,
  onChange,
}: {
  value?: string
  onChange: (color: string) => void
}) {
  const hex = colorToHex(value || '')
  const isDefault = !value || value === 'default'
  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2">
        <select
          className="input-field flex-1"
          value={!value || value === 'default' ? 'default' : SPT_COLOR_NAMES.includes(value.toLowerCase()) ? value.toLowerCase() : '__custom__'}
          onChange={e => {
            const color = e.target.value
            if (color !== '__custom__') onChange(color)
          }}
        >
          <option value="default">Default (use base template)</option>
          {SPT_COLOR_NAMES.filter(c => c !== 'default').map(c => (
            <option key={c} value={c}>
              {c}
            </option>
          ))}
          <option value="__custom__">Custom</option>
        </select>
        <input
          type="color"
          className="w-10 h-10 rounded cursor-pointer bg-transparent border-0 p-0 shrink-0"
          value={hex}
          onChange={e => onChange(e.target.value.toLowerCase())}
        />
      </div>
      {isDefault && (
        <div className="text-xs text-tarkov-text-dim">
          The base template's background color will be kept.
        </div>
      )}
    </div>
  )
}

const ALL_ITEM_OPTIONS = Object.entries(apiItemNames as Record<string, { Name: string; ShortName: string }>)
  .map(([id, info]) => ({ value: id, label: info.Name, sub: `${info.ShortName} — ${id}` }))
  .sort((a, b) => a.label.localeCompare(b.label))

function getItemOrCategoryName(id: string): string | null {
  const item = (apiItemNames as Record<string, { Name: string; ShortName: string }>)[id]
  if (item) return `${item.Name} (${item.ShortName})`
  const category = ITEM_PARENT_NAMES[id]
  if (category) return `Category: ${category}`
  return null
}

function validatePack(pack: ItemPackDefinition): ValidationError[] {
  const errors: ValidationError[] = []
  if (!pack.name.trim()) errors.push({ field: 'name', message: 'Pack name is required.' })

  if (pack.questItems.length === 0 && pack.keys.length === 0 && pack.containers.length === 0 && pack.stims.length === 0 && pack.medkits.length === 0) {
    errors.push({ field: 'items', message: 'At least one item entry is required.' })
  }

  const seenIds = new Set<string>()
  const itemIds = new Set<string>([
    ...pack.questItems.map(i => i.id),
    ...pack.keys.map(k => k.id),
    ...pack.containers.map(c => c.id),
    ...pack.stims.map(s => s.id),
    ...pack.medkits.map(m => m.id),
  ])

  pack.questItems.forEach((item, i) => {
    const prefix = `questItems[${i}]`
    if (!HEX24.test(item.id)) errors.push({ field: `${prefix}.id`, message: 'Item ID must be 24 hex characters.' })
    if (seenIds.has(item.id.toLowerCase())) errors.push({ field: `${prefix}.id`, message: 'Duplicate ID.' })
    else seenIds.add(item.id.toLowerCase())
    if (!HEX24.test(item.baseTpl)) errors.push({ field: `${prefix}.baseTpl`, message: 'Base template must be 24 hex characters.' })
    if (!item.name.trim()) errors.push({ field: `${prefix}.name`, message: 'Name is required.' })
    if (!item.shortName.trim()) errors.push({ field: `${prefix}.shortName`, message: 'Short name is required.' })
    if (!item.description.trim()) errors.push({ field: `${prefix}.description`, message: 'Description is required.' })
    if (item.weight < 0) errors.push({ field: `${prefix}.weight`, message: 'Weight cannot be negative.' })
    if (item.stackMaxSize < 1) errors.push({ field: `${prefix}.stackMaxSize`, message: 'Stack max size must be >= 1.' })
  })

  pack.keys.forEach((key, i) => {
    const prefix = `keys[${i}]`
    if (!HEX24.test(key.id)) errors.push({ field: `${prefix}.id`, message: 'Key ID must be 24 hex characters.' })
    if (seenIds.has(key.id.toLowerCase())) errors.push({ field: `${prefix}.id`, message: 'Duplicate ID.' })
    else seenIds.add(key.id.toLowerCase())
    if (!HEX24.test(key.baseTpl)) errors.push({ field: `${prefix}.baseTpl`, message: 'Base template must be 24 hex characters.' })
    if (!key.name.trim()) errors.push({ field: `${prefix}.name`, message: 'Name is required.' })
    if (!key.shortName.trim()) errors.push({ field: `${prefix}.shortName`, message: 'Short name is required.' })
    if (!key.description.trim()) errors.push({ field: `${prefix}.description`, message: 'Description is required.' })
    if (key.weight < 0) errors.push({ field: `${prefix}.weight`, message: 'Weight cannot be negative.' })
    if (key.uses < 1) errors.push({ field: `${prefix}.uses`, message: 'Uses must be >= 1.' })
  })

  pack.containers.forEach((container, i) => {
    const prefix = `containers[${i}]`
    if (!HEX24.test(container.id)) errors.push({ field: `${prefix}.id`, message: 'Container ID must be 24 hex characters.' })
    if (seenIds.has(container.id.toLowerCase())) errors.push({ field: `${prefix}.id`, message: 'Duplicate ID.' })
    else seenIds.add(container.id.toLowerCase())
    if (!HEX24.test(container.baseTpl)) errors.push({ field: `${prefix}.baseTpl`, message: 'Base template must be 24 hex characters.' })
    if (!container.name.trim()) errors.push({ field: `${prefix}.name`, message: 'Name is required.' })
    if (!container.shortName.trim()) errors.push({ field: `${prefix}.shortName`, message: 'Short name is required.' })
    if (!container.description.trim()) errors.push({ field: `${prefix}.description`, message: 'Description is required.' })
    if (container.weight < 0) errors.push({ field: `${prefix}.weight`, message: 'Weight cannot be negative.' })
    if (container.safeContainerMode !== 'all' && container.safeContainerIds.length === 0)
      errors.push({ field: `${prefix}.safeContainerIds`, message: 'At least one safe container ID is required when using Include or Exclude mode.' })
    container.safeContainerIds.forEach((id, j) => {
      if (!HEX24.test(id)) errors.push({ field: `${prefix}.safeContainerIds[${j}]`, message: 'Safe container ID must be 24 hex characters.' })
    })
  })

  pack.stims.forEach((stim, i) => {
    const prefix = `stims[${i}]`
    if (!HEX24.test(stim.id)) errors.push({ field: `${prefix}.id`, message: 'Stim ID must be 24 hex characters.' })
    if (seenIds.has(stim.id.toLowerCase())) errors.push({ field: `${prefix}.id`, message: 'Duplicate ID.' })
    else seenIds.add(stim.id.toLowerCase())
    if (!HEX24.test(stim.baseTpl)) errors.push({ field: `${prefix}.baseTpl`, message: 'Base template must be 24 hex characters.' })
    if (!stim.name.trim()) errors.push({ field: `${prefix}.name`, message: 'Name is required.' })
    if (!stim.shortName.trim()) errors.push({ field: `${prefix}.shortName`, message: 'Short name is required.' })
    if (!stim.description.trim()) errors.push({ field: `${prefix}.description`, message: 'Description is required.' })
    if (stim.weight < 0) errors.push({ field: `${prefix}.weight`, message: 'Weight cannot be negative.' })
    if (stim.medUseTime < 0) errors.push({ field: `${prefix}.medUseTime`, message: 'Use time cannot be negative.' })
    if (stim.stackMaxSize < 1) errors.push({ field: `${prefix}.stackMaxSize`, message: 'Stack max size must be at least 1.' })
    if (stim.width < 1) errors.push({ field: `${prefix}.width`, message: 'Width must be at least 1.' })
    if (stim.height < 1) errors.push({ field: `${prefix}.height`, message: 'Height must be at least 1.' })
  })

  pack.medkits.forEach((medkit, i) => {
    const prefix = `medkits[${i}]`
    if (!HEX24.test(medkit.id)) errors.push({ field: `${prefix}.id`, message: 'MedKit ID must be 24 hex characters.' })
    if (seenIds.has(medkit.id.toLowerCase())) errors.push({ field: `${prefix}.id`, message: 'Duplicate ID.' })
    else seenIds.add(medkit.id.toLowerCase())
    if (!HEX24.test(medkit.baseTpl)) errors.push({ field: `${prefix}.baseTpl`, message: 'Base template must be 24 hex characters.' })
    if (!medkit.name.trim()) errors.push({ field: `${prefix}.name`, message: 'Name is required.' })
    if (!medkit.shortName.trim()) errors.push({ field: `${prefix}.shortName`, message: 'Short name is required.' })
    if (!medkit.description.trim()) errors.push({ field: `${prefix}.description`, message: 'Description is required.' })
    if (medkit.weight < 0) errors.push({ field: `${prefix}.weight`, message: 'Weight cannot be negative.' })
    if (medkit.medUseTime < 0) errors.push({ field: `${prefix}.medUseTime`, message: 'Use time cannot be negative.' })
    if (medkit.stackMaxSize < 1) errors.push({ field: `${prefix}.stackMaxSize`, message: 'Stack max size must be at least 1.' })
    if (medkit.width < 1) errors.push({ field: `${prefix}.width`, message: 'Width must be at least 1.' })
    if (medkit.height < 1) errors.push({ field: `${prefix}.height`, message: 'Height must be at least 1.' })
  })

  pack.traders.forEach((trader, ti) => {
    const tPrefix = `traders[${ti}]`
    if (!HEX24.test(trader.traderId)) errors.push({ field: `${tPrefix}.traderId`, message: 'Trader ID must be 24 hex characters.' })
    trader.entries.forEach((entry, ei) => {
      const ePrefix = `${tPrefix}.entries[${ei}]`
      if (!HEX24.test(entry.itemId)) errors.push({ field: `${ePrefix}.itemId`, message: 'Item ID must be 24 hex characters.' })
      if (entry.priceRoubles < 0) errors.push({ field: `${ePrefix}.priceRoubles`, message: 'Price cannot be negative.' })
      if (entry.loyaltyLevel < 1 || entry.loyaltyLevel > 4) errors.push({ field: `${ePrefix}.loyaltyLevel`, message: 'Loyalty level must be between 1 and 4.' })
      if (entry.stockCount < 0) errors.push({ field: `${ePrefix}.stockCount`, message: 'Stock count cannot be negative.' })
      if (entry.buyRestrictionMax < 0) errors.push({ field: `${ePrefix}.buyRestrictionMax`, message: 'Buy restriction cannot be negative.' })
      if (!itemIds.has(entry.itemId)) errors.push({ field: `${ePrefix}.itemId`, message: 'Trader entry references an item that does not exist in this pack.' })
    })
  })

  return errors
}

function cleanProperties(props: Record<string, any>, allowed: string[], defaults?: Record<string, any>): Record<string, any> {
  const next: Record<string, any> = {}
  for (const key of allowed) {
    const value = props[key] ?? defaults?.[key]
    if (value !== undefined) {
      if (key === 'Prefab' || key === 'UsePrefab') {
        const path = value?.path
        if (path) {
          next[key] = { path, rcid: value?.rcid ?? '' }
        }
      } else if (key === 'Grids' && Array.isArray(value)) {
        next[key] = value.map(grid => ({
          ...grid,
          _props: {
            ...grid._props,
            filters: grid._props?.filters?.map((f: any) => ({
              ...f,
              Filter: f.Filter?.filter(Boolean) ?? [],
              ExcludedFilter: f.ExcludedFilter?.filter(Boolean) ?? [],
            })) ?? grid._props?.filters,
          },
        }))
      } else {
        next[key] = JSON.parse(JSON.stringify(value))
      }
    }
  }
  return next
}

function buildExportJson(pack: ItemPackDefinition): string {
  const cleaned: ItemPackDefinition = {
    ...pack,
    bundles: undefined,
    questItems: pack.questItems.map(q => ({
      ...q,
      questIds: q.questIds.filter(Boolean),
    })),
    keys: pack.keys.map(k => ({
      ...k,
      doorIds: k.doorIds.filter(Boolean),
      properties: cleanProperties(k.properties || {}, ['Prefab', 'UsePrefab']),
    })),
    containers: pack.containers.map(c => ({
      ...c,
      properties: cleanProperties(c.properties || {}, ['Prefab', 'UsePrefab', 'Grids', 'Width', 'Height', 'StackMaxSize', 'ItemSound', 'CanPutIntoDuringTheRaid', 'HideEntrails', 'ExaminedByDefault']),
    })),
    stims: pack.stims.map(s => ({
      ...s,
      properties: cleanProperties(s.properties || {}, ['Prefab', 'UsePrefab', 'StimulatorBuffs'], { StimulatorBuffs: '' }),
    })),
  }
  return JSON.stringify(cleaned, null, 2)
}

function downloadJson(pack: ItemPackDefinition) {
  const blob = new Blob([buildExportJson(pack)], { type: 'application/json' })
  saveAs(blob, `${pack.name.replace(/\s+/g, '_').toLowerCase() || 'pack'}.json`)
}

async function exportModZip(pack: ItemPackDefinition) {
  const zip = new JSZip()
  const packName = pack.name.toLowerCase().replace(/\s+/g, '-')
  zip.file(`SPT/user/mods/ItemGen/items/${packName}.json`, buildExportJson(pack))

  if (pack.bundles && pack.bundles.length > 0) {
    pack.bundles.forEach(bundle => {
      zip.file(`BepInEx/plugins/Serenity-ItemGen/bundles/${bundle.name}`, bundle.data)
    })
  }

  const blob = await zip.generateAsync({ type: 'blob' })
  saveAs(blob, `${packName}.zip`)
}

function Sidebar({ open, onClose }: { open: boolean; onClose: () => void }) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        onClose()
      }
    }
    if (open) {
      document.addEventListener('mousedown', handleClick)
      return () => document.removeEventListener('mousedown', handleClick)
    }
  }, [open, onClose])

  const links = [
    { name: 'AmmoGen Tool', url: 'https://ammogen-tool.netlify.app', icon: <Target size={18} />, active: false },
    { name: 'TraderGen Tool', url: 'https://tradergen-tool.netlify.app', icon: <Store size={18} />, active: false },
    { name: 'ItemGen Tool', url: '#', icon: <Package size={18} />, active: true },
    { name: 'Map Editor Lite', url: 'https://mapeditorlite-tool.netlify.app', icon: <Map size={18} />, active: false },
    { name: 'Serenity Workshop', url: 'https://serenity-workshop.netlify.app', icon: <Wrench size={18} />, active: false },
  ]

  return (
    <>
      {open && (
        <div className="fixed inset-0 bg-black/50 z-40 transition-opacity" onClick={onClose} />
      )}
      <div
        ref={ref}
        className={`fixed top-0 left-0 h-full w-64 bg-tarkov-surface border-r border-tarkov-border z-50 transform transition-transform duration-200 ${
          open ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <div className="flex items-center justify-between px-4 py-4 border-b border-tarkov-border">
          <div className="flex items-center gap-2 text-tarkov-accent">
            <Package size={22} />
            <span className="font-bold">Serenity Mods</span>
          </div>
          <button
            onClick={onClose}
            className="p-1 rounded hover:bg-tarkov-border/50 text-tarkov-text-dim hover:text-tarkov-text transition-colors"
            aria-label="Close menu"
          >
            <X size={20} />
          </button>
        </div>

        <nav className="p-2 space-y-1">
          {links.map((link) => (
            <a
              key={link.name}
              href={link.url}
              target={link.url.startsWith('http') ? '_blank' : undefined}
              rel={link.url.startsWith('http') ? 'noopener noreferrer' : undefined}
              onClick={link.url === '#' ? onClose : undefined}
              className={`flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm transition-colors ${
                link.active
                  ? 'bg-tarkov-accent/20 text-tarkov-accent border border-tarkov-accent/50'
                  : 'text-tarkov-text hover:bg-tarkov-border/50 hover:text-tarkov-text'
              }`}
            >
              {link.icon}
              <span className="flex-1">{link.name}</span>
              <ExternalLink size={14} className="text-tarkov-text-dim" />
            </a>
          ))}
        </nav>
      </div>
    </>
  )
}

export default function App() {
  const [pack, setPack] = useState<ItemPackDefinition>(createDefaultPack())
  const [mode, setMode] = useState<Mode>('items')
  const [tab, setTab] = useState<Tab>('quest')
  const [selectedQuestIndex, setSelectedQuestIndex] = useState(0)
  const [selectedKeyIndex, setSelectedKeyIndex] = useState(0)
  const [selectedContainerIndex, setSelectedContainerIndex] = useState(0)
  const [selectedStimIndex, setSelectedStimIndex] = useState(0)
  const [selectedMedkitIndex, setSelectedMedkitIndex] = useState(0)
  const [selectedTraderIndex, setSelectedTraderIndex] = useState(0)
  const [rightPanel, setRightPanel] = useState<RightPanel>('editor')
  const [copyFeedback, setCopyFeedback] = useState<string | null>(null)
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const bundleInputRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLDivElement>(null)

  const activeItems = useMemo(() => {
    if (tab === 'quest') return pack.questItems
    if (tab === 'key') return pack.keys
    if (tab === 'stim') return pack.stims
    if (tab === 'medkit') return pack.medkits
    return pack.containers
  }, [tab, pack])
  const selectedIndex = tab === 'quest' ? selectedQuestIndex : tab === 'key' ? selectedKeyIndex : tab === 'stim' ? selectedStimIndex : tab === 'medkit' ? selectedMedkitIndex : selectedContainerIndex
  const selectedItem = activeItems[selectedIndex]
  const validationErrors = useMemo(() => validatePack(pack), [pack])

  const updatePack = useCallback((next: ItemPackDefinition) => {
    setPack(next)
  }, [])

  const updateItem = useCallback((index: number, updates: Partial<QuestItemDefinition> | Partial<KeyDefinition> | Partial<ContainerDefinition> | Partial<StimDefinition> | Partial<MedKitDefinition>) => {
    const next = { ...pack }
    let oldId: string | null = null
    let newId: string | null = null
    if (tab === 'quest') {
      oldId = next.questItems[index]?.id ?? null
      next.questItems = next.questItems.map((item, i) => (i === index ? { ...item, ...updates } as QuestItemDefinition : item))
      newId = next.questItems[index]?.id ?? null
    } else if (tab === 'key') {
      oldId = next.keys[index]?.id ?? null
      next.keys = next.keys.map((item, i) => (i === index ? { ...item, ...updates } as KeyDefinition : item))
      newId = next.keys[index]?.id ?? null
    } else if (tab === 'stim') {
      oldId = next.stims[index]?.id ?? null
      next.stims = next.stims.map((item, i) => (i === index ? { ...item, ...updates } as StimDefinition : item))
      newId = next.stims[index]?.id ?? null
    } else if (tab === 'medkit') {
      oldId = next.medkits[index]?.id ?? null
      next.medkits = next.medkits.map((item, i) => (i === index ? { ...item, ...updates } as MedKitDefinition : item))
      newId = next.medkits[index]?.id ?? null
    } else {
      oldId = next.containers[index]?.id ?? null
      next.containers = next.containers.map((item, i) => (i === index ? { ...item, ...updates } as ContainerDefinition : item))
      newId = next.containers[index]?.id ?? null
    }
    if (oldId && newId && oldId !== newId) {
      next.traders = next.traders.map(trader => ({
        ...trader,
        entries: trader.entries.map(entry => (entry.itemId === oldId ? { ...entry, itemId: newId } : entry)),
      }))
    }
    updatePack(next)
  }, [pack, tab, updatePack])

  const addItem = useCallback(() => {
    const next = { ...pack }
    if (tab === 'quest') {
      next.questItems = [...next.questItems, createDefaultQuestItem()]
      setSelectedQuestIndex(next.questItems.length - 1)
    } else if (tab === 'key') {
      next.keys = [...next.keys, createDefaultKey()]
      setSelectedKeyIndex(next.keys.length - 1)
    } else if (tab === 'stim') {
      next.stims = [...next.stims, createDefaultStim()]
      setSelectedStimIndex(next.stims.length - 1)
    } else if (tab === 'medkit') {
      next.medkits = [...next.medkits, createDefaultMedkit()]
      setSelectedMedkitIndex(next.medkits.length - 1)
    } else {
      next.containers = [...next.containers, createDefaultContainer()]
      setSelectedContainerIndex(next.containers.length - 1)
    }
    updatePack(next)
  }, [pack, tab, updatePack])

  const removeItem = useCallback((index: number) => {
    const next = { ...pack }
    if (tab === 'quest') {
      next.questItems = next.questItems.filter((_, i) => i !== index)
      setSelectedQuestIndex(Math.max(0, Math.min(selectedQuestIndex, next.questItems.length - 1)))
    } else if (tab === 'key') {
      next.keys = next.keys.filter((_, i) => i !== index)
      setSelectedKeyIndex(Math.max(0, Math.min(selectedKeyIndex, next.keys.length - 1)))
    } else if (tab === 'stim') {
      next.stims = next.stims.filter((_, i) => i !== index)
      setSelectedStimIndex(Math.max(0, Math.min(selectedStimIndex, next.stims.length - 1)))
    } else if (tab === 'medkit') {
      next.medkits = next.medkits.filter((_, i) => i !== index)
      setSelectedMedkitIndex(Math.max(0, Math.min(selectedMedkitIndex, next.medkits.length - 1)))
    } else {
      next.containers = next.containers.filter((_, i) => i !== index)
      setSelectedContainerIndex(Math.max(0, Math.min(selectedContainerIndex, next.containers.length - 1)))
    }
    updatePack(next)
  }, [pack, selectedContainerIndex, selectedKeyIndex, selectedMedkitIndex, selectedQuestIndex, selectedStimIndex, updatePack])

  const moveItem = useCallback((index: number, dir: -1 | 1) => {
    const next = { ...pack }
    const newIndex = index + dir
    if (tab === 'quest') {
      const list = [...next.questItems]
      if (newIndex < 0 || newIndex >= list.length) return
      const [moved] = list.splice(index, 1)
      list.splice(newIndex, 0, moved)
      next.questItems = list
      setSelectedQuestIndex(newIndex)
    } else if (tab === 'key') {
      const list = [...next.keys]
      if (newIndex < 0 || newIndex >= list.length) return
      const [moved] = list.splice(index, 1)
      list.splice(newIndex, 0, moved)
      next.keys = list
      setSelectedKeyIndex(newIndex)
    } else if (tab === 'stim') {
      const list = [...next.stims]
      if (newIndex < 0 || newIndex >= list.length) return
      const [moved] = list.splice(index, 1)
      list.splice(newIndex, 0, moved)
      next.stims = list
      setSelectedStimIndex(newIndex)
    } else if (tab === 'medkit') {
      const list = [...next.medkits]
      if (newIndex < 0 || newIndex >= list.length) return
      const [moved] = list.splice(index, 1)
      list.splice(newIndex, 0, moved)
      next.medkits = list
      setSelectedMedkitIndex(newIndex)
    } else {
      const list = [...next.containers]
      if (newIndex < 0 || newIndex >= list.length) return
      const [moved] = list.splice(index, 1)
      list.splice(newIndex, 0, moved)
      next.containers = list
      setSelectedContainerIndex(newIndex)
    }
    updatePack(next)
  }, [pack, tab, updatePack])

  const importPack = useCallback(async (file: File) => {
    try {
      let raw = ''
      let importedBundles: { name: string; data: ArrayBuffer }[] = []

      if (file.name.toLowerCase().endsWith('.zip')) {
        const zip = await JSZip.loadAsync(file)
        const jsonFiles = Object.values(zip.files).filter(
          f => f.name.toLowerCase().endsWith('.json') && !f.dir
        )
        const packFile = jsonFiles.find(f => /ItemGen[\\/]items[\\/].+\.json$/i.test(f.name)) || jsonFiles[0]
        if (!packFile) {
          alert('No JSON file found in the selected ZIP.')
          return
        }
        raw = await packFile.async('text')

        const bundlePrefix = 'BepInEx/plugins/Serenity-ItemGen/bundles/'
        importedBundles = await Promise.all(
          Object.values(zip.files)
            .filter(f => !f.dir && f.name.toLowerCase().startsWith(bundlePrefix.toLowerCase()))
            .map(async f => ({
              name: f.name.slice(bundlePrefix.length),
              data: await f.async('arraybuffer'),
            }))
        )
      } else {
        raw = await file.text()
      }

      const imported = JSON.parse(raw) as ItemPackDefinition
      const normalized: ItemPackDefinition = {
        enabled: imported.enabled ?? true,
        name: imported.name || 'Imported Pack',
        questItems: imported.questItems || [],
        keys: imported.keys || [],
        containers: (imported.containers || []).map(c => ({
          ...c,
          safeContainerMode: c.safeContainerMode ?? 'all',
          safeContainerIds: c.safeContainerIds || [],
        })),
        stims: (imported.stims || []).map(s => ({
          ...s,
          maxBodyPartsToHeal: s.maxBodyPartsToHeal ?? 0,
        })),
        medkits: (imported.medkits || []).map(m => ({
          ...m,
          maxBodyPartsToHeal: m.maxBodyPartsToHeal ?? 0,
        })),
        traders: imported.traders?.length
          ? imported.traders
          : VANILLA_TRADERS.map(t => ({ traderId: t.id, enabled: true, entries: [] })),
        bundles: importedBundles.length > 0 ? importedBundles : imported.bundles || [],
      }
      updatePack(normalized)
      setTab(normalized.questItems.length > 0 ? 'quest' : normalized.keys.length > 0 ? 'key' : normalized.stims.length > 0 ? 'stim' : normalized.medkits.length > 0 ? 'medkit' : 'container')
      setSelectedQuestIndex(0)
      setSelectedKeyIndex(0)
      setSelectedContainerIndex(0)
      setSelectedMedkitIndex(0)
    } catch (e) {
      alert('Invalid pack file: ' + (e as Error).message)
    }
  }, [updatePack])

  const addBundles = useCallback(async (files: FileList | null) => {
    if (!files || files.length === 0) return
    const newBundles = await Promise.all(
      Array.from(files).map(async file => ({
        name: file.name,
        data: await file.arrayBuffer(),
      }))
    )
    updatePack({ ...pack, bundles: [...(pack.bundles || []), ...newBundles] })
  }, [pack, updatePack])

  const removeBundle = useCallback((index: number) => {
    const next = [...(pack.bundles || [])]
    next.splice(index, 1)
    updatePack({ ...pack, bundles: next })
  }, [pack, updatePack])

  const exportJson = useCallback(() => downloadJson(pack), [pack])
  const exportPackage = useCallback(() => exportModZip(pack), [pack])
  const copyJson = useCallback(() => {
    navigator.clipboard.writeText(buildExportJson(pack)).then(() => {
      setCopyFeedback('Copied!')
      setTimeout(() => setCopyFeedback(null), 1500)
    })
  }, [pack])

  const errorsByField = useCallback((field: string) => validationErrors.filter(e => e.field === field), [validationErrors])

  const selectTemplate = (templateId: string) => {
    if (!selectedItem) return
    const apiItem = apiItemNames[templateId as keyof typeof apiItemNames]
    const baseUpdates: Partial<ItemDefinition> = {
      baseTpl: templateId,
      ...(apiItem ? { name: apiItem.Name, shortName: apiItem.ShortName } : {}),
    }
    if (tab === 'quest') {
      const template = QUEST_TEMPLATES.find(t => t.id === templateId)
      updateItem(selectedIndex, {
        ...baseUpdates,
        ...(template ? {
          weight: template.weight,
          backgroundColor: template.backgroundColor,
          stackMaxSize: template.stackMaxSize,
        } : {}),
      })
    } else if (tab === 'key') {
      const template = KEY_TEMPLATES.find(t => t.id === templateId)
      updateItem(selectedIndex, {
        ...baseUpdates,
        ...(template ? {
          weight: template.weight,
          backgroundColor: template.backgroundColor,
          uses: template.maximumNumberOfUsage,
          properties: template.properties,
        } : {}),
      })
    } else if (tab === 'stim') {
      const template = STIM_TEMPLATES.find(t => t.id === templateId)
      const props = template?.properties ?? {}
      const cleanProps = { ...props, Prefab: { ...props.Prefab, path: '' }, UsePrefab: { ...props.UsePrefab, path: '' }, StimulatorBuffs: '', effects_health: {}, effects_damage: {}, BodyPartPriority: [], foodEffectType: '' }
      updateItem(selectedIndex, {
        ...baseUpdates,
        weight: template?.weight ?? (typeof props.Weight === 'number' ? props.Weight : 0.05),
        backgroundColor: template?.backgroundColor ?? props.BackgroundColor,
        rarityPvE: props.RarityPvE ?? 'Superrare',
        canSellOnRagfair: props.CanSellOnRagfair ?? true,
        itemSound: template?.itemSound ?? props.ItemSound ?? 'med_stimulator',
        stimulatorBuffs: '',
        medEffectType: template?.medEffectType ?? props.medEffectType ?? 'duringUse',
        medUseTime: template?.medUseTime ?? (typeof props.medUseTime === 'number' ? props.medUseTime : 2),
        maxHpResource: template?.maxHpResource ?? (typeof props.MaxHpResource === 'number' ? props.MaxHpResource : 0),
        hpResourceRate: template?.hpResourceRate ?? (typeof props.hpResourceRate === 'number' ? props.hpResourceRate : 0),
        maxBodyPartsToHeal: template?.maxBodyPartsToHeal ?? (typeof props.MaxBodyPartsToHeal === 'number' ? props.MaxBodyPartsToHeal : 0),
        stackMaxSize: template?.stackMaxSize ?? (typeof props.StackMaxSize === 'number' ? props.StackMaxSize : 1),
        width: template?.width ?? (typeof props.Width === 'number' ? props.Width : 1),
        height: template?.height ?? (typeof props.Height === 'number' ? props.Height : 1),
        customBuffs: template?.customBuffs ?? [],
        effectsHealth: template?.effectsHealth ?? {},
        effectsDamage: template?.effectsDamage ?? {},
        properties: cleanProps,
      } as Partial<StimDefinition>)
    } else if (tab === 'medkit') {
      const template = MEDKIT_TEMPLATES.find(t => t.id === templateId)
      const props = template?.properties ?? {}
      const cleanProps = { ...props, Prefab: { ...props.Prefab, path: '' }, UsePrefab: { ...props.UsePrefab, path: '' }, StimulatorBuffs: '', effects_health: {}, effects_damage: {}, BodyPartPriority: [], foodEffectType: '' }
      updateItem(selectedIndex, {
        ...baseUpdates,
        weight: template?.weight ?? (typeof props.Weight === 'number' ? props.Weight : 0.5),
        backgroundColor: template?.backgroundColor ?? props.BackgroundColor,
        rarityPvE: props.RarityPvE ?? 'Rare',
        canSellOnRagfair: props.CanSellOnRagfair ?? true,
        itemSound: template?.itemSound ?? props.ItemSound ?? 'med_medkit',
        stimulatorBuffs: '',
        medEffectType: template?.medEffectType ?? props.medEffectType ?? 'duringUse',
        medUseTime: template?.medUseTime ?? (typeof props.medUseTime === 'number' ? props.medUseTime : 3),
        maxHpResource: template?.maxHpResource ?? (typeof props.MaxHpResource === 'number' ? props.MaxHpResource : 400),
        hpResourceRate: template?.hpResourceRate ?? (typeof props.hpResourceRate === 'number' ? props.hpResourceRate : 85),
        maxBodyPartsToHeal: 0,
        stackMaxSize: template?.stackMaxSize ?? (typeof props.StackMaxSize === 'number' ? props.StackMaxSize : 1),
        width: template?.width ?? (typeof props.Width === 'number' ? props.Width : 1),
        height: template?.height ?? (typeof props.Height === 'number' ? props.Height : 2),
        customBuffs: [],
        effectsHealth: template?.effectsHealth ?? {},
        effectsDamage: template?.effectsDamage ?? {},
        properties: cleanProps,
      } as Partial<MedKitDefinition>)
    } else {
      const template = CONTAINER_TEMPLATES.find(t => t.id === templateId)
      const props = template?.properties ?? {}
      updateItem(selectedIndex, {
        ...baseUpdates,
        parent: template?.parent ?? '5795f317245977243854e041',
        handbookParentId: template?.handbookParentId ?? '5b5f6fa186f77409407a7eb7',
        weight: typeof props.Weight === 'number' ? props.Weight : 0,
        backgroundColor: props.BackgroundColor,
        rarityPvE: props.RarityPvE ?? 'Not_exist',
        canSellOnRagfair: props.CanSellOnRagfair ?? false,
        properties: props,
      } as Partial<ContainerDefinition>)
    }
  }

  return (
    <div className="min-h-screen flex flex-col bg-tarkov-bg text-tarkov-text">
      <Sidebar open={sidebarOpen} onClose={() => setSidebarOpen(false)} />

      <header className="bg-tarkov-surface border-b border-tarkov-border p-4 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <button
            onClick={() => setSidebarOpen(true)}
            className="p-2 -ml-2 rounded-lg hover:bg-tarkov-border/50 text-tarkov-text-dim hover:text-tarkov-text transition-colors"
            aria-label="Open menu"
          >
            <Menu size={24} />
          </button>
          <div className="w-10 h-10 rounded bg-tarkov-accent flex items-center justify-center text-tarkov-bg font-bold text-lg">
            <Package size={22} />
          </div>
          <div>
            <h1 className="text-xl font-bold text-tarkov-accent">ItemGen Tool</h1>
            <p className="text-xs text-tarkov-text-dim">SPTarkov 4.0.13 Custom Item Pack Editor</p>
          </div>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          <button className="btn-secondary text-sm flex items-center gap-1.5" onClick={() => fileInputRef.current?.click()}>
            <Upload size={14} /> Import
          </button>
          <button className="btn-secondary text-sm flex items-center gap-1.5" onClick={exportJson}>
            <FileJson size={14} /> Export JSON
          </button>
          <button className="btn-secondary text-sm flex items-center gap-1.5" onClick={copyJson}>
            <Copy size={14} /> {copyFeedback || 'Copy JSON'}
          </button>
          <button className="btn-primary text-sm flex items-center gap-1.5" onClick={exportPackage}>
            <Download size={14} /> Export
          </button>
          <input
            type="file"
            accept=".json,.zip"
            className="hidden"
            ref={fileInputRef}
            onChange={e => {
              const file = e.target.files?.[0]
              if (file) importPack(file)
              e.target.value = ''
            }}
          />
        </div>
      </header>

      <div className="bg-tarkov-surface border-b border-tarkov-border px-4 py-2 flex items-center gap-2">
        <button
          className={`px-4 py-1.5 rounded-lg text-sm font-medium transition-colors ${mode === 'items' ? 'bg-tarkov-accent text-tarkov-bg' : 'text-tarkov-text hover:bg-tarkov-border/50'}`}
          onClick={() => setMode('items')}
        >
          Items
        </button>
        <button
          className={`px-4 py-1.5 rounded-lg text-sm font-medium transition-colors ${mode === 'traders' ? 'bg-tarkov-accent text-tarkov-bg' : 'text-tarkov-text hover:bg-tarkov-border/50'}`}
          onClick={() => setMode('traders')}
        >
          Traders
        </button>
        <button
          className={`px-4 py-1.5 rounded-lg text-sm font-medium transition-colors ${mode === 'bundles' ? 'bg-tarkov-accent text-tarkov-bg' : 'text-tarkov-text hover:bg-tarkov-border/50'}`}
          onClick={() => setMode('bundles')}
        >
          Bundles {pack.bundles && pack.bundles.length > 0 && `(${pack.bundles.length})`}
        </button>
      </div>

      <div className="flex-1 flex overflow-hidden">
        <aside className="w-72 bg-tarkov-surface border-r border-tarkov-border flex flex-col">
          <div className="p-4 border-b border-tarkov-border space-y-3">
            <Field label="Pack Name" tooltip="Name of the generated item pack. Used as the ZIP folder name and file name.">
              <input className="input-field" value={pack.name} onChange={e => updatePack({ ...pack, name: e.target.value })} />
            </Field>
            <Toggle checked={pack.enabled} onChange={v => updatePack({ ...pack, enabled: v })} label="Pack Enabled" />
          </div>

          {mode === 'items' && (
            <>
              <div className="p-3 border-b border-tarkov-border space-y-2">
                <Field label="Category" tooltip="Select the item category to edit. New categories can be added easily as the mod grows.">
                  <select className="input-field" value={tab} onChange={e => setTab(e.target.value as Tab)}>
                    <option value="quest">Quest Items ({pack.questItems.length})</option>
                    <option value="key">Keys ({pack.keys.length})</option>
                    <option value="container">Containers ({pack.containers.length})</option>
                    <option value="stim">Stims ({pack.stims.length})</option>
                    <option value="medkit">MedKits ({pack.medkits.length})</option>
                  </select>
                </Field>
                <button className="btn-primary w-full text-sm flex items-center justify-center gap-1.5" onClick={addItem}>
                  <Plus size={14} /> Add {tab === 'quest' ? 'Quest Item' : tab === 'key' ? 'Key' : tab === 'stim' ? 'Stim' : tab === 'medkit' ? 'MedKit' : 'Container'}
                </button>
              </div>

              <div ref={listRef} className="flex-1 overflow-y-auto p-2 space-y-2">
                {activeItems.map((item, i) => {
                  const listPrefix = tab === 'quest' ? 'questItems' : tab === 'key' ? 'keys' : tab === 'stim' ? 'stims' : tab === 'medkit' ? 'medkits' : 'containers'
                  const hasErrors = validationErrors.some(e => e.field.startsWith(`${listPrefix}[${i}]`))
                  return (
                    <div
                      key={item.id + i}
                      className={`p-3 rounded border cursor-pointer text-sm transition-colors ${
                        i === selectedIndex
                          ? 'bg-tarkov-accent/10 border-tarkov-accent'
                          : 'bg-tarkov-bg border-tarkov-border hover:border-tarkov-text-dim'
                      } ${!item.enabled ? 'opacity-50' : ''}`}
                      onClick={() => {
                        if (tab === 'quest') setSelectedQuestIndex(i)
                        else if (tab === 'key') setSelectedKeyIndex(i)
                        else if (tab === 'stim') setSelectedStimIndex(i)
                        else if (tab === 'medkit') setSelectedMedkitIndex(i)
                        else setSelectedContainerIndex(i)
                      }}
                    >
                      <div className="flex items-center justify-between">
                        <span className="font-semibold truncate">{item.name}</span>
                        {hasErrors && <AlertCircle size={14} className="text-tarkov-error shrink-0" />}
                      </div>
                      <div className="text-xs text-tarkov-text-dim font-mono truncate mt-1">{item.id}</div>
                      <div className="text-xs text-tarkov-text-dim truncate">{item.baseTpl ? getTemplateName(item.baseTpl, tab) : 'No base template'}</div>
                    </div>
                  )
                })}
                {activeItems.length === 0 && (
                  <div className="text-sm text-tarkov-text-dim text-center py-4">No {tab === 'quest' ? 'quest items' : tab === 'key' ? 'keys' : tab === 'stim' ? 'stims' : tab === 'medkit' ? 'medkits' : 'containers'} yet.</div>
                )}
              </div>

              <div className="p-3 border-t border-tarkov-border space-y-2">
                <button className="btn-primary w-full text-sm flex items-center justify-center gap-1.5" onClick={addItem}>
                  <Plus size={14} /> Add {tab === 'quest' ? 'Quest Item' : tab === 'key' ? 'Key' : tab === 'stim' ? 'Stim' : tab === 'medkit' ? 'MedKit' : 'Container'}
                </button>
              </div>
            </>
          )}

          {mode === 'traders' && (
            <div className="flex-1 overflow-y-auto p-2 space-y-2">
              <div className="text-xs font-semibold text-tarkov-text-dim uppercase tracking-wider px-2 mb-1">Traders</div>
              {pack.traders.map((trader, i) => {
                const name = VANILLA_TRADERS.find(t => t.id === trader.traderId)?.name || trader.traderId
                const activeCount = trader.entries.filter(e => e.enabled).length
                return (
                  <div
                    key={i}
                    className={`p-3 rounded border cursor-pointer text-sm transition-colors ${
                      i === selectedTraderIndex
                        ? 'bg-tarkov-accent/10 border-tarkov-accent'
                        : 'bg-tarkov-bg border-tarkov-border hover:border-tarkov-text-dim'
                    } ${!trader.enabled ? 'opacity-50' : ''}`}
                    onClick={() => setSelectedTraderIndex(i)}
                  >
                    <div className="flex items-center justify-between">
                      <span className="font-semibold truncate">{name}</span>
                      <span className="text-xs font-mono bg-tarkov-bg border border-tarkov-border px-1.5 py-0.5 rounded">{activeCount}</span>
                    </div>
                    <div className="text-xs text-tarkov-text-dim font-mono truncate mt-1">{trader.traderId}</div>
                  </div>
                )
              })}
            </div>
          )}

          <div className="p-3 border-t border-tarkov-border">
            <button className="btn-secondary w-full text-sm flex items-center justify-center gap-1.5" onClick={() => { if (listRef.current) listRef.current.scrollTop = 0 }}>
              <ArrowUp size={14} /> Back to Top
            </button>
          </div>
        </aside>

        <main className="flex-1 flex flex-col min-w-0">
          <div className="flex items-center justify-between p-3 border-b border-tarkov-border bg-tarkov-surface">
            <div className="flex items-center gap-2">
              <button className={`px-3 py-1.5 text-sm font-semibold rounded ${rightPanel === 'editor' ? 'bg-tarkov-accent text-tarkov-bg' : 'text-tarkov-text-dim hover:text-tarkov-text'}`} onClick={() => setRightPanel('editor')}>Editor</button>
              <button className={`px-3 py-1.5 text-sm font-semibold rounded ${rightPanel === 'json' ? 'bg-tarkov-accent text-tarkov-bg' : 'text-tarkov-text-dim hover:text-tarkov-text'}`} onClick={() => setRightPanel('json')}>JSON Preview</button>
            </div>
            <div className="text-sm">
              {validationErrors.length === 0 ? (
                <span className="text-tarkov-success flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-tarkov-success" /> Valid</span>
              ) : (
                <span className="text-tarkov-error flex items-center gap-1"><AlertCircle size={14} /> {validationErrors.length} error(s)</span>
              )}
            </div>
          </div>

          <div className="flex-1 overflow-y-auto p-4">
            {mode === 'items' ? (
              rightPanel === 'json' ? (
                <section className="card">
                  <div className="flex items-center justify-between mb-3">
                    <h2 className="text-lg font-semibold text-tarkov-accent flex items-center gap-2"><FileJson size={18} /> Generated Pack JSON</h2>
                    <button className="btn-secondary text-sm flex items-center gap-1.5" onClick={copyJson}>
                      <Copy size={14} /> {copyFeedback || 'Copy'}
                    </button>
                  </div>
                  <pre className="bg-tarkov-bg border border-tarkov-border rounded p-3 overflow-auto text-xs font-mono max-h-[75vh]">{buildExportJson(pack)}</pre>
                </section>
              ) : selectedItem ? (
                <div className="space-y-4 max-w-5xl">
                <section className="card">
                  <div className="flex items-center justify-between flex-wrap gap-3">
                    <div className="flex items-center gap-3">
                      <Toggle checked={selectedItem.enabled} onChange={v => updateItem(selectedIndex, { enabled: v })} />
                      <span className="font-semibold text-tarkov-accent text-lg">{selectedItem.name}</span>
                      <span className="text-xs text-tarkov-text-dim font-mono">{selectedItem.id}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <button className="btn-secondary text-sm px-2" onClick={() => moveItem(selectedIndex, -1)} disabled={selectedIndex === 0} title="Move up">↑</button>
                      <button className="btn-secondary text-sm px-2" onClick={() => moveItem(selectedIndex, 1)} disabled={selectedIndex === activeItems.length - 1} title="Move down">↓</button>
                      <button className="btn-danger text-sm flex items-center gap-1.5" onClick={() => removeItem(selectedIndex)}>
                        <Trash2 size={14} /> Remove
                      </button>
                    </div>
                  </div>
                </section>

                <Section title="Identity" icon={<Fingerprint size={18} />}>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <Field label="Item ID" tooltip="Unique 24-character hex ID for this custom item. Used by quests, traders, and other mods to reference it." error={errorsByField(`${tab === 'quest' ? 'questItems' : tab === 'key' ? 'keys' : tab === 'medkit' ? 'medkits' : 'containers'}[${selectedIndex}].id`).length > 0}>
                      <div className="flex gap-2">
                        <input className="input-field flex-1 font-mono text-sm" value={selectedItem.id} onChange={e => updateItem(selectedIndex, { id: e.target.value })} maxLength={24} />
                        <button className="btn-secondary text-xs px-2" onClick={() => updateItem(selectedIndex, { id: generateMongoId() })} title="Regenerate ID">
                          <RefreshCw size={14} />
                        </button>
                      </div>
                      <FieldErrors errors={errorsByField(`${tab === 'quest' ? 'questItems' : tab === 'key' ? 'keys' : tab === 'medkit' ? 'medkits' : 'containers'}[${selectedIndex}].id`)} />
                    </Field>

                    <Field label="Base Template" tooltip={`Vanilla item to clone. The new item inherits the parent category, handbook category, and model unless overridden. Search all SPT items below; keys, quest items, containers, stims and medkits are included.`} error={errorsByField(`${tab === 'quest' ? 'questItems' : tab === 'key' ? 'keys' : tab === 'medkit' ? 'medkits' : 'containers'}[${selectedIndex}].baseTpl`).length > 0}>
                      <SearchableSelect
                        value={selectedItem.baseTpl}
                        onChange={id => selectTemplate(id)}
                        options={ALL_ITEM_OPTIONS}
                        placeholder="Search any SPT item (keys, quest items, containers, etc.)..."
                      />
                      {selectedItem.baseTpl && (
                        <div className="mt-1 text-xs text-tarkov-text-dim font-mono truncate">
                          {selectedItem.baseTpl} {getParentDisplay(selectedItem.baseTpl, tab)}
                        </div>
                      )}
                      <FieldErrors errors={errorsByField(`${tab === 'quest' ? 'questItems' : tab === 'key' ? 'keys' : tab === 'medkit' ? 'medkits' : 'containers'}[${selectedIndex}].baseTpl`)} />
                    </Field>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                    <Field label="Name" tooltip="Full display name shown in tooltips and trader/quest text.">
                      <input className="input-field" value={selectedItem.name} onChange={e => updateItem(selectedIndex, { name: e.target.value })} placeholder="e.g. My Custom Watch" />
                    </Field>
                    <Field label="Short Name" tooltip="Short name shown in the inventory grid and HUD.">
                      <input className="input-field" value={selectedItem.shortName} onChange={e => updateItem(selectedIndex, { shortName: e.target.value })} placeholder="e.g. My Watch" />
                    </Field>
                  </div>

                  <Field label="Description" tooltip="Description shown when the player inspects the item." className="mt-4">
                    <textarea className="input-field min-h-[80px] resize-y" value={selectedItem.description} onChange={e => updateItem(selectedIndex, { description: e.target.value })} placeholder="Describe what this item is for..." />
                  </Field>
                </Section>

                <Section title="Properties" icon={<SlidersHorizontal size={18} />}>
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <Field label="Weight" tooltip="Item weight in kilograms. Affects player stamina and movement.">
                      <input className="input-field" type="number" step="0.01" value={selectedItem.weight} onChange={e => updateItem(selectedIndex, { weight: parseFloat(e.target.value) || 0 })} />
                    </Field>
                    <Field label="Background Color" tooltip="Inventory cell background color. Choose 'Default' to keep the base template's color, pick a preset, or choose a custom hex color.">
                      <BackgroundColorPicker
                        value={selectedItem.backgroundColor}
                        onChange={color => updateItem(selectedIndex, { backgroundColor: color === 'default' ? undefined : color })}
                      />
                    </Field>
                    <Field label="Rarity PvE" tooltip="PvE rarity value used by SPT. 'Not_exist' hides the item from most loot pools; 'Common' is the default for keys.">
                      <select className="input-field" value={selectedItem.rarityPvE} onChange={e => updateItem(selectedIndex, { rarityPvE: e.target.value })}>
                        {RARITY_PVE.map(r => <option key={r} value={r}>{r}</option>)}
                      </select>
                    </Field>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mt-4">
                    <Field label="Handbook Price (₽)" tooltip="Base price used by traders and the Flea Market.">
                      <input className="input-field" type="number" min={0} value={selectedItem.handbookPriceRoubles} onChange={e => updateItem(selectedIndex, { handbookPriceRoubles: parseInt(e.target.value) || 0 })} />
                    </Field>
                    <Field label="Flea Price (₽)" tooltip="Flea Market listing price. Set to 0 to use the handbook price.">
                      <input className="input-field" type="number" min={0} value={selectedItem.fleaPriceRoubles} onChange={e => updateItem(selectedIndex, { fleaPriceRoubles: parseInt(e.target.value) || 0 })} />
                    </Field>
                    <Field label="Can Sell on Flea" tooltip="Whether the item can be listed on the Flea Market.">
                      <Toggle checked={selectedItem.canSellOnRagfair} onChange={v => updateItem(selectedIndex, { canSellOnRagfair: v })} />
                    </Field>
                  </div>
                </Section>

                {'questIds' in selectedItem && (
                  <Section title="Quest Item Specifics" icon={<ScrollText size={18} />}>
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                      <Field label="Stack Max Size" tooltip="Maximum number of this item that can stack in one inventory cell.">
                        <input className="input-field" type="number" min={1} value={(selectedItem as QuestItemDefinition).stackMaxSize} onChange={e => updateItem(selectedIndex, { stackMaxSize: parseInt(e.target.value) || 1 })} />
                      </Field>
                    </div>
                    <Field label="Linked Quest IDs (optional)" tooltip="Comma-separated quest template IDs that use this item in a FindItem condition. Used by companion mods (e.g. TraderGen) to auto-generate quests." className="mt-4">
                      <input className="input-field" placeholder="e.g. 5936da9e86f7742d65037edf, ..." value={(selectedItem as QuestItemDefinition).questIds.join(', ')} onChange={e => updateItem(selectedIndex, { questIds: e.target.value.split(',').map(s => s.trim()) })} />
                    </Field>
                    <div className="mt-3 text-sm text-tarkov-text-dim bg-tarkov-bg border border-tarkov-border rounded p-3">
                      <span className="text-tarkov-accent font-semibold">Note:</span> The server sets <span className="font-mono">QuestItem = true</span> on this item so it behaves exactly like a vanilla EFT quest inventory item.
                    </div>
                  </Section>
                )}

                {'uses' in selectedItem && (
                  <Section title="Key Specifics" icon={<Key size={18} />}>
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                      <Field label="Uses" tooltip="Number of times the key can be used before it is consumed. Vanilla keys use 40 for mechanical keys and 10 for keycards.">
                        <input className="input-field" type="number" min={1} value={(selectedItem as KeyDefinition).uses} onChange={e => updateItem(selectedIndex, { uses: parseInt(e.target.value) || 1 })} />
                      </Field>
                      <Field label="Key Category" tooltip="Optional category label for organization. Not used by SPT directly.">
                        <input className="input-field" value={(selectedItem as KeyDefinition).keyCategory} onChange={e => updateItem(selectedIndex, { keyCategory: e.target.value })} />
                      </Field>
                    </div>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                      <Field label="Item Sound" tooltip="Sound identifier used when moving the key.">
                        <input className="input-field" value={(selectedItem as KeyDefinition).properties.ItemSound ?? ''} onChange={e => {
                          const key = selectedItem as KeyDefinition
                          updateItem(selectedIndex, { properties: { ...key.properties, ItemSound: e.target.value } })
                        }} />
                      </Field>
                      <Field label="Custom Model" tooltip="Custom bundle filename (e.g. my_key.bundle). The client plugin matches this to a file in BepInEx\plugins\Serenity-ItemGen\bundles. Leave empty to inherit from the base template.">
                        <input className="input-field" value={(selectedItem as KeyDefinition).properties.Prefab?.path ?? ''} onChange={e => {
                          const key = selectedItem as KeyDefinition
                          updateItem(selectedIndex, { properties: { ...key.properties, Prefab: { ...key.properties.Prefab, path: e.target.value } } })
                        }} placeholder="my_key.bundle" />
                      </Field>
                    </div>
                    <Field label="Door IDs (optional)" tooltip="Comma-separated vanilla door IDs this key opens. These are patched into the key's KeyIds on the server. Leave empty to inherit from the base template." className="mt-4">
                      <input className="input-field" placeholder="e.g. 123456789012345678901234, 567890123456789012345678" value={(selectedItem as KeyDefinition).doorIds.join(', ')} onChange={e => updateItem(selectedIndex, { doorIds: e.target.value.split(',').map(s => s.trim()) })} />
                    </Field>
                    <div className="mt-3 text-sm text-tarkov-text-dim bg-tarkov-bg border border-tarkov-border rounded p-3">
                      <span className="text-tarkov-accent font-semibold">Note:</span> Door IDs should be vanilla EFT door IDs. Custom doors added by mods such as Map Editor Lite can have the key ID set in the editor, and Map Editor Lite can also dump door IDs while in raid.
                    </div>
                  </Section>
                )}

                {'stimulatorBuffs' in selectedItem && (
                  <StimSpecifics
                    stim={selectedItem as StimDefinition}
                    onChange={updates => updateItem(selectedIndex, updates as Partial<StimDefinition>)}
                    mode={tab === 'medkit' ? 'medkit' : 'stim'}
                  />
                )}

                {'handbookParentId' in selectedItem && (
                  <ContainerSpecifics
                    container={selectedItem as ContainerDefinition}
                    onChange={updates => updateItem(selectedIndex, updates)}
                  />
                )}

                <Section title="Advanced" icon={<Settings size={18} />}>
                  <Field label="Custom Icon Path (optional)" tooltip="Override the default icon. Path is relative to the mod bundle. Leave empty to inherit from the base template.">
                    <input className="input-field" value={selectedItem.customIcon || ''} onChange={e => updateItem(selectedIndex, { customIcon: e.target.value || undefined })} placeholder="assets/content/items/..." />
                  </Field>
                </Section>
              </div>
            ) : (
              <div className="text-center text-tarkov-text-dim mt-20">Select or add an item to edit.</div>
            )
          ) : mode === 'traders' ? (
            <TraderEditor
              pack={pack}
              traderIndex={selectedTraderIndex}
              onChange={next => setPack({ ...pack, traders: next })}
            />
          ) : (
            <div className="space-y-4 max-w-5xl">
              <Section title="Custom Bundles" icon={<Upload size={18} />}>
                <input
                  type="file"
                  multiple
                  ref={bundleInputRef}
                  className="hidden"
                  onChange={e => addBundles(e.target.files)}
                />
                <div
                  className="border border-dashed border-tarkov-border rounded-lg p-6 text-center hover:border-tarkov-accent hover:bg-tarkov-accent/5 transition-colors cursor-pointer"
                  onClick={() => bundleInputRef.current?.click()}
                  onDragOver={e => { e.preventDefault(); e.dataTransfer.dropEffect = 'copy' }}
                  onDrop={e => { e.preventDefault(); addBundles(e.dataTransfer.files) }}
                >
                  <Upload size={24} className="mx-auto text-tarkov-text-dim mb-2" />
                  <div className="text-sm text-tarkov-text">Drop bundle files here or click to browse</div>
                  <div className="text-xs text-tarkov-text-dim mt-1">Files will be bundled into the exported ZIP under BepInEx/plugins/Serenity-ItemGen/bundles/</div>
                </div>
                {(pack.bundles || []).length > 0 && (
                  <div className="mt-4 space-y-2">
                    <h3 className="text-sm font-semibold text-tarkov-text">Added Bundles</h3>
                    <div className="space-y-1">
                      {(pack.bundles || []).map((bundle, i) => (
                        <div key={`${bundle.name}-${i}`} className="flex items-center justify-between px-3 py-2 bg-tarkov-bg border border-tarkov-border rounded text-sm">
                          <span className="truncate text-tarkov-text">{bundle.name}</span>
                          <button
                            className="text-tarkov-error hover:text-tarkov-error/80 shrink-0 ml-2"
                            onClick={() => removeBundle(i)}
                            aria-label="Remove bundle"
                          >
                            <Trash2 size={14} />
                          </button>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </Section>
            </div>
          )}
          </div>
        </main>
      </div>

      {validationErrors.length > 0 && (
        <div className="bg-tarkov-error/10 border-t border-tarkov-error p-3 text-sm max-h-40 overflow-y-auto">
          <div className="font-semibold text-tarkov-error mb-1 flex items-center gap-2"><AlertCircle size={14} /> Validation Errors</div>
          <ul className="list-disc list-inside space-y-0.5">
            {validationErrors.map((err, i) => (
              <li key={i}><span className="font-mono text-tarkov-text-dim">{err.field}</span>: {err.message}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}

function getTemplateName(id: string, tab: Tab): string {
  if (tab === 'quest') {
    const t = QUEST_TEMPLATES.find(x => x.id === id)
    return t ? t.displayName : ''
  }
  if (tab === 'key') {
    const t = KEY_TEMPLATES.find(x => x.id === id)
    return t ? t.displayName : ''
  }
  if (tab === 'stim') {
    const t = STIM_TEMPLATES.find(x => x.id === id)
    return t ? t.displayName : ''
  }
  if (tab === 'medkit') {
    const t = MEDKIT_TEMPLATES.find(x => x.id === id)
    return t ? t.displayName : ''
  }
  const t = CONTAINER_TEMPLATES.find(x => x.id === id)
  return t ? t.displayName : ''
}

function getTemplateParent(id: string, tab: Tab): string {
  if (tab === 'quest') {
    const t = QUEST_TEMPLATES.find(x => x.id === id)
    return t ? t.parent : ''
  }
  if (tab === 'key') {
    const t = KEY_TEMPLATES.find(x => x.id === id)
    return t ? t.parent : ''
  }
  if (tab === 'stim') {
    const t = STIM_TEMPLATES.find(x => x.id === id)
    return t ? t.parent : ''
  }
  if (tab === 'medkit') {
    const t = MEDKIT_TEMPLATES.find(x => x.id === id)
    return t ? t.parent : ''
  }
  const t = CONTAINER_TEMPLATES.find(x => x.id === id)
  return t ? t.parent : ''
}

function getParentDisplay(id: string, tab: Tab): string {
  const parentId = getTemplateParent(id, tab)
  if (!parentId) return ''
  return `(${getParentName(parentId)})`
}

function TraderEditor({ pack, traderIndex, onChange }: { pack: ItemPackDefinition; traderIndex: number; onChange: (traders: TraderDefinition[]) => void }) {
  const trader = pack.traders[traderIndex]
  if (!trader) {
    return <div className="text-center text-tarkov-text-dim mt-20">Select a trader to edit.</div>
  }

  const updateTrader = (updates: Partial<TraderDefinition>) => {
    const next = [...pack.traders]
    next[traderIndex] = { ...next[traderIndex], ...updates }
    onChange(next)
  }

  const updateEntry = (index: number, updates: Partial<TraderItemEntry>) => {
    const next = [...trader.entries]
    next[index] = { ...next[index], ...updates }
    updateTrader({ entries: next })
  }

  const addEntry = () => {
    const allItems = [...pack.questItems, ...pack.keys, ...pack.containers, ...pack.stims, ...pack.medkits]
    const itemId = allItems[0]?.id || ''
    updateTrader({ entries: [...trader.entries, createDefaultTraderEntry(itemId)] })
  }

  const removeEntry = (index: number) => {
    updateTrader({ entries: trader.entries.filter((_, i) => i !== index) })
  }

  const allItems = [...pack.questItems, ...pack.keys, ...pack.containers, ...pack.stims, ...pack.medkits]
  const itemOptions = allItems.map(item => ({
    value: item.id,
    label: `${item.name} (${item.shortName})`,
    sub: item.id,
  }))

  return (
    <div className="space-y-4 max-w-5xl">
      <section className="card">
        <div className="flex items-center justify-between flex-wrap gap-3">
          <div className="flex items-center gap-3">
            <Toggle checked={trader.enabled} onChange={v => updateTrader({ enabled: v })} />
            <span className="font-semibold text-tarkov-accent text-lg">
              {VANILLA_TRADERS.find(t => t.id === trader.traderId)?.name || trader.traderId}
            </span>
            <span className="text-xs text-tarkov-text-dim font-mono">{trader.traderId}</span>
          </div>
          <div className="text-sm text-tarkov-text-dim">{trader.entries.filter(e => e.enabled).length} of {trader.entries.length} entries enabled</div>
        </div>
      </section>

      <Section title="Trader Entries" icon={<Package size={18} />}>
        <div className="space-y-4">
          {trader.entries.map((entry, i) => {
            const item = allItems.find(item => item.id === entry.itemId)
            return (
              <div key={i} className="bg-tarkov-bg border border-tarkov-border rounded-lg p-4">
                <div className="flex items-center justify-between mb-4">
                  <Toggle checked={entry.enabled} onChange={v => updateEntry(i, { enabled: v })} label={`Entry ${i + 1}`} />
                  <button className="btn-danger text-xs flex items-center gap-1" onClick={() => removeEntry(i)}>
                    <Trash2 size={14} /> Remove
                  </button>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
                  <Field label="Item" tooltip="Custom item to sell at this trader.">
                    <SearchableSelect
                      value={entry.itemId}
                      onChange={id => updateEntry(i, { itemId: id })}
                      options={itemOptions}
                      placeholder="Search item..."
                    />
                    {item && <div className="text-xs text-tarkov-text-dim font-mono mt-1">{item.id}</div>}
                  </Field>
                  <Field label="Price (₽)" tooltip="Price in roubles.">
                    <input className="input-field" type="number" min={0} value={entry.priceRoubles} onChange={e => updateEntry(i, { priceRoubles: parseInt(e.target.value) || 0 })} />
                  </Field>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                  <Field label="Loyalty Level" tooltip="Trader level required.">
                    <input className="input-field" type="number" min={1} max={4} value={entry.loyaltyLevel} onChange={e => updateEntry(i, { loyaltyLevel: parseInt(e.target.value) || 1 })} />
                  </Field>
                  <Field label="Stock Count" tooltip="Amount available after restock.">
                    <input className="input-field" type="number" min={0} disabled={entry.unlimitedStock} value={entry.stockCount} onChange={e => updateEntry(i, { stockCount: parseInt(e.target.value) || 0 })} />
                  </Field>
                  <Field label="Buy Restriction Max" tooltip="Max a player can buy per restock.">
                    <input className="input-field" type="number" min={0} disabled={entry.unlimitedBuyRestriction} value={entry.buyRestrictionMax} onChange={e => updateEntry(i, { buyRestrictionMax: parseInt(e.target.value) || 0 })} />
                  </Field>
                  <div className="flex flex-col gap-2 justify-end">
                    <Toggle checked={entry.unlimitedStock} onChange={v => updateEntry(i, { unlimitedStock: v })} label="Unlimited stock" />
                    <Toggle checked={entry.unlimitedBuyRestriction} onChange={v => updateEntry(i, { unlimitedBuyRestriction: v })} label="Unlimited buy restriction" />
                  </div>
                </div>
              </div>
            )
          })}

          {trader.entries.length === 0 && (
            <div className="text-sm text-tarkov-text-dim text-center py-4">No entries yet. Add one to sell a custom item at this trader.</div>
          )}

          <button className="btn-secondary flex items-center gap-1.5" onClick={addEntry}>
            <Plus size={14} /> Add Entry
          </button>
        </div>
      </Section>
    </div>
  )
}

function FilterIdChips({ ids }: { ids: string[] }) {
  const visibleIds = ids.filter(Boolean)
  return (
    <div className="flex flex-wrap gap-1 mt-2">
      {visibleIds.map(id => {
        const name = getItemOrCategoryName(id)
        const isCategory = name?.startsWith('Category:')
        return (
          <a
            key={id}
            href={`https://db.sp-tarkov.com/items/${id}`}
            target="_blank"
            rel="noopener noreferrer"
            className={`text-xs px-2 py-1 rounded border ${name ? 'bg-tarkov-accent/10 border-tarkov-accent text-tarkov-text' : 'bg-tarkov-bg border-tarkov-border text-tarkov-text-dim'}`}
            title={name ? `${name} — click to open in DB` : 'Unknown item — click to open in DB'}
          >
            {id} {name && <span className={`${isCategory ? 'text-tarkov-success' : 'text-tarkov-accent'}`}>{name}</span>}
          </a>
        )
      })}
      {visibleIds.length === 0 && <span className="text-xs text-tarkov-text-dim">No IDs set</span>}
    </div>
  )
}

const STIM_BUFF_TYPES = [
  'SkillRate',
  'HealthRate',
  'MaxStamina',
  'StaminaRate',
  'EnergyRate',
  'HydrationRate',
  'DamageModifier',
  'Contusion',
  'ContusionBlur',
  'ContusionWiggle',
  'Pain',
  'HandsTremor',
  'QuantumTunnelling',
  'RemoveNegativeEffects',
  'RemoveAllBuffs',
  'RemoveAllBloodLosses',
  'LightBleeding',
  'HeavyBleeding',
  'Fracture',
  'StomachBloodloss',
  'Antidote',
  'BodyTemperature',
  'WeightLimit',
  'UnknownToxin',
  'LethalToxin',
  'HalloweenBuff',
  'MisfireEffect',
  'ZombieInfection',
  'FrostbiteBuff',
  'Custom',
]

const HEALTH_FACTORS = [
  'Health',
  'Hydration',
  'Energy',
  'Radiation',
  'Temperature',
  'Poisoning',
  'Effect',
]

const DAMAGE_EFFECT_TYPES = [
  'HeavyBleeding',
  'LightBleeding',
  'Fracture',
  'Contusion',
  'Intoxication',
  'LethalIntoxication',
  'RadExposure',
  'Pain',
  'DestroyedPart',
]

const KNOWN_STIM_BUFFS = [
  'BuffsMorphine',
  'BuffsAdrenaline',
  'BuffsPropital',
  'BuffsETGChange',
  'BuffsXTG12',
  'BuffsPerfotoran',
  'BuffsAHF1M',
  'BuffsZagustin',
  'BuffsPNB',
  'BuffsP22',
  'BuffsMeldonin',
  'BuffsSJ1TGLabs',
  'BuffsL1',
  'BuffsSJ6TGLabs',
  'Buffs3bTG',
  'Buffs2A2bTG',
  'BuffsTrimadol',
  'BuffsObdolbos',
  'BuffsObdolbos2',
  'BuffsMULE',
  'BuffsSJ9TGLabs',
  'BuffsSJ12TGLabs',
  'Custom',
]

const MED_EFFECT_TYPES = ['duringUse', 'afterUse', 'onUse', 'duringUseOrAfterUse']

const SKILL_NAMES = [
  'Metabolism',
  'Vitality',
  'Health',
  'Strength',
  'Endurance',
  'StressResistance',
  'Immunity',
  'Perception',
  'Attention',
  'Intellect',
  'Charisma',
  'Memory',
  'MagDrill',
  'RecoilControl',
  'Search',
  'CovertMovement',
  'ProneMovement',
  'Sprinting',
  'FieldMedicine',
  'LightVests',
  'HeavyVests',
  'WeaponMods',
  'Custom',
]

interface EffectSelectorProps {
  options: string[]
  onSelect: (value: string) => void
  label: string
}

function EffectSelector({ options, onSelect, label }: EffectSelectorProps) {
  const [value, setValue] = useState('')

  const handleAdd = () => {
    if (value) {
      onSelect(value)
      setValue('')
    }
  }

  return (
    <div className="flex items-center gap-2">
      <select className="input-field text-xs" value={value} onChange={e => setValue(e.target.value)}>
        <option value="">{label}</option>
        {options.map(o => <option key={o} value={o}>{o}</option>)}
      </select>
      <button className="btn-secondary text-xs flex items-center gap-1" onClick={handleAdd} disabled={!value}>
        <Plus size={14} /> Add
      </button>
    </div>
  )
}

interface StimSpecificsProps {
  stim: StimDefinition
  onChange: (updates: Partial<StimDefinition>) => void
  mode?: 'stim' | 'medkit'
}

function StimSpecifics({ stim, onChange, mode = 'stim' }: StimSpecificsProps) {
  const props = stim.properties || {}

  const updateProp = (key: string, value: any) => {
    const next = { ...props, [key]: value }
    onChange({ properties: next })
  }

  const updateBuff = (index: number, updates: Partial<StimBuff>) => {
    const next = stim.customBuffs.map((b, i) => (i === index ? { ...b, ...updates } : b))
    onChange({ customBuffs: next })
  }

  const addBuff = () => {
    onChange({ customBuffs: [...stim.customBuffs, createDefaultStimBuff()] })
  }

  const removeBuff = (index: number) => {
    onChange({ customBuffs: stim.customBuffs.filter((_, i) => i !== index) })
  }

  const isCustomBuff = stim.stimulatorBuffs !== '' && !KNOWN_STIM_BUFFS.includes(stim.stimulatorBuffs)

  const setStimulatorBuffs = (value: string) => {
    const nextProps = { ...stim.properties, StimulatorBuffs: value }
    onChange({ stimulatorBuffs: value, properties: nextProps })
  }

  const updateHealthEffect = (key: string, updates: Partial<EffectsHealthProperties>) => {
    const next = { ...stim.effectsHealth, [key]: { ...(stim.effectsHealth[key] ?? {}), ...updates } }
    onChange({ effectsHealth: next })
  }

  const removeHealthEffect = (key: string) => {
    const { [key]: _, ...next } = stim.effectsHealth
    onChange({ effectsHealth: next })
  }

  const addHealthEffect = (key: string) => {
    if (key && !stim.effectsHealth[key]) {
      onChange({ effectsHealth: { ...stim.effectsHealth, [key]: {} } })
    }
  }

  const updateDamageEffect = (key: string, updates: Partial<EffectsDamageProperties>) => {
    const next = { ...stim.effectsDamage, [key]: { ...(stim.effectsDamage[key] ?? {}), ...updates } }
    onChange({ effectsDamage: next })
  }

  const removeDamageEffect = (key: string) => {
    const { [key]: _, ...next } = stim.effectsDamage
    onChange({ effectsDamage: next })
  }

  const addDamageEffect = (key: string) => {
    if (key && !stim.effectsDamage[key]) {
      onChange({ effectsDamage: { ...stim.effectsDamage, [key]: {} } })
    }
  }

  const isMedKit = mode === 'medkit'

  return (
    <Section title={isMedKit ? 'MedKit Specifics' : 'Stim Specifics'} icon={isMedKit ? <Package size={18} /> : <Syringe size={18} />}>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {!isMedKit && <Field label="Stimulator Buffs" tooltip="Existing buff set to use as a base, or leave as (Nothing) to only use the custom buffs below. If custom buffs are defined, the server will patch globals and reference that set here.">
          <select className="input-field" value={isCustomBuff ? 'Custom' : stim.stimulatorBuffs} onChange={e => setStimulatorBuffs(e.target.value === 'Custom' ? '' : e.target.value)}>
            <option value="">(Nothing)</option>
            {KNOWN_STIM_BUFFS.map(b => <option key={b} value={b}>{b}</option>)}
            <option value="Custom">Custom</option>
          </select>
          {isCustomBuff && <input className="input-field mt-2" value={stim.stimulatorBuffs} onChange={e => setStimulatorBuffs(e.target.value)} placeholder="Custom buff set name..." />}
        </Field>}
        <Field label="Med Effect Type" tooltip="When the stim effect applies.">
          <select className="input-field" value={stim.medEffectType} onChange={e => onChange({ medEffectType: e.target.value })}>
            {MED_EFFECT_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
          </select>
        </Field>
        <Field label="Med Use Time" tooltip="Injection/use time in seconds.">
          <input className="input-field" type="number" min={0} step="0.1" value={stim.medUseTime} onChange={e => onChange({ medUseTime: parseFloat(e.target.value) || 0 })} />
        </Field>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mt-4">
        <Field label="Max HP Resource" tooltip={isMedKit ? 'HP resource pool for this medkit.' : 'HP resource pool for medical items. Usually 0 for stims.'}>
          <input className="input-field" type="number" min={0} value={stim.maxHpResource} onChange={e => onChange({ maxHpResource: parseInt(e.target.value) || 0 })} />
        </Field>
        <Field label="HP Resource Rate" tooltip={isMedKit ? 'HP restoration rate per use.' : 'HP regeneration rate. Usually 0 for stims.'}>
          <input className="input-field" type="number" min={0} value={stim.hpResourceRate} onChange={e => onChange({ hpResourceRate: parseInt(e.target.value) || 0 })} />
        </Field>
        {!isMedKit && <Field label="Max Body Parts" tooltip="Maximum number of body parts this stim can heal in one use (0 = unlimited). Randomly picks if more are damaged.">
          <input className="input-field" type="number" min={0} value={stim.maxBodyPartsToHeal} onChange={e => onChange({ maxBodyPartsToHeal: parseInt(e.target.value) || 0 })} />
        </Field>}
        <Field label="Stack Max Size" tooltip="Maximum number of this item that can stack in one cell.">
          <input className="input-field" type="number" min={1} value={stim.stackMaxSize} onChange={e => onChange({ stackMaxSize: parseInt(e.target.value) || 1 })} />
        </Field>
        <Field label="Item Sound" tooltip="Sound identifier used when moving the item.">
          <input className="input-field" value={stim.itemSound} onChange={e => onChange({ itemSound: e.target.value })} />
        </Field>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
        <Field label="Width" tooltip="Inventory cell width.">
          <input className="input-field" type="number" min={1} value={stim.width} onChange={e => onChange({ width: parseInt(e.target.value) || 1 })} />
        </Field>
        <Field label="Height" tooltip="Inventory cell height.">
          <input className="input-field" type="number" min={1} value={stim.height} onChange={e => onChange({ height: parseInt(e.target.value) || 1 })} />
        </Field>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
        <Field label="Custom Model" tooltip="Custom bundle filename for the loot/inventory model (e.g. my_medkit.bundle). The client plugin matches this to a file in BepInEx\plugins\Serenity-ItemGen\bundles. Leave empty to inherit from the base template.">
          <input className="input-field" value={props.Prefab?.path ?? ''} onChange={e => updateProp('Prefab', { ...props.Prefab, path: e.target.value })} placeholder="my_medkit.bundle" />
        </Field>
        <Field label="Use Model" tooltip="Custom bundle filename for the in-hand/use animation model. Leave empty to inherit from the base template.">
          <input className="input-field" value={props.UsePrefab?.path ?? ''} onChange={e => updateProp('UsePrefab', { ...props.UsePrefab, path: e.target.value })} placeholder="my_medkit.bundle" />
        </Field>
      </div>

      {!isMedKit && <div className="mt-6">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-semibold text-tarkov-text">Custom Buffs</h3>
          <button className="btn-secondary text-xs flex items-center gap-1" onClick={addBuff}>
            <Plus size={14} /> Add Buff
          </button>
        </div>

        {stim.customBuffs.length === 0 && (
          <div className="text-sm text-tarkov-text-dim bg-tarkov-bg border border-tarkov-border rounded p-3">
            No custom buffs. Add buffs to fully customize the stim effects, or pick an existing Stimulator Buffs set above.
          </div>
        )}

        {stim.customBuffs.map((buff, i) => (
          <div key={buff.id} className="card p-3 mb-3 bg-tarkov-bg border border-tarkov-border">
            <div className="flex items-center justify-between mb-3">
              <span className="text-xs font-semibold text-tarkov-text-dim uppercase">Buff {i + 1}</span>
              <button className="btn-danger text-xs flex items-center gap-1" onClick={() => removeBuff(i)}>
                <Trash2 size={14} /> Remove
              </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              <Field label="Type" tooltip="Buff effect type. SkillRate requires a skill name. EnergyRate/HydrationRate affect hunger and thirst over time.">
                <select className="input-field" value={STIM_BUFF_TYPES.includes(buff.buffType) ? buff.buffType : 'Custom'} onChange={e => updateBuff(i, { buffType: e.target.value === 'Custom' ? '' : e.target.value })}>
                  {STIM_BUFF_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
                </select>
                {(!STIM_BUFF_TYPES.includes(buff.buffType) || buff.buffType === 'Custom') && (
                  <input className="input-field mt-2" value={buff.buffType} onChange={e => updateBuff(i, { buffType: e.target.value })} placeholder="Custom type..." />
                )}
              </Field>
              <Field label="Skill Name" tooltip="Skill affected when Type is SkillRate. Choose 'Custom' to type a skill not listed.">
                <select className="input-field" value={SKILL_NAMES.includes(buff.skillName) ? buff.skillName : 'Custom'} onChange={e => updateBuff(i, { skillName: e.target.value === 'Custom' ? '' : e.target.value })} disabled={buff.buffType !== 'SkillRate'}>
                  {SKILL_NAMES.map(s => <option key={s} value={s}>{s}</option>)}
                </select>
                {buff.buffType === 'SkillRate' && !SKILL_NAMES.includes(buff.skillName) && (
                  <input className="input-field mt-2" value={buff.skillName} onChange={e => updateBuff(i, { skillName: e.target.value })} placeholder="Custom skill name..." />
                )}
              </Field>
              <Field label="Value" tooltip="Positive for buff, negative for nerf. For EnergyRate/HydrationRate, negative drains hunger/thirst over time.">
                <input className="input-field" type="number" step="0.01" value={buff.value} onChange={e => updateBuff(i, { value: parseFloat(e.target.value) || 0 })} />
              </Field>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-4 gap-3 mt-3">
              <Field label="Delay (s)" tooltip="Seconds before the buff starts.">
                <input className="input-field" type="number" min={0} step="0.01" value={buff.delay} onChange={e => updateBuff(i, { delay: parseFloat(e.target.value) || 0 })} />
              </Field>
              <Field label="Duration (s)" tooltip="How long the buff lasts.">
                <input className="input-field" type="number" min={0} step="0.01" value={buff.duration} onChange={e => updateBuff(i, { duration: parseFloat(e.target.value) || 0 })} />
              </Field>
              <Field label="Chance" tooltip="0-1 chance for the buff to apply.">
                <input className="input-field" type="number" min={0} max={1} step="0.01" value={buff.chance} onChange={e => updateBuff(i, { chance: parseFloat(e.target.value) || 0 })} />
              </Field>
              <Field label="Absolute Value" tooltip="Whether the value is an absolute amount rather than a multiplier.">
                <Toggle checked={buff.absoluteValue} onChange={v => updateBuff(i, { absoluteValue: v })} />
              </Field>
            </div>
          </div>
        ))}
      </div>}

      <div className="mt-6">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-semibold text-tarkov-text">Health Effects</h3>
          <EffectSelector options={HEALTH_FACTORS} onSelect={addHealthEffect} label="Add health effect" />
        </div>

        {Object.keys(stim.effectsHealth).length === 0 && (
          <div className="text-sm text-tarkov-text-dim bg-tarkov-bg border border-tarkov-border rounded p-3">
            No health effects. Add effects like Hydration/Energy regeneration.
          </div>
        )}

        {Object.entries(stim.effectsHealth).map(([key, effect]) => (
          <div key={key} className="card p-3 mb-3 bg-tarkov-bg border border-tarkov-border">
            <div className="flex items-center justify-between mb-3">
              <span className="text-xs font-semibold text-tarkov-text-dim uppercase">{key}</span>
              <button className="btn-danger text-xs flex items-center gap-1" onClick={() => removeHealthEffect(key)}>
                <Trash2 size={14} /> Remove
              </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              <Field label="Value" tooltip="Magnitude of the health effect.">
                <input className="input-field" type="number" step="0.01" value={effect.value ?? ''} onChange={e => updateHealthEffect(key, { value: e.target.value === '' ? undefined : parseFloat(e.target.value) })} />
              </Field>
              <Field label="Delay (s)" tooltip="Seconds before the effect starts.">
                <input className="input-field" type="number" min={0} step="0.01" value={effect.delay ?? ''} onChange={e => updateHealthEffect(key, { delay: e.target.value === '' ? undefined : parseFloat(e.target.value) })} />
              </Field>
              <Field label="Duration (s)" tooltip="How long the effect lasts.">
                <input className="input-field" type="number" min={0} step="0.01" value={effect.duration ?? ''} onChange={e => updateHealthEffect(key, { duration: e.target.value === '' ? undefined : parseFloat(e.target.value) })} />
              </Field>
            </div>
          </div>
        ))}
      </div>

      <div className="mt-6">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-semibold text-tarkov-text">Damage Effects</h3>
          <EffectSelector options={DAMAGE_EFFECT_TYPES} onSelect={addDamageEffect} label="Add damage effect" />
        </div>

        {Object.keys(stim.effectsDamage).length === 0 && (
          <div className="text-sm text-tarkov-text-dim bg-tarkov-bg border border-tarkov-border rounded p-3">
            No damage effects. Add effects the item treats, like Fracture or Pain.
          </div>
        )}

        {Object.entries(stim.effectsDamage).map(([key, effect]) => (
          <div key={key} className="card p-3 mb-3 bg-tarkov-bg border border-tarkov-border">
            <div className="flex items-center justify-between mb-3">
              <span className="text-xs font-semibold text-tarkov-text-dim uppercase">{key}</span>
              <button className="btn-danger text-xs flex items-center gap-1" onClick={() => removeDamageEffect(key)}>
                <Trash2 size={14} /> Remove
              </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
              <Field label="Cost" tooltip="HP resource cost to remove the effect. For medkits this is the amount of HP resource consumed to cure the effect.">
                <input className="input-field" type="number" min={0} step="0.01" value={effect.cost ?? ''} onChange={e => updateDamageEffect(key, { cost: e.target.value === '' ? undefined : parseFloat(e.target.value) })} />
              </Field>
              <Field label="Delay (s)" tooltip="Seconds before the effect starts.">
                <input className="input-field" type="number" min={0} step="0.01" value={effect.delay ?? ''} onChange={e => updateDamageEffect(key, { delay: e.target.value === '' ? undefined : parseFloat(e.target.value) })} />
              </Field>
              <Field label="Duration (s)" tooltip="How long the effect lasts.">
                <input className="input-field" type="number" min={0} step="0.01" value={effect.duration ?? ''} onChange={e => updateDamageEffect(key, { duration: e.target.value === '' ? undefined : parseFloat(e.target.value) })} />
              </Field>
              <Field label="Fade Out (s)" tooltip="Fade out duration of the effect.">
                <input className="input-field" type="number" min={0} step="0.01" value={effect.fadeOut ?? ''} onChange={e => updateDamageEffect(key, { fadeOut: e.target.value === '' ? undefined : parseFloat(e.target.value) })} />
              </Field>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-3 mt-3">
              <Field label="Health Penalty Min" tooltip="Minimum health penalty.">
                <input className="input-field" type="number" step="0.01" value={effect.healthPenaltyMin ?? ''} onChange={e => updateDamageEffect(key, { healthPenaltyMin: e.target.value === '' ? undefined : parseFloat(e.target.value) })} />
              </Field>
              <Field label="Health Penalty Max" tooltip="Maximum health penalty.">
                <input className="input-field" type="number" step="0.01" value={effect.healthPenaltyMax ?? ''} onChange={e => updateDamageEffect(key, { healthPenaltyMax: e.target.value === '' ? undefined : parseFloat(e.target.value) })} />
              </Field>
            </div>
          </div>
        ))}
      </div>
    </Section>
  )
}

interface ContainerSpecificsProps {
  container: ContainerDefinition
  onChange: (updates: Partial<ContainerDefinition>) => void
}

function ContainerSpecifics({ container, onChange }: ContainerSpecificsProps) {
  const props = container.properties || {}

  const updateProp = (key: string, value: any) => {
    const next = { ...props, [key]: value }
    onChange({ properties: next })
  }

  const grids = (props.Grids as any[]) || []
  const updateGrid = (index: number, gridUpdate: any) => {
    const next = grids.map((g, i) => (i === index ? { ...g, ...gridUpdate } : g))
    updateProp('Grids', next)
  }

  return (
    <Section title="Container Specifics" icon={<Package size={18} />}>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Field label="Width" tooltip="Outer width of the container in inventory cells.">
          <input className="input-field" type="number" min={1} value={props.Width ?? 1} onChange={e => updateProp('Width', parseInt(e.target.value) || 1)} />
        </Field>
        <Field label="Height" tooltip="Outer height of the container in inventory cells.">
          <input className="input-field" type="number" min={1} value={props.Height ?? 1} onChange={e => updateProp('Height', parseInt(e.target.value) || 1)} />
        </Field>
        <Field label="Item Sound" tooltip="Sound identifier used when moving the container.">
          <input className="input-field" value={props.ItemSound ?? ''} onChange={e => updateProp('ItemSound', e.target.value)} />
        </Field>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
        <Field label="Custom Model" tooltip="Custom bundle filename for the loot/inventory model (e.g. my_container.bundle). The client plugin matches this to a file in BepInEx\plugins\Serenity-ItemGen\bundles. Leave empty to inherit from the base template.">
          <input className="input-field" value={props.Prefab?.path ?? ''} onChange={e => updateProp('Prefab', { ...props.Prefab, path: e.target.value })} placeholder="my_container.bundle" />
        </Field>
        <Field label="Use Model" tooltip="Custom bundle filename for the in-hand/interaction model. Leave empty to inherit from the base template.">
          <input className="input-field" value={props.UsePrefab?.path ?? ''} onChange={e => updateProp('UsePrefab', { ...props.UsePrefab, path: e.target.value })} placeholder="my_container.bundle" />
        </Field>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mt-4">
        <Field label="Can Put Into During Raid" tooltip="Whether the container can be placed inside other containers during a raid.">
          <Toggle checked={props.CanPutIntoDuringTheRaid ?? false} onChange={v => updateProp('CanPutIntoDuringTheRaid', v)} />
        </Field>
        <Field label="Hide Entrails" tooltip="Whether the container's contents are hidden on the player model.">
          <Toggle checked={props.HideEntrails ?? true} onChange={v => updateProp('HideEntrails', v)} />
        </Field>
        <Field label="Examined By Default" tooltip="Whether the item starts examined for the player.">
          <Toggle checked={props.ExaminedByDefault ?? true} onChange={v => updateProp('ExaminedByDefault', v)} />
        </Field>
      </div>

      <div className="mt-4">
        <h3 className="text-sm font-semibold text-tarkov-text mb-2">Grids</h3>
        {grids.length === 0 && <div className="text-sm text-tarkov-text-dim">No grids defined.</div>}
        {grids.map((grid, i) => {
          const gProps = grid._props || {}
          const filter = (gProps.filters?.[0]?.Filter || []) as string[]
          const excluded = (gProps.filters?.[0]?.ExcludedFilter || []) as string[]
          return (
            <div key={i} className="card p-3 mb-3">
              <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
                <Field label="Grid Name">
                  <input className="input-field" value={grid._name || ''} onChange={e => updateGrid(i, { _name: e.target.value })} />
                </Field>
                <Field label="Cells H">
                  <input className="input-field" type="number" min={1} value={gProps.cellsH ?? 1} onChange={e => updateGrid(i, { _props: { ...gProps, cellsH: parseInt(e.target.value) || 1 } })} />
                </Field>
                <Field label="Cells V">
                  <input className="input-field" type="number" min={1} value={gProps.cellsV ?? 1} onChange={e => updateGrid(i, { _props: { ...gProps, cellsV: parseInt(e.target.value) || 1 } })} />
                </Field>
                <Field label="Max Weight">
                  <input className="input-field" type="number" step="0.01" value={gProps.maxWeight ?? 0} onChange={e => updateGrid(i, { _props: { ...gProps, maxWeight: parseFloat(e.target.value) || 0 } })} />
                </Field>
              </div>
              <div className="flex items-center justify-between mt-3">
                <span className="text-xs text-tarkov-text-dim">Allowed IDs</span>
                <a className="text-xs text-tarkov-accent hover:underline flex items-center gap-1" href="https://db.sp-tarkov.com" target="_blank" rel="noopener noreferrer">
                  Find IDs here
                </a>
              </div>
              <Field label="Allowed Item IDs (comma-separated)" className="mt-1">
                <input className="input-field" placeholder="e.g. 5448eb774bdc2d0a728b4567, ..." value={filter.join(', ')} onChange={e => {
                  const ids = e.target.value.split(',').map(s => s.trim())
                  const nextFilters = [{ ...(gProps.filters?.[0] || {}), Filter: ids }]
                  updateGrid(i, { _props: { ...gProps, filters: nextFilters } })
                }} />
                <FilterIdChips ids={filter} />
              </Field>
              <Field label="Excluded Item IDs (comma-separated)" className="mt-3">
                <input className="input-field" placeholder="e.g. 5448eb774bdc2d0a728b4567, ..." value={excluded.join(', ')} onChange={e => {
                  const ids = e.target.value.split(',').map(s => s.trim())
                  const nextFilters = [{ ...(gProps.filters?.[0] || {}), ExcludedFilter: ids }]
                  updateGrid(i, { _props: { ...gProps, filters: nextFilters } })
                }} />
                <FilterIdChips ids={excluded} />
              </Field>
            </div>
          )
        })}
      </div>

      <div className="mt-6 border-t border-tarkov-border pt-4">
        <h3 className="text-sm font-semibold text-tarkov-text mb-2">Safe Container Allow-list</h3>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <Field label="Mode" tooltip="All = allow inside every vanilla safe container. Include only = allow only inside the listed IDs. Exclude only = allow inside all vanilla safe containers except the listed IDs.">
            <select className="input-field" value={container.safeContainerMode} onChange={e => onChange({ safeContainerMode: e.target.value as ContainerDefinition['safeContainerMode'] })}>
              <option value="all">All safe containers</option>
              <option value="include">Include only</option>
              <option value="exclude">Exclude only</option>
            </select>
          </Field>
          <Field label="Safe Container IDs" tooltip="Comma-separated safe container IDs. Only used when Mode is Include or Exclude. Leave empty for All." className="md:col-span-2">
            <input className="input-field" placeholder="e.g. 544a11ac4bdc2d470e8b456a, 5857a8b324597729ab0a0e7d" value={container.safeContainerIds.join(', ')} onChange={e => onChange({ safeContainerIds: e.target.value.split(',').map(s => s.trim()).filter(Boolean) })} disabled={container.safeContainerMode === 'all'} />
          </Field>
        </div>
        <div className="mt-2 text-xs text-tarkov-text-dim">
          Default vanilla IDs: Alpha 544a11ac4bdc2d470e8b456a, Beta 5857a8b324597729ab0a0e7d, Gamma 5857a8bc2459772bad15db29, Epsilon 59db794186f77448bc595262, Kappa 5c093ca986f7740a1867ab12, Theta 664a55d84a90fc2c8a6305c9, Waist pouch 60b0f9326c1d6c524c5ce3d5
        </div>
      </div>

    </Section>
  )
}

function Field({ label, children, className = '', tooltip, error }: { label: string; children: React.ReactNode; className?: string; tooltip?: string; error?: boolean }) {
  return (
    <div className={className}>
      <label className={`label flex items-center gap-1.5 ${error ? 'text-tarkov-error' : ''}`}>
        {label}
        {tooltip && (
          <Tooltip text={tooltip}>
            <HelpCircle size={13} className="text-tarkov-text-dim hover:text-tarkov-accent cursor-help transition-colors" />
          </Tooltip>
        )}
      </label>
      {children}
    </div>
  )
}

function Tooltip({ text, children }: { text: string; children: React.ReactElement }) {
  const [visible, setVisible] = useState(false)
  const [pos, setPos] = useState({ x: 0, y: 0, width: 0 })
  const ref = useRef<HTMLSpanElement>(null)

  const show = () => {
    if (ref.current) {
      const rect = ref.current.getBoundingClientRect()
      setPos({ x: rect.left + rect.width / 2, y: rect.top, width: rect.width })
    }
    setVisible(true)
  }

  const tooltip = (
    <div
      className="fixed z-[100] pointer-events-none px-3 py-2 bg-tarkov-bg border border-tarkov-border rounded-lg text-xs text-tarkov-text font-normal w-64 shadow-xl leading-relaxed"
      style={{ left: Math.max(8, Math.min(window.innerWidth - 272, pos.x - 128)), top: Math.max(8, pos.y - 8), transform: 'translateY(-100%)' }}
    >
      {text}
    </div>
  )

  return (
    <>
      <span ref={ref} onMouseEnter={show} onMouseLeave={() => setVisible(false)} onFocus={show} onBlur={() => setVisible(false)} tabIndex={0} className="inline-flex">
        {children}
      </span>
      {visible && createPortal(tooltip, document.body)}
    </>
  )
}

function FieldErrors({ errors }: { errors: ValidationError[] }) {
  if (errors.length === 0) return null
  return (
    <div className="mt-1 space-y-0.5">
      {errors.map((e, i) => (
        <p key={i} className="text-xs text-tarkov-error flex items-center gap-1">
          <AlertCircle size={10} /> {e.message}
        </p>
      ))}
    </div>
  )
}

function Section({ title, icon, children }: { title: string; icon: React.ReactNode; children: React.ReactNode }) {
  return (
    <section className="card">
      <h2 className="text-lg font-semibold text-tarkov-accent mb-4 flex items-center gap-2">
        {icon} {title}
      </h2>
      {children}
    </section>
  )
}

function Toggle({ checked, onChange, label }: { checked: boolean; onChange: (v: boolean) => void; label?: string }) {
  return (
    <label className="toggle flex items-center gap-2 cursor-pointer">
      <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} />
      <span className="toggle-track"></span>
      <span className="toggle-thumb"></span>
      {label && <span className="text-sm text-tarkov-text">{label}</span>}
    </label>
  )
}

interface SearchableSelectProps {
  value: string
  onChange: (value: string) => void
  options: { value: string; label: string; sub?: string }[]
  placeholder?: string
}

function SearchableSelect({ value, onChange, options, placeholder }: SearchableSelectProps) {
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const containerRef = useRef<HTMLDivElement>(null)
  const selected = options.find(o => o.value === value)

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [])

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return options.slice(0, 50)
    return options.filter(o => o.label.toLowerCase().includes(q) || o.value.toLowerCase().includes(q)).slice(0, 100)
  }, [options, query])

  return (
    <div ref={containerRef} className="relative flex-1">
      <div className="relative">
        <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-tarkov-text-dim pointer-events-none" />
        <input
          className="input-field w-full pl-9"
          placeholder={placeholder}
          value={open ? query : selected?.label || value}
          onFocus={() => {
            setQuery(selected?.label || '')
            setOpen(true)
          }}
          onChange={(e) => {
            setQuery(e.target.value)
            setOpen(true)
          }}
        />
      </div>
      {open && (
        <div className="absolute z-50 mt-1 w-full max-h-60 overflow-y-auto bg-tarkov-surface border border-tarkov-border rounded shadow-lg">
          {filtered.length === 0 ? (
            <div className="px-3 py-2 text-sm text-tarkov-text-dim">No matches</div>
          ) : (
            filtered.map((o) => (
              <button
                key={o.value}
                className="w-full text-left px-3 py-2 text-sm hover:bg-tarkov-border/50 text-tarkov-text"
                onClick={() => {
                  onChange(o.value)
                  setQuery(o.label)
                  setOpen(false)
                }}
              >
                <div className="truncate">{o.label}</div>
                <div className="text-xs text-tarkov-text-dim font-mono truncate">{o.value}</div>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}
