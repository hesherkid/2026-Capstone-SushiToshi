// In development, warns in the console when code reads a key an API response doesn't have.
// A key renamed on the backend but not here otherwise just reads as undefined and shows as a blank

// Keys that libraries check for on any object (promises, JSON.stringify, React, Jest), not API fields
const IGNORED_KEYS = new Set(["then", "toJSON", "$$typeof", "constructor", "asymmetricMatch", "nodeType", "_isMockFunction"]);

const warned = new Set();
// One wrapper per object, so reading the same object twice gives the same value and React effects don't re-run
const wrappers = new WeakMap();

// Plain JSON objects and arrays only; files like QR images (Blob) break if wrapped
const isJson = (value) =>
  value !== null &&
  typeof value === "object" &&
  (Array.isArray(value) || Object.getPrototypeOf(value) === Object.prototype);

const warnOnce = (label, key) => {
  const id = `${label} ${key}`;
  if (warned.has(id)) return;
  warned.add(id);
  console.warn(`[api] ${label}: the response has no "${key}" key. Was it renamed?`);
};

export const warnOnMissingKeys = (data, label) => {
  if (!isJson(data)) return data;
  if (wrappers.has(data)) return wrappers.get(data);

  const wrapper = new Proxy(data, {
    get(target, key, receiver) {
      const value = Reflect.get(target, key, receiver);
      if (typeof key !== "string" || IGNORED_KEYS.has(key)) return value;
      // A frozen property (e.g. rows MUI's DataGrid froze) must come back exactly as it is, or the browser throws
      const own = Object.getOwnPropertyDescriptor(target, key);
      if (own && !own.configurable && !own.writable) return value;
      // items[0] on an empty list is normal, not a missing key
      if (Array.isArray(target) && /^\d+$/.test(key)) return warnOnMissingKeys(value, label);
      if (!(key in target)) {
        warnOnce(label, key);
        return value;
      }
      return warnOnMissingKeys(value, label);
    },
  });
  wrappers.set(data, wrapper);
  return wrapper;
};

// "GET /Bill/get_bills/12" -> "GET /Bill/get_bills/:id", so each endpoint warns once, not once per id
export const endpointLabel = (config) =>
  `${(config?.method ?? "get").toUpperCase()} ${(config?.url ?? "").split("?")[0].replace(/\/\d+(?=\/|$)/g, "/:id")}`;

export default warnOnMissingKeys;
