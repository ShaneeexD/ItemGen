# ItemGen

ItemGen is a modular server-side framework for creating custom items in SPTarkov 4.0.13. It ships with an optional React web editor and starts with generators for **Quest Inventory Items** and **Keys**, with an extensible architecture for future item categories.

## Structure

```
ItemGen/
├── Server/                         # SPT server mod
│   ├── ItemGen.Server.csproj
│   ├── ItemGenPlugin.cs            # DI entry point
│   ├── Models/                     # Data definitions
│   ├── Services/                   # Pack loader
│   ├── Generators/                 # Quest & Key generators
│   ├── Validation/                 # Pack validators
│   ├── config/
│   │   └── config.json
│   └── package.json
├── WebTool/                        # React + Vite editor
│   ├── package.json
│   ├── src/
│   │   ├── App.tsx
│   │   ├── types.ts
│   │   ├── index.css
│   │   ├── generated_quest_templates.ts
│   │   └── generated_key_templates.ts
│   ├── tailwind.config.js
│   └── vite.config.ts
└── ItemGen.sln
```

## Quick Start

### Players

1. Install the server mod by copying the `Server/` folder into `SPT/user/mods/ItemGen/`.
2. Create or drop item pack JSON files into `SPT/user/mods/ItemGen/items/`.
3. Launch SPT server — custom items are injected automatically.

### Creators

1. Open `ItemGen.sln` in Visual Studio or Rider.
2. Build the server project. The `DeployToSPT` target copies the output to `C:\SPT\SPT\user\mods\ItemGen` automatically.
3. Use the web editor to design packs, export JSON, or download a ready-to-install ZIP.

### Web Tool

```bash
cd WebTool
npm install
npm run dev
npm run build
```

## Item Pack JSON Format

```json
{
  "enabled": true,
  "name": "My Custom Items",
  "questItems": [
    {
      "id": "0123456789abcdef01234567",
      "enabled": true,
      "baseTpl": "5937fd0086f7742bf33fc198",
      "name": "My Custom Watch",
      "shortName": "Custom Watch",
      "description": "A quest watch for my custom quest.",
      "weight": 0.5,
      "backgroundColor": "yellow",
      "stackMaxSize": 1,
      "handbookPriceRoubles": 0,
      "fleaPriceRoubles": 0,
      "rarityPvE": "Not_exist",
      "canSellOnRagfair": true
    }
  ],
  "keys": [
    {
      "id": "0123456789abcdef01234568",
      "enabled": true,
      "baseTpl": "5672c92d4bdc2d180f8b4567",
      "name": "My Custom Key",
      "shortName": "Custom Key",
      "description": "A key for a custom door.",
      "weight": 0.01,
      "backgroundColor": "blue",
      "uses": 40,
      "keyCategory": "",
      "handbookPriceRoubles": 1000,
      "fleaPriceRoubles": 5000,
      "rarityPvE": "Common",
      "canSellOnRagfair": true,
      "doorIds": []
    }
  ]
}
```

## Features

- **Modular generators** for Quest Inventory Items and Keys.
- **Quest items** behave exactly like vanilla EFT quest inventory items (`QuestItem = true`).
- **Keys** inherit base template behavior and support `MaximumNumberOfUsage`.
- **Validation** of pack JSON before loading, including 24-char hex ID checks.
- **Web tool** with dark theme, searchable base template dropdowns, import/export, live JSON preview, and ZIP export.
- **Auto-deployment** to SPT `user/mods/` folder after build.

## Generated Mod Package

The web tool exports a ZIP containing:

```
MyCustomItems/
├── package.json
├── config/
│   └── config.json
└── items/
    └── my_custom_items.json
```

Drop this folder into `SPT/user/mods/` and launch the server.

## Extending ItemGen

Add new item categories by creating a new generator class in `Server/Generators/` and extending the web tool types/tabs in `WebTool/src/types.ts` and `App.tsx`.

## License

MIT
