import { useEffect, useState } from "react";
import Link from "next/link";
import { Alert, Box, CircularProgress, Typography } from "@mui/material";
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
  const [success, setSuccess] = useState(null);

  useEffect(() => {
    Promise.all([getDefaultPricing(), getLocationPricingList()])
      .then(([defaults, list]) => {
        setProfile(defaults);
        setLocations(list);
      })
      .catch((err) => setError(serverMessage(err, "Couldn't load pricing.")));
  }, []);

  const handleSave = async (request) => {
    setError(null);
    setSuccess(null);
    try {
      setProfile(await updateDefaultPricing(request));
      setSuccess("Default pricing saved. Open bills use the new prices; closed bills keep theirs.");
    } catch (err) {
      setError(serverMessage(err, "Couldn't save pricing."));
    }
  };

  const ownPricing = locations.filter((l) => l.source === "Location");

  return (
    <Box sx={{ width: "100%", maxWidth: 1200, mx: "auto", p: 3, textAlign: "left" }}>
      <ManagementHeader title="Default Pricing" />
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Used by every location without its own pricing.
        {ownPricing.length > 0 &&
          ` Locations with their own pricing: ${ownPricing.map((l) => l.location_name).join(", ")}.`}{" "}
        Set a location's own pricing from <Link href="/location/menu-location">Locations</Link>.
      </Typography>

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

      {profile ? (
        // A new key after saving resets the form to what the server stored (rounded values)
        <PricingProfileForm
          key={profile.updated_at}
          profile={profile}
          onSubmit={handleSave}
          submitLabel="Save default pricing"
        />
      ) : (
        !error && <CircularProgress />
      )}
    </Box>
  );
};

export default DefaultPricing;
