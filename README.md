# ItemGen - Custom Item Framework for SPTarkov 4.0.13

A server-side framework for SPTarkov that lets anyone, including non-programmers, create custom items using simple JSON packs. Includes a web-based **ItemGen Tool** for generating packs visually and a client-side bundle injector for custom 3D models.

> **New in v1.0.0**: Support for **Stims**, **Containers**, and **Custom Bundle Injection** with drag-and-drop bundle packaging in the web tool.

---

## Quick Start

### For Players (Installing ItemGen)

1. Install the base **ItemGen** framework by dragging the `SPT/` and `BepInEx/` folders from the ItemGen release into your SPT install directory.
2. Start the SPT server once so the `ItemGen/items/` folder is created.
3. ItemGen is now ready for addon packs.

### For Players (Installing an Item Pack Addon)

1. Make sure **ItemGen** is already installed.
2. Place the addon pack's `.json` file into `SPT/user/mods/ItemGen/items/` (or a subfolder).
3. If the addon includes custom bundles, place them in `BepInEx/plugins/Serenity-ItemGen/bundles/`.
4. Start the SPT server — custom items are injected automatically.

### For Creators (Making an Item Pack)

1. Open the ItemGen Tool.
2. Pick a base item template, set name/description/stats, and configure traders.
3. For custom 3D models, drop bundle files into the **Bundles** tab and set the matching **Custom Model** path on your items.
4. Click **Export** to download a ready-to-use ZIP.
5. Extract the ZIP and drag the `SPT/` (and `BepInEx/` if bundles are included) folders into your SPT install directory to test.
6. Publish your pack as an **ItemGen addon** — users need **ItemGen** installed as a dependency.

> **Tip**: Always test on a new developer profile so you can verify trader listings and item rendering without affecting your main save.

---

## Export Format

The ItemGen Tool exports a pre-packaged ZIP that drops straight into any SPT install:

### Without custom bundles:

```
SPT/
└── user/
    └── mods/
        └── ItemGen/
            └── items/
                └── my-item-pack.json
```

### With custom bundles:

```
SPT/
└── user/
    └── mods/
        └── ItemGen/
            └── items/
                └── my-item-pack.json
BepInEx/
└── plugins/
    └── Serenity-ItemGen/
        └── bundles/
            └── my_bundle.bundle
```

You can also click **Export JSON** to download a single `.json` file and place it manually in `SPT/user/mods/ItemGen/items/`.

---

## Features

- **Item pack JSON editor** with live preview and validation.
- **Clone base item templates** for quest items, keys, containers, and stims.
- **Custom bundle injection** for unique 3D models and use animations.
- **Drag-and-drop bundle packaging** in the web tool.
- **Vanilla trader integration** with per-item entries.
- **Auto-fill stats** when selecting a base item template.
- **Export / import** packs as JSON or ready-to-install ZIP.
- **Tooltips** on every major field.
- **Dark theme** and responsive layout.

---

## Item Pack JSON

A pack is a single JSON file containing one or more custom item definitions.

```json
{
  "enabled": true,
  "name": "My Custom Items",
  "questItems": [
    {
      "id": "010000000000000000000001",
      "enabled": true,
      "baseTpl": "5937fd0086f7742bf33fc198",
      "name": "Custom Quest Watch",
      "shortName": "CQWatch",
      "description": "A quest item for a custom mission.",
      "weight": 0.5,
      "backgroundColor": "yellow",
      "stackMaxSize": 1,
      "handbookPriceRoubles": 0,
      "fleaPriceRoubles": 0,
      "rarityPvE": "Not_exist",
      "canSellOnRagfair": true,
      "questIds": []
    }
  ],
  "keys": [
    {
      "id": "020000000000000000000001",
      "enabled": true,
      "baseTpl": "5672c92d4bdc2d180f8b4567",
      "name": "Custom Key",
      "shortName": "CKey",
      "description": "A key for a custom door.",
      "weight": 0.01,
      "backgroundColor": "blue",
      "uses": 40,
      "keyCategory": "",
      "doorIds": [],
      "handbookPriceRoubles": 1000,
      "fleaPriceRoubles": 5000,
      "rarityPvE": "Common",
      "canSellOnRagfair": true
    }
  ],
  "containers": [],
  "stims": [],
  "traders": []
}
```

Find item template IDs at: https://db.sp-tarkov.com/search

---

## Field Reference

| Field | Description |
|-------|-------------|
| `id` | Unique 24-character hex item ID for the new item. |
| `baseTpl` | Existing vanilla item template to clone. |
| `name` / `shortName` / `description` | Locale strings shown in-game. |
| `weight` | Item weight in kilograms. |
| `backgroundColor` | Optional inventory cell color (`yellow`, `blue`, `red`, etc.). |
| `handbookPriceRoubles` | Handbook price. |
| `fleaPriceRoubles` | Flea market price. |
| `rarityPvE` | PvE rarity (`Not_exist`, `Common`, `Rare`, `Superrare`, `Legendary`). |
| `canSellOnRagfair` | Whether the item can be listed on the flea market. |

### Quest Item Fields

| Field | Description |
|-------|-------------|
| `stackMaxSize` | Maximum stack size in a single inventory cell. |
| `questIds` | Optional quest IDs that mark this item as a quest item. |

### Key Fields

| Field | Description |
|-------|-------------|
| `uses` | Maximum number of uses before the key is consumed. |
| `keyCategory` | Optional key category. |
| `doorIds` | Optional door IDs this key can unlock. |

### Container Fields

| Field | Description |
|-------|-------------|
| `parent` | Parent item category ID. |
| `handbookParentId` | Handbook parent category. |
| `properties` | Grid layout and other container properties. |

### Stim Fields

| Field | Description |
|-------|-------------|
| `itemSound` | Sound effect used by the stim. |
| `medEffectType` / `medUseTime` | Meds effect type and use time. |
| `maxHpResource` / `hpResourceRate` | Resource pool and drain rate. |
| `stackMaxSize` | Maximum stack size. |
| `width` / `height` | Inventory cell dimensions. |
| `customBuffs` | Optional custom buff definitions. |

### Vanilla Trader IDs

| Trader | ID |
|--------|-----|
| Prapor | `54cb50c76803fa8b248b4571` |
| Therapist | `54cb57776803fa99248b456e` |
| Skier | `58330581ace78e27b8b10cee` |
| Peacekeeper | `5935c25fb3acc3127c3d8cd9` |
| Fence | `579dc571d53a0658a154fbec` |
| Mechanic | `5a7c2eca46aef81a7ca2145d` |
| Ragman | `5ac3b934156ae10c4430e83c` |
| Jaeger | `5c0647fdd443bc2504c2d371` |
| Caretaker | `638f541a29ffd1183d187f57` |
| BTR | `656f0f98d80a697f855d34b1` |
| Arena | `6617beeaa9cfa777ca915b7c` |
| Storyteller | `6864e812f9fe664cb8b8e152` |

---

## Custom Models & Bundle Injection

ItemGen supports custom 3D models and use animations via the client-side **BundleInjector**.

1. Place bundle files in `BepInEx/plugins/Serenity-ItemGen/bundles/`.
2. In the web tool, set the **Custom Model** field on an item to the bundle filename (e.g. `my_stim.bundle`).
3. Optionally set **Use Model** for a separate in-hand animation bundle.
4. The client plugin automatically matches the bundle filename to the **Custom Model** path and loads it into the game.

If the **Custom Model** field is left empty, the item inherits the base template's model.

---

## Validation & Error Handling

ItemGen validates every pack on load and logs clear errors to the server console:

- Missing required fields (`id`, `baseTpl`, `name`, etc.)
- Invalid ID format (must be 24-character hex)
- Invalid trader IDs
- Invalid base templates

Invalid packs are **skipped** — other packs still load normally.

---

## Publishing an Item Pack Addon

The ItemGen Tool export ZIP is already structured for distribution. When publishing:

1. **State the dependency**: Your pack requires **ItemGen v1.1.0** for SPT 4.0.13.
2. **Publish as an addon**, not a standalone mod. Your ZIP should only contain your pack JSON and any custom bundles.
3. **Do not include** the ItemGen DLL or other authors' packs in your ZIP.
4. **Test** by extracting and running the server before publishing.

---

## Technical Details

- **SPT Version**: 4.0.13
- **Framework**: .NET 9.0, C#
- **DI Pattern**: `[Injectable]` + `IOnLoad`
- **NuGet Packages**: `SPTarkov.Common`, `SPTarkov.DI`, `SPTarkov.Server.Core` (4.0.13)
- **Tool**: React + TypeScript + Vite + TailwindCSS
- **Client plugin**: `BepInEx/plugins/Serenity-ItemGen/` handles custom bundle injection.

## License

MIT — Use freely for your SPT addons.
