import { EffectsHealthProperties, EffectsDamageProperties } from './types'

export interface MedKitTemplate {
  id: string
  name: string
  displayName: string
  parent: string
  handbookParentId: string
  backgroundColor: string
  weight: number
  width: number
  height: number
  stackMaxSize: number
  itemSound: string
  medEffectType: string
  medUseTime: number
  maxHpResource: number
  hpResourceRate: number
  prefab: string
  usePrefab: string
  effectsHealth: Record<string, EffectsHealthProperties>
  effectsDamage: Record<string, EffectsDamageProperties>
  properties: Record<string, any>
}

export const MEDKIT_TEMPLATES: MedKitTemplate[] = [
  {
    "id": "544fb45d4bdc2dee738b4568",
    "name": "Salewa first aid kit",
    "displayName": "Salewa first aid kit",
    "parent": "5448f39d4bdc2d0a728b4568",
    "handbookParentId": "5b47574386f77428ca22b338",
    "backgroundColor": "orange",
    "weight": 0.6,
    "width": 1,
    "height": 2,
    "stackMaxSize": 1,
    "itemSound": "med_medkit",
    "medEffectType": "duringUse",
    "medUseTime": 3,
    "maxHpResource": 400,
    "hpResourceRate": 85,
    "prefab": "assets/content/weapons/usable_items/item_meds_salewa/item_meds_salewa_loot.bundle",
    "usePrefab": "assets/content/weapons/usable_items/item_meds_salewa/item_meds_salewa_container.bundle",
    "effectsHealth": {},
    "effectsDamage": {
      "HeavyBleeding": {
        "delay": 0,
        "duration": 0,
        "fadeOut": 0,
        "cost": 175
      },
      "LightBleeding": {
        "delay": 0,
        "duration": 0,
        "fadeOut": 0,
        "cost": 45
      }
    },
    "properties": {
      "AnimationVariantsNumber": 2,
      "BackgroundColor": "orange",
      "BodyPartPriority": [],
      "CanRequireOnRagfair": false,
      "CanSellOnRagfair": true,
      "ConflictingItems": [],
      "Description": "",
      "DiscardLimit": -1,
      "DiscardingBlock": false,
      "DropSoundType": "None",
      "ExamineExperience": 6,
      "ExamineTime": 1,
      "ExaminedByDefault": true,
      "ExtraSizeDown": 0,
      "ExtraSizeForceAdd": false,
      "ExtraSizeLeft": 0,
      "ExtraSizeRight": 0,
      "ExtraSizeUp": 0,
      "Height": 2,
      "HideEntrails": false,
      "InsuranceDisabled": false,
      "IsAlwaysAvailableForInsurance": false,
      "IsLockedafterEquip": false,
      "IsSecretExitRequirement": false,
      "IsSpecialSlotOnly": false,
      "IsUnbuyable": false,
      "IsUndiscardable": false,
      "IsUngivable": false,
      "IsUnremovable": false,
      "IsUnsaleable": false,
      "ItemSound": "med_medkit",
      "LeftHandItem": false,
      "LootExperience": 10,
      "MaxHpResource": 400,
      "MergesWithChildren": false,
      "MetascoreGroup": "Utility",
      "Name": "Аптечка Salewa FIRST AID KIT",
      "NotShownInSlot": false,
      "Prefab": {
        "path": "",
        "rcid": ""
      },
      "QuestItem": false,
      "QuestStashMaxCount": 0,
      "RagFairCommissionModifier": 1,
      "RarityPvE": "Rare",
      "RepairCost": 0,
      "RepairSpeed": 0,
      "ShortName": "Salewa",
      "StackMaxSize": 1,
      "StackObjectsCount": 1,
      "StimulatorBuffs": "",
      "Unlootable": false,
      "UnlootableFromSide": [],
      "UnlootableFromSlot": "FirstPrimaryWeapon",
      "UsePrefab": {
        "path": "",
        "rcid": ""
      },
      "Weight": 0.6,
      "Width": 1,
      "effects_damage": {},
      "effects_health": {},
      "hpResourceRate": 85,
      "medEffectType": "duringUse",
      "medUseTime": 3,
      "foodEffectType": ""
    }
  },
  {
    "id": "590c678286f77426c9660122",
    "name": "IFAK individual first aid kit",
    "displayName": "IFAK individual first aid kit",
    "parent": "5448f39d4bdc2d0a728b4568",
    "handbookParentId": "5b47574386f77428ca22b338",
    "backgroundColor": "orange",
    "weight": 0.5,
    "width": 1,
    "height": 1,
    "stackMaxSize": 1,
    "itemSound": "med_medkit",
    "medEffectType": "duringUse",
    "medUseTime": 3,
    "maxHpResource": 300,
    "hpResourceRate": 50,
    "prefab": "assets/content/weapons/usable_items/item_ifak/item_ifak_loot.bundle",
    "usePrefab": "assets/content/weapons/usable_items/item_ifak/item_ifak_container.bundle",
    "effectsHealth": {},
    "effectsDamage": {
      "HeavyBleeding": {
        "delay": 0,
        "duration": 0,
        "fadeOut": 0,
        "cost": 210,
        "healthPenaltyMin": 0,
        "healthPenaltyMax": 0
      },
      "LightBleeding": {
        "delay": 0,
        "duration": 0,
        "fadeOut": 0,
        "cost": 30,
        "healthPenaltyMin": 0,
        "healthPenaltyMax": 0
      },
      "RadExposure": {
        "delay": 0,
        "duration": 0,
        "fadeOut": 0,
        "cost": 0,
        "healthPenaltyMin": 0,
        "healthPenaltyMax": 0
      }
    },
    "properties": {
      "AnimationVariantsNumber": 2,
      "BackgroundColor": "orange",
      "BodyPartPriority": [],
      "CanRequireOnRagfair": false,
      "CanSellOnRagfair": true,
      "ConflictingItems": [],
      "Description": "ifak",
      "DiscardLimit": -1,
      "DiscardingBlock": false,
      "DropSoundType": "None",
      "ExamineExperience": 10,
      "ExamineTime": 1,
      "ExaminedByDefault": true,
      "ExtraSizeDown": 0,
      "ExtraSizeForceAdd": false,
      "ExtraSizeLeft": 0,
      "ExtraSizeRight": 0,
      "ExtraSizeUp": 0,
      "Height": 1,
      "HideEntrails": false,
      "InsuranceDisabled": false,
      "IsAlwaysAvailableForInsurance": false,
      "IsLockedafterEquip": false,
      "IsSecretExitRequirement": false,
      "IsSpecialSlotOnly": false,
      "IsUnbuyable": false,
      "IsUndiscardable": false,
      "IsUngivable": false,
      "IsUnremovable": false,
      "IsUnsaleable": false,
      "ItemSound": "med_medkit",
      "LeftHandItem": false,
      "LootExperience": 10,
      "MaxHpResource": 300,
      "MergesWithChildren": false,
      "MetascoreGroup": "Utility",
      "Name": "ifak",
      "NotShownInSlot": false,
      "Prefab": {
        "path": "",
        "rcid": ""
      },
      "QuestItem": false,
      "QuestStashMaxCount": 0,
      "RagFairCommissionModifier": 1,
      "RarityPvE": "Rare",
      "RepairCost": 0,
      "RepairSpeed": 0,
      "ShortName": "ifak",
      "StackMaxSize": 1,
      "StackObjectsCount": 1,
      "StimulatorBuffs": "",
      "Unlootable": false,
      "UnlootableFromSide": [],
      "UnlootableFromSlot": "FirstPrimaryWeapon",
      "UsePrefab": {
        "path": "",
        "rcid": ""
      },
      "Weight": 0.5,
      "Width": 1,
      "effects_damage": {},
      "effects_health": {},
      "hpResourceRate": 50,
      "medEffectType": "duringUse",
      "medUseTime": 3,
      "foodEffectType": ""
    }
  },
  {
    "id": "60098ad7c2240c0fe85c570a",
    "name": "AFAK tactical individual first aid kit",
    "displayName": "AFAK tactical individual first aid kit",
    "parent": "5448f39d4bdc2d0a728b4568",
    "handbookParentId": "5b47574386f77428ca22b338",
    "backgroundColor": "orange",
    "weight": 0.8,
    "width": 1,
    "height": 1,
    "stackMaxSize": 1,
    "itemSound": "med_medkit",
    "medEffectType": "duringUse",
    "medUseTime": 3,
    "maxHpResource": 400,
    "hpResourceRate": 60,
    "prefab": "assets/content/weapons/usable_items/item_meds_afak/item_meds_afak_loot.bundle",
    "usePrefab": "assets/content/weapons/usable_items/item_meds_afak/item_meds_afak_container.bundle",
    "effectsHealth": {},
    "effectsDamage": {
      "HeavyBleeding": {
        "delay": 0,
        "duration": 0,
        "fadeOut": 0,
        "cost": 170,
        "healthPenaltyMin": 0,
        "healthPenaltyMax": 0
      },
      "LightBleeding": {
        "delay": 0,
        "duration": 0,
        "fadeOut": 0,
        "cost": 30,
        "healthPenaltyMin": 0,
        "healthPenaltyMax": 0
      },
      "RadExposure": {
        "delay": 0,
        "duration": 0,
        "fadeOut": 0,
        "cost": 0,
        "healthPenaltyMin": 0,
        "healthPenaltyMax": 0
      }
    },
    "properties": {
      "AnimationVariantsNumber": 2,
      "BackgroundColor": "orange",
      "BodyPartPriority": [],
      "CanRequireOnRagfair": false,
      "CanSellOnRagfair": true,
      "ConflictingItems": [],
      "Description": "ifak",
      "DiscardLimit": -1,
      "DiscardingBlock": false,
      "DropSoundType": "None",
      "ExamineExperience": 10,
      "ExamineTime": 1,
      "ExaminedByDefault": true,
      "ExtraSizeDown": 0,
      "ExtraSizeForceAdd": false,
      "ExtraSizeLeft": 0,
      "ExtraSizeRight": 0,
      "ExtraSizeUp": 0,
      "Height": 1,
      "HideEntrails": false,
      "InsuranceDisabled": false,
      "IsAlwaysAvailableForInsurance": false,
      "IsLockedafterEquip": false,
      "IsSecretExitRequirement": false,
      "IsSpecialSlotOnly": false,
      "IsUnbuyable": false,
      "IsUndiscardable": false,
      "IsUngivable": false,
      "IsUnremovable": false,
      "IsUnsaleable": false,
      "ItemSound": "med_medkit",
      "LeftHandItem": false,
      "LootExperience": 10,
      "MaxHpResource": 400,
      "MergesWithChildren": false,
      "MetascoreGroup": "Utility",
      "Name": "ifak",
      "NotShownInSlot": false,
      "Prefab": {
        "path": "",
        "rcid": ""
      },
      "QuestItem": false,
      "QuestStashMaxCount": 0,
      "RagFairCommissionModifier": 1,
      "RarityPvE": "Superrare",
      "RepairCost": 0,
      "RepairSpeed": 0,
      "ShortName": "ifak",
      "StackMaxSize": 1,
      "StackObjectsCount": 1,
      "StimulatorBuffs": "",
      "Unlootable": false,
      "UnlootableFromSide": [],
      "UnlootableFromSlot": "FirstPrimaryWeapon",
      "UsePrefab": {
        "path": "",
        "rcid": ""
      },
      "Weight": 0.8,
      "Width": 1,
      "effects_damage": {},
      "effects_health": {},
      "hpResourceRate": 60,
      "medEffectType": "duringUse",
      "medUseTime": 3,
      "foodEffectType": ""
    }
  }
]
