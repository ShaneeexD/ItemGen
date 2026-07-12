import { useEffect, useMemo, useState } from 'react'
import { Copy, Search } from 'lucide-react'
import vanillaBundles from '../vanillaBundles.json'

interface VanillaBundleItem {
  id: string
  name: string
  shortName: string
  prefab: string | null
  usePrefab: string | null
  bundles: string[]
}

const MAX_RESULTS = 100

const allItems = (Object.values(vanillaBundles) as VanillaBundleItem[]).map(item => ({
  ...item,
  searchText: `${item.name} ${item.shortName} ${item.id} ${item.bundles.join(' ')}`.toLowerCase()
}))

export function VanillaBundlesPanel() {
  const [query, setQuery] = useState('')
  const [debouncedQuery, setDebouncedQuery] = useState('')
  const [copied, setCopied] = useState<string | null>(null)

  useEffect(() => {
    const raw = query.trim()
    if (raw.length === 0) {
      setDebouncedQuery('')
      return
    }
    const timer = setTimeout(() => setDebouncedQuery(raw), 150)
    return () => clearTimeout(timer)
  }, [query])

  const filtered = useMemo(() => {
    const raw = debouncedQuery.trim()
    if (raw.length === 0) return { items: [], totalMatches: 0 }
    const terms = raw.toLowerCase().split(/\s+/)
    const items: VanillaBundleItem[] = []
    let totalMatches = 0
    for (const item of allItems) {
      if (terms.every(term => item.searchText.includes(term))) {
        totalMatches++
        if (items.length < MAX_RESULTS) {
          items.push(item)
        }
      }
    }
    return { items, totalMatches }
  }, [debouncedQuery])

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text).then(() => {
      setCopied(text)
      setTimeout(() => setCopied(null), 1500)
    })
  }

  return (
    <div className="space-y-4 max-w-5xl h-full flex flex-col">
      <section className="card">
        <h2 className="text-lg font-semibold text-tarkov-accent mb-4 flex items-center gap-2">
          <Search size={18} /> Vanilla Bundles
        </h2>
        <p className="text-sm text-tarkov-text-dim mb-3">
          Search for vanilla items by name, short name, or bundle path. Generated from <code className="bg-tarkov-bg px-1 rounded">items.json</code>.
        </p>
        <div className="relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-tarkov-text-dim" />
          <input
            type="text"
            className="input-field pl-9"
            placeholder="Search by item name, short name, bundle path..."
            value={query}
            onChange={e => setQuery(e.target.value)}
          />
        </div>
      </section>

      <section className="card flex-1 min-h-0 flex flex-col">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-semibold text-tarkov-text">Results</h3>
          <span className="text-xs text-tarkov-text-dim">
            {query.trim().length === 0 ? `Start typing to search ${allItems.length.toLocaleString()} items` : `${filtered.totalMatches.toLocaleString()} matches` + (filtered.totalMatches > MAX_RESULTS ? ` (showing ${MAX_RESULTS})` : '')}
          </span>
        </div>

        <div className="flex-1 overflow-y-auto -mx-4 px-4">
          {query.trim().length === 0 && (
            <div className="text-center text-tarkov-text-dim py-12">
              Type a search term to filter vanilla bundles.
            </div>
          )}

          {query.trim().length > 0 && filtered.totalMatches === 0 && (
            <div className="text-center text-tarkov-text-dim py-12">
              No items match your search.
            </div>
          )}

          <div className="space-y-2">
            {filtered.items.map(item => (
              <div
                key={item.id}
                className="bg-tarkov-bg border border-tarkov-border rounded p-3 text-sm"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <div className="font-semibold text-tarkov-text truncate">{item.name}</div>
                    <div className="text-xs text-tarkov-text-dim truncate">
                      {item.shortName} <span className="font-mono text-tarkov-text-dim/70">{item.id}</span>
                    </div>
                  </div>
                </div>

                <div className="mt-2 space-y-1">
                  {item.bundles.map((bundle, i) => (
                    <div
                      key={`${item.id}-${bundle}-${i}`}
                      className="flex items-center gap-2 group"
                    >
                      <code className="flex-1 min-w-0 text-xs font-mono bg-tarkov-surface border border-tarkov-border rounded px-2 py-1 truncate text-tarkov-text-dim">
                        {bundle}
                      </code>
                      <button
                        className="shrink-0 p-1.5 text-tarkov-text-dim hover:text-tarkov-accent transition-colors"
                        onClick={() => copyToClipboard(bundle)}
                        title="Copy bundle path"
                      >
                        <Copy size={14} />
                      </button>
                    </div>
                  ))}
                </div>

                {copied && (
                  <div className="text-xs text-tarkov-accent mt-2">Copied to clipboard</div>
                )}
              </div>
            ))}
          </div>
        </div>
      </section>
    </div>
  )
}
