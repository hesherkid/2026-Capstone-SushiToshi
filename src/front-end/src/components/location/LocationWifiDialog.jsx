import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  DialogActions,
  DialogContent,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import DialogHeader from "@/components/common/DialogHeader";
import { getWifiCheck, updateWifiCheck } from "@/services/wifiCheckService";
import serverMessage from "@/utils/serverMessage";

/**
 * A location's Wi-Fi check. When it lists networks, guests can only join a table from them, so the
 * table QR link can't be used from outside the restaurant. The server compares the phone's address,
 * so an admin at the restaurant, on its Wi-Fi, can add it with "Add this network".
 */
const LocationWifiDialog = ({ open, location, onClose }) => {
  const locationId = location?.location_id;
  const [check, setCheck] = useState(null);
  const [networks, setNetworks] = useState([]);
  const [entry, setEntry] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);

  useEffect(() => {
    if (!open || !locationId) return;
    setCheck(null);
    setEntry("");
    setError(null);
    setSuccess(null);
    getWifiCheck(locationId)
      .then((data) => {
        setCheck(data);
        setNetworks(data.allowed_networks);
      })
      .catch((err) => setError(serverMessage(err, "Couldn't load this location's Wi-Fi check.")));
  }, [open, locationId]);

  const saved = check?.allowed_networks ?? [];
  const changed = networks.join(",") !== saved.join(",");

  const addNetwork = (value) => {
    const network = value.trim();
    setSuccess(null);
    if (network && !networks.includes(network)) setNetworks([...networks, network]);
    setEntry("");
  };

  const removeNetwork = (network) => {
    setSuccess(null);
    setNetworks(networks.filter((n) => n !== network));
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    try {
      const data = await updateWifiCheck(locationId, networks);
      setCheck(data);
      setNetworks(data.allowed_networks);
      setSuccess(
        data.allowed_networks.length
          ? "Saved. Guests can only join a table from these networks."
          : "Saved. The Wi-Fi check is off, so anyone with the QR link can join.",
      );
    } catch (err) {
      setError(serverMessage(err, "Couldn't save the Wi-Fi check."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <AppDialog open={open} onClose={onClose} maxWidth="sm">
      <DialogHeader title="Wi-Fi check" subtitle={location?.name} onClose={onClose} />
      <DialogContent sx={{ textAlign: "left" }}>
        {error && (
          <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}
        {success && (
          <Alert severity="success" onClose={() => setSuccess(null)} sx={{ mb: 2 }}>
            {success}
          </Alert>
        )}

        {!check ? (
          !error && <CircularProgress />
        ) : (
          <Stack spacing={3} sx={{ mt: 1 }}>
            <Typography variant="body2" color="text.secondary">
              Only phones on these networks can join a table. Guests on mobile data or a VPN will be asked
              to connect to the restaurant Wi-Fi first. With no networks, anyone with the table&apos;s QR
              link can join.
            </Typography>

            {!check.can_see_client_addresses && (
              <Alert severity="warning">
                The server sees every request as coming from {check.your_address ?? "an unknown address"}, so it
                can&apos;t tell phones apart and the check would refuse everyone. Turn on forwarded headers
                (ForwardedHeaders:Enabled) before turning this on.
              </Alert>
            )}

            <Box>
              <Typography variant="h6" sx={{ mb: 1 }}>
                Networks
              </Typography>
              {networks.length === 0 ? (
                <Chip label="Off: anyone can join" size="small" variant="outlined" />
              ) : (
                <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                  {networks.map((network) => (
                    <Chip
                      key={network}
                      label={network}
                      onDelete={() => removeNetwork(network)}
                      aria-label={`Network ${network}`}
                    />
                  ))}
                </Stack>
              )}
            </Box>

            {check.your_network && check.can_see_client_addresses && (
              <Box>
                <Typography variant="body2" sx={{ mb: 1 }}>
                  You&apos;re connecting from <strong>{check.your_address}</strong>. If you&apos;re at this
                  restaurant on its Wi-Fi, add this network.
                </Typography>
                <Button
                  variant="outlined"
                  size="small"
                  onClick={() => addNetwork(check.your_network)}
                  disabled={networks.includes(check.your_network)}
                >
                  Add this network ({check.your_network})
                </Button>
              </Box>
            )}

            <Stack direction="row" spacing={2} alignItems="flex-start">
              <TextField
                label="Address or network"
                placeholder="203.0.113.10 or 192.168.1.0/24"
                size="small"
                value={entry}
                onChange={(e) => setEntry(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    addNetwork(entry);
                  }
                }}
                sx={{ flexGrow: 1 }}
              />
              <Button variant="outlined" onClick={() => addNetwork(entry)} disabled={!entry.trim()}>
                Add
              </Button>
            </Stack>
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
        <Button variant="contained" onClick={handleSave} disabled={!check || !changed || saving}>
          Save
        </Button>
      </DialogActions>
    </AppDialog>
  );
};

export default LocationWifiDialog;
