# Changelog

## v1.2.5
- **Custom secure containers** - a new feature integrated into the existing container section: create a custom container and toggle `isSecured` so it behaves like a secure container in-game.
- **Raw JSON properties editor** - new for all existing item types. Add any extra field in the `Raw Properties JSON` panel; the game will register those fields even though they are not in the UI.
- **Reduced server logging** - item generators no longer log per-item success messages. Final startup output now shows `registered/enabled` counts for each item type plus total trader entries; per-item errors and warnings are still logged.

## v1.2.2
- **Custom secure containers** — container editor now has an `isSecured` toggle and a raw `properties` JSON editor. Secure containers can be chosen as base templates and their `isSecured` value is preserved.
- **Raw property preservation** — all item generators now merge raw `properties` from the pack into the generated item, so any extra JSON values are respected instead of dropped.

## v1.2.1
- **Client-side icon scaling** — custom items now render their inventory icon at the size defined by `Width`/`Height`, preventing oversized icons from overlapping adjacent slots.

## v1.2.0
- **Custom keys now work on vanilla doors** — assign custom keys to vanilla doors, including multiple keys per door via door-key mappings.
- **New MedKit category** — create custom medkits by cloning Salewa, IFAK, and AFAK templates. Control HP resource, healing cost, damage/health effects, and custom models.
- **Bundle injection supports medkits** — custom medkit loot and use-model bundles are loaded automatically like other item types.

## v1.1.0
- Custom containers can now be placed inside secure containers.
- Stims can use `EffectsDamage` to heal fractures, heavy/light bleeding, and blacked/destroyed limbs.
- Added `maxBodyPartsToHeal` so one stim can heal multiple limbs up to a set limit.
- Stim buff `value`, `delay`, and `duration` now support decimal numbers.
- `skillName` is only used when the buff type is `SkillRate`.
- Web tool fields added for `EffectsDamage`, `EffectsHealth`, `maxBodyPartsToHeal`, and decimal buff inputs.

## v1.0.0
- Initial release: quest items, keys, containers, stims, custom bundle injection, trader integration, and the web tool.
