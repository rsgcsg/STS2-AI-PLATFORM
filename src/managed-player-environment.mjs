import { createHash } from "node:crypto";
import {
  SUPPORTED_PLAYER_ENVIRONMENT_PROTOCOL,
  decodePlayerRead,
  decodePlayerReceipt,
  decodePlayerSnapshot
} from "@rsgcsg/sts2-connector-client";
import { startManagedCandidateRuntime } from "./managed-candidate.mjs";

const ACTION_LIMIT = 512;
const INFORMATION_POLICY = Object.freeze({
  id: "player_visible_v1",
  scope: "Information currently presented by, or normally inspectable through, the local player's game UI.",
  includes_hidden_information: false,
  unknown_field_behavior: "omit_and_mark_incomplete"
});

function plainObject(value) {
  return value != null && typeof value === "object" && !Array.isArray(value);
}

function canonicalValue(value) {
  if (Array.isArray(value)) return value.map(canonicalValue);
  if (!plainObject(value)) return value;
  return Object.fromEntries(Object.keys(value).sort()
    .map((key) => [key, canonicalValue(value[key])]));
}

function digest(value) {
  return createHash("sha256").update(JSON.stringify(canonicalValue(value))).digest("hex");
}

function stableId(prefix, ...parts) {
  return `${prefix}_${digest(parts).slice(0, 24)}`;
}

function definitionId(value) {
  const text = String(value ?? "UNKNOWN");
  const entry = text.includes(".") ? text.slice(text.lastIndexOf(".") + 1) : text;
  return entry.trim().replace(/[^a-z0-9]+/giu, "_").replace(/^_+|_+$/gu, "").toUpperCase() || "UNKNOWN";
}

function visibleState({ enabled = true, selected = null } = {}) {
  return {
    visible: true,
    enabled,
    ...(selected == null ? {} : { selected }),
    observation_basis: "native_visible_fact"
  };
}

function createProjectionContext({ state, runtimeInstanceId, sequence }) {
  const rawStateSha = digest(state);
  const snapshotId = `managed_${runtimeInstanceId}_${rawStateSha}`;
  const interactionId = `interaction_${rawStateSha}`;
  const referents = [];
  const actions = [];
  const bindings = new Map();

  function referent({ role, label, properties, enabled = true, selected = null, occurrence = 0 }) {
    const referentId = stableId("ref", snapshotId, role, properties, occurrence);
    const value = {
      referent_id: referentId,
      role,
      kind: "entity",
      label: label == null ? null : String(label),
      state: visibleState({ enabled, selected }),
      properties_schema: `sts2.player-environment/referent/${role.replace(/[^a-z0-9_]+/giu, "_").toLowerCase()}-1`,
      properties: { ...properties, entity_id: referentId }
    };
    referents.push(value);
    return value;
  }

  function action({ verb, subject = null, arguments: actionArguments = [], label, raw }) {
    const descriptor = {
      verb,
      subject_referent_id: subject?.referent_id ?? null,
      arguments: actionArguments.map(({ role, referent }) => ({
        role,
        referent_id: referent.referent_id
      })),
      label
    };
    const boundActionId = stableId("action", snapshotId, descriptor, raw);
    const value = {
      bound_action_id: boundActionId,
      verb,
      interaction_id: interactionId,
      subject_referent_id: descriptor.subject_referent_id,
      arguments: descriptor.arguments,
      label
    };
    actions.push(value);
    bindings.set(boundActionId, Object.freeze({
      expected_raw_state_sha256: rawStateSha,
      raw_request: canonicalValue(raw),
      action: value
    }));
    return value;
  }

  return {
    state,
    sequence,
    rawStateSha,
    snapshotId,
    interactionId,
    referents,
    actions,
    bindings,
    referent,
    action
  };
}

function withoutDeck(player) {
  if (!plainObject(player)) return null;
  const { deck: _deck, native_ref: _nativeRef, potions = [], ...visible } = player;
  return {
    ...visible,
    potions: potions.map((potion) => {
      const {
        native_ref: _potionNativeRef,
        valid_target_refs: _validTargetRefs,
        binding_supported: _bindingSupported,
        ...publicPotion
      } = potion;
      return publicPotion;
    })
  };
}

function cardProperties(card) {
  return {
    definition_id: definitionId(card?.id ?? card?.name),
    name: card?.name ?? null,
    type: card?.type ?? "Unknown",
    cost: String(card?.cost ?? "?"),
    rarity: card?.rarity ?? "Unknown",
    is_upgraded: card?.upgraded === true || card?.is_upgraded === true,
    description: card?.description ?? null,
    target_type: card?.target_type ?? null,
    can_play: card?.can_play ?? null,
    stats: card?.stats ?? null,
    keywords: card?.keywords ?? null,
    after_upgrade: card?.after_upgrade ?? null
  };
}

function currentSurface(state, ctx) {
  const commonContext = {
    kind: "run",
    run: state.context ?? null,
    player: withoutDeck(state.player)
  };
  const supported = { complete: true, missing: [] };

  switch (state.decision) {
    case "map_select": {
      const nativeIdentityComplete = (state.choices ?? []).every((choice) =>
        typeof choice.native_ref === "string" && choice.native_ref.length > 0);
      const choices = (state.choices ?? []).map((choice, index) => {
        const node = ctx.referent({
          role: "map_node",
          label: `${choice.type ?? "node"} (${choice.col},${choice.row})`,
          occurrence: index,
          properties: {
            col: choice.col,
            row: choice.row,
            point_type: String(choice.type ?? "unknown").toLowerCase(),
            state: "travelable"
          }
        });
        ctx.action({
          verb: "activate",
          subject: node,
          label: `Travel to ${choice.type ?? "node"} (${choice.col},${choice.row})`,
          raw: {
            cmd: "action",
            action: "select_map_node",
            args: { col: choice.col, row: choice.row, map_point_ref: choice.native_ref }
          }
        });
        return node.properties;
      });
      return {
        kind: "map_navigation",
        stage: "ready",
        prompt: null,
        surface: { kind: "map_navigation", stage: "ready", choices },
        context: { ...commonContext, kind: "map" },
        complete: nativeIdentityComplete,
        missing: [
          "full_visible_map_topology",
          ...(nativeIdentityComplete ? [] : ["native_map_point_identity"])
        ]
      };
    }
    case "event_choice": {
      const options = (state.options ?? []).map((option, index) => {
        const unlocked = option.is_locked !== true;
        const item = ctx.referent({
          role: "option",
          label: option.title ?? `Option ${index + 1}`,
          enabled: unlocked,
          occurrence: index,
          properties: {
            index: option.index,
            title: option.title ?? null,
            description: option.description ?? null,
            text_key: option.text_key ?? null,
            is_locked: !unlocked,
            variables: option.vars ?? null
          }
        });
        if (unlocked) {
          ctx.action({
            verb: "activate",
            subject: item,
            label: String(option.title ?? `Choose option ${index + 1}`),
            raw: { cmd: "action", action: "choose_option", args: { option_index: option.index } }
          });
        }
        return item.properties;
      });
      return {
        kind: "event_option",
        stage: "choosing",
        prompt: state.description ?? null,
        surface: {
          kind: "event_option",
          stage: "choosing",
          title: state.event_name ?? null,
          description: state.description ?? null,
          options
        },
        context: { ...commonContext, kind: "event" },
        ...supported
      };
    }
    case "rest_site": {
      const options = (state.options ?? []).map((option, index) => {
        const enabled = option.is_enabled !== false;
        const item = ctx.referent({
          role: "rest_option",
          label: option.name ?? option.option_id ?? `Option ${index + 1}`,
          enabled,
          occurrence: index,
          properties: {
            index: option.index,
            option_id: option.option_id ?? null,
            name: option.name ?? null,
            is_enabled: enabled
          }
        });
        if (enabled) {
          ctx.action({
            verb: "activate",
            subject: item,
            label: String(option.name ?? option.option_id ?? `Choose option ${index + 1}`),
            raw: { cmd: "action", action: "choose_option", args: { option_index: option.index } }
          });
        }
        return item.properties;
      });
      return {
        kind: "rest_site",
        stage: "choosing",
        prompt: null,
        surface: { kind: "rest_site", stage: "choosing", options },
        context: { ...commonContext, kind: "rest" },
        ...supported,
        missing: ["localized_rest_option_descriptions"]
      };
    }
    case "treasure_chest": {
      const room = ctx.referent({
        role: "treasure_room",
        label: "Treasure chest",
        properties: { stage: "closed" }
      });
      ctx.action({
        verb: "activate",
        subject: room,
        label: "Open treasure chest",
        raw: { cmd: "action", action: "open_treasure", args: { room_ref: state.room_ref } }
      });
      const complete = typeof state.room_ref === "string" && state.room_ref.length > 0;
      return {
        kind: "treasure_chest",
        stage: "closed",
        prompt: null,
        surface: { kind: "treasure_chest", stage: "closed" },
        context: { ...commonContext, kind: "treasure" },
        complete,
        missing: complete ? [] : ["native_treasure_room_identity"]
      };
    }
    case "treasure_relic": {
      const room = ctx.referent({
        role: "treasure_room",
        label: "Opened treasure chest",
        properties: { stage: "relic_selection" }
      });
      const nativeIdentityComplete = (state.relics ?? []).every((relic) =>
        typeof relic.native_ref === "string" && relic.native_ref.length > 0);
      const relics = (state.relics ?? []).map((relic, index) => {
        const item = ctx.referent({
          role: "relic",
          label: relic.name ?? `Relic ${index + 1}`,
          occurrence: index,
          properties: {
            index: relic.index ?? index,
            definition_id: definitionId(relic.id ?? relic.name),
            name: relic.name ?? null,
            description: relic.description ?? null,
            rarity: relic.rarity ?? null
          }
        });
        ctx.action({
          verb: "select",
          subject: item,
          label: `Take ${relic.name ?? `relic ${index + 1}`}`,
          raw: {
            cmd: "action",
            action: "select_treasure_relic",
            args: { relic_ref: relic.native_ref }
          }
        });
        return item.properties;
      });
      if (state.can_skip === true) {
        ctx.action({
          verb: "skip",
          subject: room,
          label: "Skip treasure relic",
          raw: {
            cmd: "action",
            action: "skip_treasure_relic",
            args: { room_ref: state.room_ref }
          }
        });
      }
      const roomIdentityComplete = typeof state.room_ref === "string" && state.room_ref.length > 0;
      const complete = roomIdentityComplete && nativeIdentityComplete && relics.length > 0;
      return {
        kind: "treasure_relic_selection",
        stage: "choosing",
        prompt: null,
        surface: {
          kind: "treasure_relic_selection",
          stage: "choosing",
          relics,
          can_skip: state.can_skip === true
        },
        context: { ...commonContext, kind: "treasure" },
        complete,
        missing: [
          ...(roomIdentityComplete ? [] : ["native_treasure_room_identity"]),
          ...(nativeIdentityComplete ? [] : ["native_treasure_relic_identity"]),
          ...(relics.length > 0 ? [] : ["visible_treasure_relics"])
        ]
      };
    }
    case "treasure_complete": {
      const room = ctx.referent({
        role: "treasure_room",
        label: "Completed treasure room",
        properties: { stage: "complete" }
      });
      ctx.action({
        verb: "close",
        subject: room,
        label: "Proceed to map",
        raw: { cmd: "action", action: "leave_room", args: { room_ref: state.room_ref } }
      });
      const complete = typeof state.room_ref === "string" && state.room_ref.length > 0;
      return {
        kind: "treasure_completion",
        stage: "ready",
        prompt: null,
        surface: { kind: "treasure_completion", stage: "ready" },
        context: { ...commonContext, kind: "treasure" },
        complete,
        missing: complete ? [] : ["native_treasure_room_identity"]
      };
    }
    case "reward_set": {
      const nativeIdentityComplete = (state.rewards ?? []).every((reward) =>
        typeof reward.native_ref === "string" && reward.native_ref.length > 0);
      const rewards = (state.rewards ?? []).map((reward, index) => {
        const item = ctx.referent({
          role: "reward",
          label: reward.name ?? reward.kind ?? `Reward ${index + 1}`,
          occurrence: index,
          properties: {
            index: reward.index ?? index,
            reward_kind: reward.kind ?? "unknown",
            name: reward.name ?? null,
            description: reward.description ?? null,
            value: reward.value ?? null
          }
        });
        ctx.action({
          verb: "activate",
          subject: item,
          label: `Take ${reward.name ?? reward.kind ?? `reward ${index + 1}`}`,
          raw: {
            cmd: "action",
            action: "select_reward",
            args: { reward_ref: reward.native_ref }
          }
        });
        return item.properties;
      });
      if (state.is_terminal === true && state.can_proceed === true) {
        const room = ctx.referent({
          role: "combat_room",
          label: state.is_boss === true ? "Completed boss combat" : "Completed combat",
          properties: {
            room_type: state.is_boss === true ? "boss" : "combat",
            rewards_complete: rewards.length === 0
          }
        });
        ctx.action({
          verb: "activate",
          subject: room,
          label: rewards.length === 0 ? "Proceed" : "Proceed and skip remaining rewards",
          raw: { cmd: "action", action: "proceed", args: { room_ref: state.room_ref } }
        });
      } else if (state.is_terminal !== true && state.can_skip === true) {
        ctx.action({
          verb: "skip",
          label: "Skip remaining rewards",
          raw: { cmd: "action", action: "skip_rewards" }
        });
      }
      const terminalIdentityComplete = state.is_terminal !== true
        || (typeof state.room_ref === "string" && state.room_ref.length > 0);
      return {
        kind: "reward_collection",
        stage: "choosing",
        prompt: null,
        surface: {
          kind: "reward_collection",
          stage: "choosing",
          rewards,
          can_skip: state.can_skip === true,
          is_terminal: state.is_terminal === true,
          can_proceed: state.can_proceed === true
        },
        context: { ...commonContext, kind: "reward" },
        complete: nativeIdentityComplete && terminalIdentityComplete,
        missing: [
          ...(nativeIdentityComplete ? [] : ["native_reward_identity"]),
          ...(terminalIdentityComplete ? [] : ["terminal_reward_room_identity"])
        ]
      };
    }
    case "card_reward": {
      const cards = (state.cards ?? []).map((card, index) => {
        const item = ctx.referent({
          role: "card",
          label: card.name ?? `Card ${index + 1}`,
          occurrence: index,
          properties: { index: card.index ?? index, ...cardProperties(card) }
        });
        ctx.action({
          verb: "select",
          subject: item,
          label: `Take ${card.name ?? `card ${index + 1}`}`,
          raw: { cmd: "action", action: "select_card_reward", args: { card_index: card.index ?? index } }
        });
        return item.properties;
      });
      if (state.can_skip === true) {
        ctx.action({
          verb: "skip",
          label: "Skip card reward",
          raw: { cmd: "action", action: "skip_card_reward" }
        });
      }
      return {
        kind: "card_reward_selection",
        stage: "choosing",
        prompt: null,
        surface: { kind: "card_reward_selection", stage: "choosing", cards, can_skip: state.can_skip === true },
        context: { ...commonContext, kind: "reward" },
        ...supported
      };
    }
    case "combat_rewards_complete": {
      const room = ctx.referent({
        role: "combat_room",
        label: state.is_boss === true ? "Completed boss combat" : "Completed combat",
        properties: {
          room_type: state.is_boss === true ? "boss" : "combat",
          rewards_complete: true
        }
      });
      ctx.action({
        verb: "activate",
        subject: room,
        label: state.is_boss === true ? "Proceed from boss rewards" : "Proceed to map",
        raw: { cmd: "action", action: "proceed", args: { room_ref: state.room_ref } }
      });
      return {
        kind: "reward_completion",
        stage: "ready",
        prompt: null,
        surface: {
          kind: "reward_completion",
          stage: "ready",
          is_boss: state.is_boss === true
        },
        context: { ...commonContext, kind: "reward" },
        complete: typeof state.room_ref === "string",
        missing: typeof state.room_ref === "string" ? [] : ["native_combat_room_identity"]
      };
    }
    case "bundle_select": {
      const bundles = (state.bundles ?? []).map((bundle, index) => {
        const item = ctx.referent({
          role: "card_bundle",
          label: `Bundle ${index + 1}`,
          occurrence: index,
          properties: {
            index: bundle.index ?? index,
            cards: (bundle.cards ?? []).map(cardProperties)
          }
        });
        ctx.action({
          verb: "select",
          subject: item,
          label: `Take bundle ${index + 1}`,
          raw: { cmd: "action", action: "select_bundle", args: { bundle_index: bundle.index ?? index } }
        });
        return item.properties;
      });
      return {
        kind: "card_bundle_selection",
        stage: "choosing",
        prompt: null,
        surface: { kind: "card_bundle_selection", stage: "choosing", bundles },
        context: { ...commonContext, kind: "selection" },
        ...supported
      };
    }
    case "card_select": {
      const cardReferents = (state.cards ?? []).map((card, index) => ctx.referent({
        role: "card",
        label: card.name ?? `Card ${index + 1}`,
        occurrence: index,
        properties: { index: card.index ?? index, ...cardProperties(card) }
      }));
      const min = Number.isSafeInteger(state.min_select) ? state.min_select : 1;
      const max = Number.isSafeInteger(state.max_select) ? state.max_select : min;
      const selections = enumerateSelections(cardReferents, min, max, ACTION_LIMIT + 1);
      if (selections.length > ACTION_LIMIT) {
        return {
          kind: "card_selection",
          stage: "choosing",
          prompt: null,
          surface: { kind: "card_selection", stage: "choosing", cards: cardReferents.map((item) => item.properties), min_select: min, max_select: max },
          context: { ...commonContext, kind: "selection" },
          complete: false,
          missing: ["finite_selection_projection_exceeds_limit"],
          totalCount: selections.length
        };
      }
      for (const selected of selections) {
        if (selected.length === 0) {
          ctx.action({ verb: "skip", label: "Skip selection", raw: { cmd: "action", action: "skip_select" } });
        } else {
          ctx.action({
            verb: "confirm",
            arguments: selected.map((referent) => ({ role: "selected_card", referent })),
            label: `Confirm ${selected.map((item) => item.label).join(", ")}`,
            raw: {
              cmd: "action",
              action: "select_cards",
              args: { indices: selected.map((item) => item.properties.index).join(",") }
            }
          });
        }
      }
      return {
        kind: "card_selection",
        stage: "choosing",
        prompt: null,
        surface: { kind: "card_selection", stage: "choosing", cards: cardReferents.map((item) => item.properties), min_select: min, max_select: max },
        context: { ...commonContext, kind: "selection" },
        ...supported
      };
    }
    case "combat_play": {
      const rawHand = state.hand ?? [];
      const rawEnemies = state.enemies ?? [];
      const rawPotions = state.player?.potions ?? [];
      const identityComplete = typeof state.player?.native_ref === "string"
        && [...rawHand, ...rawEnemies, ...rawPotions].every((item) =>
          typeof item?.native_ref === "string" && item.native_ref.length > 0);
      const unsupportedCardTarget = rawHand.some((card) =>
        card.can_play === true && card.target_type === "AnyAlly");
      const unsupportedPotionTarget = rawPotions.some((potion) =>
        potion.can_use === true && potion.binding_supported !== true);

      const handEntries = rawHand.map((card, index) => ({
        raw: card,
        referent: ctx.referent({
          role: "card",
          label: card.name ?? `Card ${index + 1}`,
          enabled: card.can_play === true,
          occurrence: index,
          properties: { index: card.index ?? index, ...cardProperties(card) }
        })
      }));
      const enemyEntries = rawEnemies.map((enemy, index) => {
        const { native_ref: _nativeRef, ...visibleEnemy } = enemy;
        return {
          raw: enemy,
          referent: ctx.referent({
            role: "enemy",
            label: enemy.name ?? `Enemy ${index + 1}`,
            enabled: (enemy.hp ?? 0) > 0,
            occurrence: index,
            properties: { ...visibleEnemy, index: enemy.index ?? index }
          })
        };
      });

      for (const { raw: card, referent } of handEntries.filter(({ referent: item }) =>
        item.state.enabled === true)) {
        if (card.target_type === "AnyEnemy") {
          const validTargets = new Set(card.valid_target_refs ?? []);
          for (const { raw: enemy, referent: target } of enemyEntries.filter(({ referent: item }) =>
            item.state.enabled === true)) {
            if (!validTargets.has(enemy.native_ref)) continue;
            ctx.action({
              verb: "play",
              subject: referent,
              arguments: [{ role: "target", referent: target }],
              label: `Play ${referent.label} on ${target.label}`,
              raw: {
                cmd: "action",
                action: "play_card",
                args: { card_ref: card.native_ref, target_ref: enemy.native_ref }
              }
            });
          }
        } else if (card.target_type !== "AnyAlly") {
          ctx.action({
            verb: "play",
            subject: referent,
            label: `Play ${referent.label}`,
            raw: { cmd: "action", action: "play_card", args: { card_ref: card.native_ref } }
          });
        }
      }

      ctx.action({
        verb: "end_turn",
        label: "End turn",
        raw: { cmd: "action", action: "end_turn", args: { player_ref: state.player?.native_ref } }
      });

      for (const [index, potion] of rawPotions.entries()) {
        const {
          native_ref: _nativeRef,
          valid_target_refs: _validTargetRefs,
          binding_supported: _bindingSupported,
          ...visiblePotion
        } = potion;
        const potionReferent = ctx.referent({
          role: "potion",
          label: potion.name ?? `Potion ${index + 1}`,
          enabled: potion.can_use === true || potion.can_discard === true,
          occurrence: index,
          properties: visiblePotion
        });
        if (potion.can_use === true && potion.binding_supported === true) {
          if (potion.target_type === "AnyEnemy") {
            const validTargets = new Set(potion.valid_target_refs ?? []);
            for (const { raw: enemy, referent: target } of enemyEntries) {
              if (!validTargets.has(enemy.native_ref)) continue;
              ctx.action({
                verb: "use",
                subject: potionReferent,
                arguments: [{ role: "target", referent: target }],
                label: `Use ${potionReferent.label} on ${target.label}`,
                raw: {
                  cmd: "action",
                  action: "use_potion",
                  args: {
                    potion_slot: potion.slot,
                    potion_ref: potion.native_ref,
                    target_ref: enemy.native_ref
                  }
                }
              });
            }
          } else {
            ctx.action({
              verb: "use",
              subject: potionReferent,
              label: `Use ${potionReferent.label}`,
              raw: {
                cmd: "action",
                action: "use_potion",
                args: { potion_slot: potion.slot, potion_ref: potion.native_ref }
              }
            });
          }
        }
        if (potion.can_discard === true) {
          ctx.action({
            verb: "activate",
            subject: potionReferent,
            label: `Discard ${potionReferent.label}`,
            raw: {
              cmd: "action",
              action: "discard_potion",
              args: { potion_slot: potion.slot, potion_ref: potion.native_ref }
            }
          });
        }
      }

      const complete = identityComplete && !unsupportedCardTarget && !unsupportedPotionTarget;
      return {
        kind: "combat_turn",
        stage: "ready",
        prompt: null,
        surface: { kind: "combat_turn", stage: "ready" },
        context: {
          ...commonContext,
          kind: "combat",
          round: state.round ?? null,
          energy: state.energy ?? null,
          max_energy: state.max_energy ?? null,
          hand: handEntries.map(({ referent }) => referent.properties),
          enemies: enemyEntries.map(({ referent }) => referent.properties),
          player_powers: state.player_powers ?? null,
          draw_pile_count: state.draw_pile_count ?? null,
          discard_pile_count: state.discard_pile_count ?? null
        },
        complete,
        missing: [
          ...(identityComplete ? [] : ["native_combat_operand_identity"]),
          ...(unsupportedCardTarget ? ["native_any_ally_card_targeting"] : []),
          ...(unsupportedPotionTarget ? ["native_potion_target_binding"] : [])
        ]
      };
    }
    case "game_over":
      return {
        kind: "game_over",
        stage: "complete",
        prompt: null,
        surface: { kind: "game_over", stage: "complete", victory: state.victory === true },
        context: { ...commonContext, kind: "terminal" },
        ...supported
      };
    case "shop": {
      const cards = (state.cards ?? []).map((card, index) => {
        const canPurchase = card.can_purchase === true;
        const item = ctx.referent({
          role: "shop_card",
          label: card.name ?? `Card ${index + 1}`,
          enabled: canPurchase,
          occurrence: index,
          properties: {
            ...cardProperties({ ...card, cost: card.card_cost }),
            price: card.cost ?? null,
            is_stocked: card.is_stocked === true,
            can_purchase: canPurchase,
            on_sale: card.on_sale === true
          }
        });
        if (canPurchase && typeof card.native_ref === "string") {
          ctx.action({
            verb: "activate",
            subject: item,
            label: `Buy ${item.label}`,
            raw: { cmd: "action", action: "buy_card", args: { entry_ref: card.native_ref } }
          });
        }
        return item.properties;
      });
      const relics = (state.relics ?? []).map((relic, index) => {
        const canPurchase = relic.can_purchase === true;
        const item = ctx.referent({
          role: "shop_relic",
          label: relic.name ?? `Relic ${index + 1}`,
          enabled: canPurchase,
          occurrence: index,
          properties: {
            name: relic.name ?? null,
            description: relic.description ?? null,
            price: relic.cost ?? null,
            is_stocked: relic.is_stocked === true,
            can_purchase: canPurchase
          }
        });
        if (canPurchase && typeof relic.native_ref === "string") {
          ctx.action({
            verb: "activate",
            subject: item,
            label: `Buy ${item.label}`,
            raw: { cmd: "action", action: "buy_relic", args: { entry_ref: relic.native_ref } }
          });
        }
        return item.properties;
      });
      const potions = (state.potions ?? []).map((potion, index) => {
        const canPurchase = potion.can_purchase === true;
        const item = ctx.referent({
          role: "shop_potion",
          label: potion.name ?? `Potion ${index + 1}`,
          enabled: canPurchase,
          occurrence: index,
          properties: {
            name: potion.name ?? null,
            description: potion.description ?? null,
            price: potion.cost ?? null,
            is_stocked: potion.is_stocked === true,
            can_purchase: canPurchase
          }
        });
        if (canPurchase && typeof potion.native_ref === "string") {
          ctx.action({
            verb: "activate",
            subject: item,
            label: `Buy ${item.label}`,
            raw: { cmd: "action", action: "buy_potion", args: { entry_ref: potion.native_ref } }
          });
        }
        return item.properties;
      });
      let cardRemoval = null;
      if (plainObject(state.card_removal)) {
        const canPurchase = state.card_removal.can_purchase === true;
        const item = ctx.referent({
          role: "shop_service",
          label: "Card removal",
          enabled: canPurchase,
          properties: {
            service: "card_removal",
            price: state.card_removal.cost ?? null,
            is_stocked: state.card_removal.is_stocked === true,
            can_purchase: canPurchase
          }
        });
        if (canPurchase && typeof state.card_removal.native_ref === "string") {
          ctx.action({
            verb: "activate",
            subject: item,
            label: "Buy card removal",
            raw: {
              cmd: "action",
              action: "remove_card",
              args: { entry_ref: state.card_removal.native_ref }
            }
          });
        }
        cardRemoval = item.properties;
      }
      const roomIdentityComplete = typeof state.room_ref === "string" && state.room_ref.length > 0;
      if (roomIdentityComplete) {
        ctx.action({
          verb: "close",
          label: "Leave shop",
          raw: { cmd: "action", action: "leave_shop", args: { room_ref: state.room_ref } }
        });
      }
      const entryIdentityComplete = [
        ...(state.cards ?? []),
        ...(state.relics ?? []),
        ...(state.potions ?? []),
        ...(plainObject(state.card_removal) ? [state.card_removal] : [])
      ].every((entry) => typeof entry.native_ref === "string" && entry.native_ref.length > 0);
      const actionabilityComplete = [
        ...(state.cards ?? []),
        ...(state.relics ?? []),
        ...(state.potions ?? []),
        ...(plainObject(state.card_removal) ? [state.card_removal] : [])
      ].every((entry) => typeof entry.can_purchase === "boolean");
      const complete = roomIdentityComplete && entryIdentityComplete && actionabilityComplete;
      return {
        kind: "shop_inventory",
        stage: "choosing",
        prompt: null,
        surface: {
          kind: "shop_inventory",
          stage: "choosing",
          cards,
          relics,
          potions,
          card_removal: cardRemoval
        },
        context: { ...commonContext, kind: "shop" },
        complete,
        missing: [
          ...(roomIdentityComplete ? [] : ["native_shop_owner_identity"]),
          ...(entryIdentityComplete ? [] : ["stable_shop_operand_identity"]),
          ...(actionabilityComplete ? [] : ["native_shop_entry_actionability"])
        ]
      };
    }
    default:
      return {
        kind: String(state.decision ?? "unknown"),
        stage: "unknown",
        prompt: state.message ?? null,
        surface: { kind: String(state.decision ?? "unknown"), stage: "unknown" },
        context: commonContext,
        complete: false,
        missing: ["unsupported_managed_decision"]
      };
  }
}

function enumerateSelections(items, min, max, stopAfter) {
  const result = [];
  const upper = Math.min(Math.max(max, 0), items.length);
  const lower = Math.max(0, Math.min(min, upper));
  function visit(start, selected, target) {
    if (result.length >= stopAfter) return;
    if (selected.length === target) {
      result.push([...selected]);
      return;
    }
    for (let index = start; index < items.length; index += 1) {
      selected.push(items[index]);
      visit(index + 1, selected, target);
      selected.pop();
      if (result.length >= stopAfter) return;
    }
  }
  for (let count = lower; count <= upper && result.length < stopAfter; count += 1) {
    visit(0, [], count);
  }
  return result;
}

function capabilities(actions, referents) {
  const roles = new Map(referents.map((item) => [item.referent_id, item.role]));
  const values = new Map();
  for (const action of actions) {
    const value = {
      verb: action.verb,
      ...(action.subject_referent_id == null ? {} : { subject_role: roles.get(action.subject_referent_id) }),
      arguments: action.arguments.map((argument) => ({ role: argument.role, required: true })),
      availability_basis: "current_native_interaction"
    };
    values.set(JSON.stringify(value), value);
  }
  return [...values.values()];
}

function runDeckRead(snapshotId, state) {
  if (!Array.isArray(state.player?.deck)) return null;
  return {
    descriptor: {
      read_id: stableId("read", snapshotId, "run_deck"),
      kind: "run_deck",
      target_referent_id: null,
      content_schema: "sts2.player-environment/read/run_deck-1",
      visibility_basis: "player_openable_run_deck_view",
      snapshot_bound: true,
      ordering_semantics: "unordered_multiset",
      hidden_by_policy: []
    },
    content: {
      kind: "run_deck",
      card_count: state.player.deck.length,
      cards: state.player.deck.map((card, index) => ({
        entity_id: stableId("deck_card", snapshotId, index, card.id, card.upgraded),
        ...cardProperties(card),
        is_selected: false
      }))
    }
  };
}

export function projectManagedCandidateDecision({ state, runtimeInstanceId, environmentFingerprint, sequence }) {
  if (!plainObject(state) || state.type !== "decision") {
    throw new TypeError("Managed Player Environment projection requires a raw decision state.");
  }
  if (typeof runtimeInstanceId !== "string" || runtimeInstanceId.length === 0) {
    throw new TypeError("Managed Player Environment projection requires runtimeInstanceId.");
  }
  if (typeof environmentFingerprint !== "string" || environmentFingerprint.length === 0) {
    throw new TypeError("Managed Player Environment projection requires environmentFingerprint.");
  }
  if (!Number.isSafeInteger(sequence) || sequence < 1) {
    throw new TypeError("Managed Player Environment projection requires a positive sequence.");
  }

  const ctx = createProjectionContext({ state, runtimeInstanceId, sequence });
  const surface = currentSurface(state, ctx);
  const terminal = state.decision === "game_over";
  const actionProjectionComplete = surface.complete === true && ctx.actions.length <= ACTION_LIMIT;
  if (!actionProjectionComplete) {
    ctx.actions.length = 0;
    ctx.bindings.clear();
  }
  const read = runDeckRead(ctx.snapshotId, state);
  const missing = [...new Set([
    "canonical_persistent_run_identity",
    "native_entity_identity",
    ...(surface.missing ?? [])
  ])];
  const snapshot = {
    protocol_version: SUPPORTED_PLAYER_ENVIRONMENT_PROTOCOL,
    schema: "sts2.player-environment/snapshot-1",
    snapshot_id: ctx.snapshotId,
    sequence,
    observed_at: new Date().toISOString(),
    status: actionProjectionComplete && ctx.actions.length > 0
      ? "interactive"
      : terminal ? "observed" : "visible_unsupported",
    persistent: null,
    interaction: {
      interaction_id: ctx.interactionId,
      kind: surface.kind,
      stage: surface.stage,
      prompt: surface.prompt,
      content_schema: `sts2.player-environment/surface/${surface.kind.replace(/[^a-z0-9_]+/giu, "_").toLowerCase()}-1`,
      content: { surface: surface.surface, context: surface.context },
      capabilities: actionProjectionComplete ? capabilities(ctx.actions, ctx.referents) : []
    },
    referents: ctx.referents,
    bound_actions: {
      schema: "sts2.player-environment/bound-actions-1",
      status: actionProjectionComplete ? "complete" : "unavailable",
      materialized_count: actionProjectionComplete ? ctx.actions.length : 0,
      total_count: actionProjectionComplete ? ctx.actions.length : (surface.totalCount ?? 0),
      limit: ACTION_LIMIT,
      ordering_semantics: "managed_native_order_then_operand_identity",
      actions: actionProjectionComplete ? ctx.actions : []
    },
    reads: read == null ? [] : [read.descriptor],
    completeness: {
      status: "partial",
      visible_information: "managed_candidate_projection_is_not_yet_cross_host_qualified",
      interaction_discovery: "derived_from_exact_build_managed_decision_state",
      missing,
      hidden_by_policy: ["hidden_rng", "future_rewards", "future_events"]
    },
    session: { runtime_instance_id: runtimeInstanceId, environment_fingerprint: environmentFingerprint },
    information_policy: INFORMATION_POLICY
  };
  decodePlayerSnapshot(snapshot);
  return Object.freeze({
    snapshot,
    bindings: ctx.bindings,
    raw_state_sha256: ctx.rawStateSha,
    reads: read == null ? new Map() : new Map([[read.descriptor.read_id, read]])
  });
}

function unknownAction(boundActionId) {
  return { bound_action_id: boundActionId, verb: "activate", subject_referent_id: null, arguments: [] };
}

function receipt({ requestId, delivery, action, reasonCode, detail, retry, successor }) {
  const value = {
    protocol_version: SUPPORTED_PLAYER_ENVIRONMENT_PROTOCOL,
    schema: "sts2.player-environment/receipt-1",
    request_id: requestId,
    delivery,
    action: {
      bound_action_id: action.bound_action_id,
      verb: action.verb,
      subject_referent_id: action.subject_referent_id ?? null,
      arguments: action.arguments ?? []
    },
    reason_code: reasonCode,
    detail,
    retry,
    successor
  };
  decodePlayerReceipt(value);
  return value;
}

export class ManagedPlayerEnvironmentSession {
  #process;
  #runtimeInstanceId;
  #environmentFingerprint;
  #character;
  #sequence = 0;
  #projection = null;
  #ledger = new Map();
  #tainted = false;

  constructor({ process, runtimeInstanceId, environmentFingerprint, character = "Ironclad" }) {
    if (process == null || typeof process.request !== "function") {
      throw new TypeError("ManagedPlayerEnvironmentSession requires a JSON-line process.");
    }
    this.#process = process;
    this.#runtimeInstanceId = runtimeInstanceId;
    this.#environmentFingerprint = environmentFingerprint;
    this.#character = character;
  }

  get tainted() {
    return this.#tainted;
  }

  async mount({ seed, reset = false, timeoutMs = 10_000 }) {
    if (typeof seed !== "string" || seed.length === 0) throw new TypeError("mount requires seed.");
    if (this.#tainted) throw new Error("A tainted Managed Player Environment session cannot mount another run.");
    const state = await this.#process.request({
      cmd: reset ? "reset_run" : "start_run",
      character: this.#character,
      seed
    }, timeoutMs);
    this.#ledger.clear();
    return this.#setState(state);
  }

  async close() {
    return this.#process.stop({ request: { cmd: "quit" }, timeoutMs: 5_000 });
  }

  observe() {
    if (this.#projection == null) throw new Error("No managed run is mounted.");
    return this.#projection.snapshot;
  }

  read({ readId, expectedSnapshotId }) {
    if (this.#projection == null) throw new Error("No managed run is mounted.");
    if (expectedSnapshotId !== this.#projection.snapshot.snapshot_id) {
      throw new Error("stale_snapshot");
    }
    const read = this.#projection.reads.get(readId);
    if (read == null) throw new Error("unknown_read");
    const value = {
      protocol_version: SUPPORTED_PLAYER_ENVIRONMENT_PROTOCOL,
      schema: "sts2.player-environment/read-1",
      read_id: readId,
      expected_snapshot_id: expectedSnapshotId,
      observed_snapshot_id: this.#projection.snapshot.snapshot_id,
      observed_at: new Date().toISOString(),
      kind: read.descriptor.kind,
      target_referent_id: read.descriptor.target_referent_id,
      visibility_basis: read.descriptor.visibility_basis,
      ordering_semantics: read.descriptor.ordering_semantics,
      content_schema: read.descriptor.content_schema,
      content: read.content,
      completeness: {
        status: "partial",
        visible_information: "managed_candidate_run_deck_projection_not_cross_host_qualified",
        interaction_discovery: "read_only",
        missing: ["native_card_entity_identity", "fully_rendered_localized_dynamic_text"],
        hidden_by_policy: []
      },
      session: this.#projection.snapshot.session,
      information_policy: INFORMATION_POLICY
    };
    decodePlayerRead(value);
    return value;
  }

  async submit({ requestId, expectedSnapshotId, boundActionId, timeoutMs = 10_000 }) {
    if (this.#projection == null) throw new Error("No managed run is mounted.");
    for (const [name, value] of Object.entries({ requestId, expectedSnapshotId, boundActionId })) {
      if (typeof value !== "string" || value.length === 0) throw new TypeError(`submit requires ${name}.`);
    }
    const requestKey = JSON.stringify({ expectedSnapshotId, boundActionId });
    const previous = this.#ledger.get(requestId);
    if (previous != null) {
      if (previous.requestKey === requestKey) return previous.receipt;
      return receipt({
        requestId,
        delivery: "not_delivered",
        action: unknownAction(boundActionId),
        reasonCode: "request_id_conflict",
        detail: "The request id was already used for different snapshot/action operands.",
        retry: { allowed: false, reason: "request_identity_is_immutable" },
        successor: this.#projection.snapshot
      });
    }
    if (this.#tainted) {
      const value = receipt({
        requestId,
        delivery: "not_delivered",
        action: unknownAction(boundActionId),
        reasonCode: "runtime_tainted_after_unknown",
        detail: "Mutation authority is closed until the process is replaced.",
        retry: { allowed: false, reason: "unknown_delivery_requires_process_replacement" },
        successor: null
      });
      this.#ledger.set(requestId, { requestKey, receipt: value });
      return value;
    }
    if (expectedSnapshotId !== this.#projection.snapshot.snapshot_id) {
      const value = receipt({
        requestId,
        delivery: "not_delivered",
        action: unknownAction(boundActionId),
        reasonCode: "stale_snapshot",
        detail: "The current snapshot no longer matches the request.",
        retry: { allowed: true, reason: "observe_current_snapshot_and_choose_again" },
        successor: this.#projection.snapshot
      });
      this.#ledger.set(requestId, { requestKey, receipt: value });
      return value;
    }
    const binding = this.#projection.bindings.get(boundActionId);
    if (binding == null) {
      const value = receipt({
        requestId,
        delivery: "not_delivered",
        action: unknownAction(boundActionId),
        reasonCode: "unknown_bound_action",
        detail: "The action is not part of the complete current projection.",
        retry: { allowed: false, reason: "unadvertised_actions_are_not_authorized" },
        successor: this.#projection.snapshot
      });
      this.#ledger.set(requestId, { requestKey, receipt: value });
      return value;
    }
    let successor;
    try {
      successor = await this.#process.request(binding.raw_request, timeoutMs);
    } catch (error) {
      this.#tainted = true;
      const value = receipt({
        requestId,
        delivery: "unknown",
        action: binding.action,
        reasonCode: "managed_transport_unknown",
        detail: error instanceof Error ? error.message : String(error),
        retry: { allowed: false, reason: "unknown_delivery_must_not_be_retried" },
        successor: null
      });
      this.#ledger.set(requestId, { requestKey, receipt: value });
      return value;
    }
    if (!plainObject(successor) || successor.type !== "decision") {
      this.#tainted = true;
      const value = receipt({
        requestId,
        delivery: "unknown",
        action: binding.action,
        reasonCode: "managed_execution_unknown",
        detail: plainObject(successor) && successor.type === "error"
          ? String(successor.message ?? "Managed action returned an error after dispatch.")
          : "Managed action did not return a successor decision.",
        retry: { allowed: false, reason: "unknown_delivery_must_not_be_retried" },
        successor: null
      });
      this.#ledger.set(requestId, { requestKey, receipt: value });
      return value;
    }
    const next = this.#setState(successor);
    const value = receipt({
      requestId,
      delivery: "delivered",
      action: binding.action,
      reasonCode: null,
      detail: "The exact bound input was delivered; no business completion is claimed.",
      retry: { allowed: false, reason: "request_already_delivered" },
      successor: next
    });
    this.#ledger.set(requestId, { requestKey, receipt: value });
    return value;
  }

  #setState(state) {
    this.#sequence += 1;
    this.#projection = projectManagedCandidateDecision({
      state,
      runtimeInstanceId: this.#runtimeInstanceId,
      environmentFingerprint: this.#environmentFingerprint,
      sequence: this.#sequence
    });
    return this.#projection.snapshot;
  }
}

export async function startManagedPlayerEnvironmentSession({
  root,
  candidateDirectory,
  diskIdentity,
  character = "Ironclad",
  requestTimeoutMs = 10_000
}) {
  const runtime = await startManagedCandidateRuntime({
    root,
    candidateDirectory,
    diskIdentity,
    requestTimeoutMs
  });
  const environmentFingerprint = digest({
    candidate_id: runtime.manifest.candidate_id,
    source_patch_sha256: runtime.build.source_patch_sha256,
    host_artifact_sha256: runtime.build.artifact_sha256,
    runtime_sts2_sha256: runtime.build.runtime_sts2_sha256,
    exact_game: runtime.exactGame
  });
  return {
    session: new ManagedPlayerEnvironmentSession({
      process: runtime.process,
      runtimeInstanceId: runtime.adapterRuntimeInstanceId,
      environmentFingerprint,
      character
    }),
    runtime,
    environmentFingerprint
  };
}
