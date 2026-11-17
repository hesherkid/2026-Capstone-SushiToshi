import api from "@/config/api";

// A location's Wi-Fi check (LocationController): the networks guests must join a table from.
// Also returns the admin's own address and network as the server sees them

export const getWifiCheck = async (locationId) =>
  (await api.get(`/location/${locationId}/wifi-check`)).data;

// An empty list turns the check off
export const updateWifiCheck = async (locationId, allowedNetworks) =>
  (await api.put(`/location/${locationId}/wifi-check`, { allowed_networks: allowedNetworks })).data;
