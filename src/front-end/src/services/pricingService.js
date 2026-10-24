import api from "@/config/api";

// Pricing admin (PricingController). Rates are fractions: 0.05 = 5%

export const getDefaultPricing = async () => (await api.get("/pricing/default")).data;

export const updateDefaultPricing = async (profile) =>
  (await api.put("/pricing/default", profile)).data;

// Every location and whether it uses the default ("Default") or its own ("Location") pricing
export const getLocationPricingList = async () => (await api.get("/pricing/locations")).data;

// The pricing a location uses; `source` says whether it's its own or the default
export const getLocationPricing = async (locationId) =>
  (await api.get(`/pricing/locations/${locationId}`)).data;

export const updateLocationPricing = async (locationId, profile) =>
  (await api.put(`/pricing/locations/${locationId}`, profile)).data;

// Back to the default; returns the default pricing
export const resetLocationPricing = async (locationId) =>
  (await api.delete(`/pricing/locations/${locationId}`)).data;

// What a bill created on that date would be charged ("2026-12-25")
export const previewPricing = async (locationId, date) =>
  (await api.get(`/pricing/locations/${locationId}/preview`, { params: { date } })).data;
