import { useEffect, useState } from "react";
import NextLink from "next/link";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  DialogActions,
  DialogContent,
  FormControlLabel,
  Link,
  Stack,
  Switch,
  TextField,
  Typography,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import DialogHeader from "@/components/common/DialogHeader";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import PricingProfileForm from "@/components/admin/pricing/PricingProfileForm";
import {
  getLocationPricing,
  previewPricing,
  resetLocationPricing,
  updateLocationPricing,
} from "@/services/pricingService";
import {
  formatCurrency,
  formatPricingType,
} from "@/components/staff/SessionDashboard/utils/sessionHelpers";
import serverMessage from "@/utils/serverMessage";

// Today as yyyy-mm-dd in the browser's time zone (toISOString would give the UTC date)
const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};

/**
 * A location's pricing: the default, or its own. Switching custom pricing on starts from the
 * default prices; switching it off (after saving) asks first, then goes back to the default.
 */
const LocationPricingDialog = ({ open, location, onClose }) => {
  const locationId = location?.location_Id;
  const [profile, setProfile] = useState(null);
  const [useOwn, setUseOwn] = useState(false);
  const [confirmReset, setConfirmReset] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);
  const [saveResult, setSaveResult] = useState(null);
  const [previewDate, setPreviewDate] = useState(today);
  const [preview, setPreview] = useState(null);

  useEffect(() => {
    if (!open || !locationId) return;
    setProfile(null);
    setError(null);
    setSuccess(null);
    setSaveResult(null);
    setPreview(null);
    getLocationPricing(locationId)
      .then((data) => {
        setProfile(data);
        setUseOwn(data.source === "Location");
      })
      .catch((err) => setError(serverMessage(err, "Couldn't load this location's pricing.")));
  }, [open, locationId]);

  const hasOwnPricing = profile?.source === "Location";

  const handleToggle = (e) => {
    setSuccess(null);
    setSaveResult(null);
    if (e.target.checked) {
      setUseOwn(true);   // the form starts from the default prices shown
    } else if (hasOwnPricing) {
      setConfirmReset(true);   // saved custom pricing: ask before removing it
    } else {
      setUseOwn(false);   // switched on but never saved
    }
  };

  const handleSave = async (request) => {
    setSuccess(null);
    try {
      setProfile(await updateLocationPricing(locationId, request));
      setSaveResult({ severity: "success", message: "Saved. This location now uses its own pricing." });
    } catch (err) {
      setSaveResult({ severity: "error", message: serverMessage(err, "Couldn't save pricing.") });
    }
  };

  const handleReset = async () => {
    try {
      setProfile(await resetLocationPricing(locationId));
      setUseOwn(false);
      setSuccess("This location uses the default pricing again.");
    } catch (err) {
      setError(serverMessage(err, "Couldn't switch back to the default pricing."));
    }
  };

  const handlePreview = async () => {
    setError(null);
    try {
      setPreview(await previewPricing(locationId, previewDate));
    } catch (err) {
      setError(serverMessage(err, "Couldn't check prices for that date."));
    }
  };

  return (
    <>
      <AppDialog open={open} onClose={onClose} maxWidth="md">
        <DialogHeader title="Pricing" subtitle={location?.name} onClose={onClose} />
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

          {!profile ? (
            !error && <CircularProgress />
          ) : (
            <Stack spacing={3} sx={{ mt: 1 }}>
              <Box>
                <FormControlLabel
                  label="Custom pricing for this location"
                  control={<Switch checked={useOwn} onChange={handleToggle} />}
                />
                <Typography variant="body2" color="text.secondary">
                  {useOwn ? (
                    "These prices apply only to this location."
                  ) : (
                    <>
                      Uses the <Link component={NextLink} href="/admin/pricing" underline="hover" sx={{ fontWeight: 600 }}>
                        default pricing
                      </Link>, shown below.
                    </>
                  )}
                </Typography>
              </Box>

              <PricingProfileForm
                key={`${profile.source}-${profile.updated_at}-${useOwn}`}
                profile={profile}
                onSubmit={handleSave}
                readOnly={!useOwn}
                canSaveUnchanged={useOwn && !hasOwnPricing}
                submitLabel="Save location pricing"
                saveResult={saveResult}
                onSaveResultClose={() => setSaveResult(null)}
              />

              <Box>
                <Typography variant="h6">Check a date</Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                  Uses saved prices and approved holidays.
                </Typography>
                <Stack direction="row" spacing={2} alignItems="center">
                  <TextField
                    type="date"
                    label="Date"
                    size="small"
                    value={previewDate}
                    onChange={(e) => setPreviewDate(e.target.value)}
                    slotProps={{ inputLabel: { shrink: true } }}
                  />
                  <Button variant="outlined" onClick={handlePreview} disabled={!previewDate}>
                    Check prices
                  </Button>
                </Stack>
                {preview && (
                  <Typography sx={{ mt: 1 }}>
                    {formatPricingType(preview.pricing_type)}
                    {preview.holiday_name && ` (${preview.holiday_name})`}: adults{" "}
                    {formatCurrency(preview.adult_price)}, seniors {formatCurrency(preview.senior_price)},
                    children {formatCurrency(preview.child_price)}, toddlers{" "}
                    {formatCurrency(preview.toddler_price)}
                  </Typography>
                )}
              </Box>
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>Close</Button>
        </DialogActions>
      </AppDialog>

      <ConfirmDialog
        open={confirmReset}
        title="Use default pricing?"
        message={`${location?.name} will lose its own prices and use the default pricing. Closed bills keep their prices.`}
        confirmLabel="Use default pricing"
        onConfirm={handleReset}
        onClose={() => setConfirmReset(false)}
      />
    </>
  );
};

export default LocationPricingDialog;
