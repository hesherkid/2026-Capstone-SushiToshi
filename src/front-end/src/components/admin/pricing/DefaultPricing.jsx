import { useEffect, useState } from "react";
import NextLink from "next/link";
import { Alert, Box, Chip, CircularProgress, Link, Stack, Typography } from "@mui/material";
import ManagementHeader from "@/components/common/ManagementHeader";
import PricingProfileForm from "@/components/admin/pricing/PricingProfileForm";
import {
  getDefaultPricing,
  getLocationPricingList,
  updateDefaultPricing,
} from "@/services/pricingService";
import serverMessage from "@/utils/serverMessage";

// Pricing for every location that doesn't have its own
const DefaultPricing = () => {
  const [profile, setProfile] = useState(null);
  const [locations, setLocations] = useState([]);
  const [error, setError] = useState(null);
  const [saveResult, setSaveResult] = useState(null);

  useEffect(() => {
    Promise.all([getDefaultPricing(), getLocationPricingList()])
      .then(([defaults, list]) => {
        setProfile(defaults);
        setLocations(list);
      })
      .catch((err) => setError(serverMessage(err, "Couldn't load pricing.")));
  }, []);

  const handleSave = async (request) => {
    try {
      setProfile(await updateDefaultPricing(request));
      setSaveResult({
        severity: "success",
        message: "Default pricing saved. Open bills use the new prices; closed bills keep theirs.",
      });
    } catch (err) {
      setSaveResult({ severity: "error", message: serverMessage(err, "Couldn't save pricing.") });
    }
  };

  const ownPricing = locations.filter((l) => l.source === "Location");

  return (
    <Box sx={{ width: "100%", maxWidth: 1200, mx: "auto", p: 3, textAlign: "left" }}>
      <ManagementHeader title="Default Pricing" />
      <Typography color="text.secondary" sx={{ mb: ownPricing.length > 0 ? 1 : 2 }}>
        These prices apply to every location that doesn&apos;t have its own pricing. To give a location
        its own pricing, open it on the{" "}
        <Link component={NextLink} href="/location/menu-location" underline="hover" sx={{ fontWeight: 600 }}>
          Locations
        </Link>{" "}
        page.
      </Typography>
      {ownPricing.length > 0 && (
        <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" alignItems="center" sx={{ mb: 2 }}>
          <Typography variant="body2" color="text.secondary">
            Locations with their own pricing:
          </Typography>
          {ownPricing.map((l) => (
            <Chip key={l.location_id} label={l.location_name} size="small" variant="outlined" />
          ))}
        </Stack>
      )}

      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {profile ? (
        // A new key after saving resets the form to what the server stored (rounded values)
        <PricingProfileForm
          key={profile.updated_at}
          profile={profile}
          onSubmit={handleSave}
          submitLabel="Save default pricing"
          saveResult={saveResult}
          onSaveResultClose={() => setSaveResult(null)}
        />
      ) : (
        !error && <CircularProgress />
      )}
    </Box>
  );
};

export default DefaultPricing;
