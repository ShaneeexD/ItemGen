# Changelog

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
