import { z } from "zod";

interface ExtensibleVisibleFact {
  readonly [key: string]: unknown;
}

export interface PlayerVisibleEnchantment extends ExtensibleVisibleFact {
  readonly definition_id: string;
  readonly name?: string | null;
  readonly description?: string | null;
  readonly amount: number;
  readonly observation_source?: string;
}

export interface PlayerVisibleCard extends ExtensibleVisibleFact {
  readonly entity_id: string;
  readonly definition_id: string;
  readonly name?: string | null;
  readonly type: string;
  readonly cost: string;
  readonly star_cost?: string | null;
  readonly description?: string | null;
  readonly rarity: string;
  readonly is_upgraded: boolean;
  readonly is_selected: boolean;
  readonly existing_enchantment?: PlayerVisibleEnchantment | null;
  readonly target_type?: string | null;
  readonly can_play?: boolean | null;
  readonly unplayable_reason?: string | null;
}

export interface PlayerVisibleKeyword extends ExtensibleVisibleFact {
  readonly name: string;
  readonly description?: string | null;
}

export interface PlayerVisibleRelic extends ExtensibleVisibleFact {
  readonly entity_id: string;
  readonly definition_id: string;
  readonly name?: string | null;
  readonly description?: string | null;
  readonly counter?: number | null;
  readonly keywords: PlayerVisibleKeyword[];
  readonly card_previews: PlayerVisibleCard[];
}

export interface PlayerVisiblePotion extends ExtensibleVisibleFact {
  readonly entity_id: string;
  readonly definition_id: string;
  readonly name?: string | null;
  readonly description?: string | null;
  readonly slot: number;
  readonly keywords: PlayerVisibleKeyword[];
  readonly card_previews: PlayerVisibleCard[];
}

export interface PlayerPersistentVisibleState extends ExtensibleVisibleFact {
  readonly scope: "active_single_player_run";
  readonly run: ExtensibleVisibleFact & {
    readonly act: number;
    readonly act_definition_id: string;
    readonly act_name?: string | null;
    readonly floor: number;
    readonly ascension: number;
    readonly bosses: Array<ExtensibleVisibleFact & {
      readonly definition_id: string;
      readonly name?: string | null;
      readonly order: number;
    }>;
    readonly modifiers: Array<ExtensibleVisibleFact & {
      readonly definition_id: string;
      readonly name?: string | null;
      readonly description?: string | null;
      readonly keywords: PlayerVisibleKeyword[];
      readonly card_previews: PlayerVisibleCard[];
    }>;
  };
  readonly player: ExtensibleVisibleFact & {
    readonly entity_id: string;
    readonly character_definition_id: string;
    readonly character_name?: string | null;
    readonly hp: number;
    readonly max_hp: number;
    readonly gold: number;
    readonly relics: PlayerVisibleRelic[];
    readonly potions: PlayerVisiblePotion[];
    readonly max_potion_slots: number;
  };
  readonly completeness: ExtensibleVisibleFact & {
    readonly player_visible_semantics: string;
    readonly sources: string[];
    readonly missing: string[];
  };
}

export const visibleEnchantmentSchema: z.ZodType<PlayerVisibleEnchantment> = z.object({
  definition_id: z.string().min(1),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  amount: z.number().int(),
  observation_source: z.string().optional()
}).passthrough();

export const visibleCardSchema: z.ZodType<PlayerVisibleCard> = z.object({
  entity_id: z.string().min(1),
  definition_id: z.string().min(1),
  name: z.string().nullable().optional(),
  type: z.string(),
  cost: z.string(),
  star_cost: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  rarity: z.string(),
  is_upgraded: z.boolean(),
  is_selected: z.boolean(),
  existing_enchantment: visibleEnchantmentSchema.nullable().optional(),
  target_type: z.string().nullable().optional(),
  can_play: z.boolean().nullable().optional(),
  unplayable_reason: z.string().nullable().optional()
}).passthrough();

export const visibleKeywordSchema: z.ZodType<PlayerVisibleKeyword> = z.object({
  name: z.string().min(1),
  description: z.string().nullable().optional()
}).passthrough();

export const visibleRelicSchema: z.ZodType<PlayerVisibleRelic> = z.object({
  entity_id: z.string().min(1),
  definition_id: z.string().min(1),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  counter: z.number().nullable().optional(),
  keywords: z.array(visibleKeywordSchema),
  card_previews: z.array(visibleCardSchema)
}).passthrough();

export const visibleOwnedPotionSchema: z.ZodType<PlayerVisiblePotion> = z.object({
  entity_id: z.string().min(1),
  definition_id: z.string().min(1),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  slot: z.number().int().nonnegative(),
  keywords: z.array(visibleKeywordSchema),
  card_previews: z.array(visibleCardSchema)
}).passthrough();

export const persistentVisibleStateSchema: z.ZodType<PlayerPersistentVisibleState> = z.object({
  scope: z.literal("active_single_player_run"),
  run: z.object({
    act: z.number().int().positive(),
    act_definition_id: z.string().min(1),
    act_name: z.string().nullable().optional(),
    floor: z.number().int().nonnegative(),
    ascension: z.number().int().nonnegative(),
    bosses: z.array(z.object({
      definition_id: z.string().min(1),
      name: z.string().nullable().optional(),
      order: z.number().int().nonnegative()
    }).passthrough()),
    modifiers: z.array(z.object({
      definition_id: z.string().min(1),
      name: z.string().nullable().optional(),
      description: z.string().nullable().optional(),
      keywords: z.array(visibleKeywordSchema),
      card_previews: z.array(visibleCardSchema)
    }).passthrough())
  }).passthrough(),
  player: z.object({
    entity_id: z.string().min(1),
    character_definition_id: z.string().min(1),
    character_name: z.string().nullable().optional(),
    hp: z.number(),
    max_hp: z.number(),
    gold: z.number().int().nonnegative(),
    relics: z.array(visibleRelicSchema),
    potions: z.array(visibleOwnedPotionSchema),
    max_potion_slots: z.number().int().nonnegative()
  }).passthrough(),
  completeness: z.object({
    player_visible_semantics: z.string().min(1),
    sources: z.array(z.string().min(1)),
    missing: z.array(z.string().min(1))
  }).passthrough()
}).passthrough();
