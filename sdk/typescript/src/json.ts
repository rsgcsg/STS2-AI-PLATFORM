export type JsonPrimitive = string | number | boolean | null;
export type JsonValue = JsonPrimitive | JsonObject | JsonValue[];
export interface JsonObject {
  [key: string]: JsonValue;
}

export function isJsonObject(value: unknown): value is JsonObject {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function asJsonObject(value: unknown): JsonObject | undefined {
  return isJsonObject(value) ? value : undefined;
}

export function asJsonArray(value: unknown): JsonValue[] | undefined {
  return Array.isArray(value) ? value.filter(isJsonValue) : undefined;
}

export function isJsonValue(value: unknown): value is JsonValue {
  if (value === null || ["string", "number", "boolean"].includes(typeof value)) return true;
  if (Array.isArray(value)) return value.every(isJsonValue);
  if (!isJsonObject(value)) return false;
  return Object.values(value).every(isJsonValue);
}

export function cloneJson<T extends JsonValue>(value: T): T {
  return JSON.parse(JSON.