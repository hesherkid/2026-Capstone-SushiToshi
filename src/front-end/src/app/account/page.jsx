"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Alert, Box, Button, Paper, TextField, Typography } from "@mui/material";
import useAuth from "@/hooks/useAuth";
import { decodeToken } from "@/config/auth";
import { getAuthToken, isGuestClaims } from "@/utils/auth";
import { changePassword, fetchAccount, updateAccount } from "@/services/account";
import { setPageLoading } from "@/utils/pageLoading";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import NextLink from "next/link";

const LOGIN_PATH = "/auth/login";
const MENU_PATH = "/menu/full-menu";
// Same limits as the API: names up to 100 characters, passwords 8 to 200
const NAME_MAX = 100;
const PASSWORD_MIN = 8;
const PASSWORD_MAX = 200;

const errorMessage = (err, fallback) =>
  err?.response?.status === 429
    ? "Too many attempts. Please wait a minute and try again."
    : err?.response?.data?.message || fallback;

const Section = ({ title, children }) => (
  <Paper variant="outlined" sx={{ borderRadius: 3, p: 2.5, textAlign: "left" }}>
    <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 700, mb: 2 }}>
      {title}
    </Typography>
    {children}
  </Paper>
);

const NameForm = ({ account, onSaved }) => {
  const [firstName, setFirstName] = useState(account.first_name);
  const [lastName, setLastName] = useState(account.last_name);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);

  const changed =
    firstName.trim() !== account.first_name || lastName.trim() !== account.last_name;
  const valid = firstName.trim() !== "" && lastName.trim() !== "";

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);
    setSaved(false);
    setSaving(true);
    try {
      const updated = await updateAccount({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
      });
      setFirstName(updated.first_name);
      setLastName(updated.last_name);
      onSaved(updated);
      setSaved(true);
    } catch (err) {
      setError(errorMessage(err, "Couldn't save your name. Please try again."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Box component="form" onSubmit={handleSubmit} noValidate>
      <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        <TextField
          label="First name"
          value={firstName}
          onChange={(e) => {
            setFirstName(e.target.value);
            setSaved(false);
          }}
          required
          fullWidth
          autoComplete="given-name"
          slotProps={{ htmlInput: { maxLength: NAME_MAX } }}
        />
        <TextField
          label="Last name"
          value={lastName}
          onChange={(e) => {
            setLastName(e.target.value);
            setSaved(false);
          }}
          required
          fullWidth
          autoComplete="family-name"
          slotProps={{ htmlInput: { maxLength: NAME_MAX } }}
        />
        {error && <Alert severity="error">{error}</Alert>}
        {saved && <Alert severity="success">Your name was saved.</Alert>}
        <Button
          type="submit"
          variant="contained"
          disabled={!changed || !valid || saving}
          sx={{ alignSelf: "flex-end", borderRadius: 999, px: 3 }}
        >
          {saving ? "Saving…" : "Save name"}
        </Button>
      </Box>
    </Box>
  );
};

const EMPTY_PASSWORDS = { current: "", next: "", confirm: "" };

const PasswordForm = () => {
  const [values, setValues] = useState(EMPTY_PASSWORDS);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);

  const set = (field) => (e) => {
    setValues((v) => ({ ...v, [field]: e.target.value }));
    setSaved(false);
  };

  // Passwords are never trimmed, but one of only spaces isn't allowed
  const problem = () => {
    if (values.next !== values.confirm) return "New passwords do not match";
    if (values.next.length < PASSWORD_MIN)
      return `New password must be at least ${PASSWORD_MIN} characters long`;
    if (values.next.length > PASSWORD_MAX)
      return `New password must be at most ${PASSWORD_MAX} characters long`;
    if (values.next.trim() === "") return "New password can't be only spaces";
    return null;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaved(false);
    const invalid = problem();
    if (invalid) {
      setError(invalid);
      return;
    }

    setError(null);
    setSaving(true);
    try {
      await changePassword({ currentPassword: values.current, newPassword: values.next });
      setValues(EMPTY_PASSWORDS);
      setSaved(true);
    } catch (err) {
      setError(errorMessage(err, "Couldn't change your password. Please try again."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Box component="form" onSubmit={handleSubmit} noValidate>
      <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        <TextField
          label="Current password"
          type="password"
          value={values.current}
          onChange={set("current")}
          required
          fullWidth
          autoComplete="current-password"
        />
        <TextField
          label="New password"
          type="password"
          value={values.next}
          onChange={set("next")}
          required
          fullWidth
          autoComplete="new-password"
          helperText={`At least ${PASSWORD_MIN} characters`}
        />
        <TextField
          label="Confirm new password"
          type="password"
          value={values.confirm}
          onChange={set("confirm")}
          required
          fullWidth
          autoComplete="new-password"
        />
        {error && <Alert severity="error">{error}</Alert>}
        {saved && <Alert severity="success">Your password was changed.</Alert>}
        <Button
          type="submit"
          variant="contained"
          disabled={!values.current || !values.next || !values.confirm || saving}
          sx={{ alignSelf: "flex-end", borderRadius: 999, px: 3 }}
        >
          {saving ? "Changing…" : "Change password"}
        </Button>
      </Box>
    </Box>
  );
};

/**
 * The signed-in user's account: change their name (not staff, an admin does that), and their
 * password unless they only sign in with Google. The email can't be changed here. Guests go to the menu, signed-out visitors
 * to sign in.
 */
const AccountPage = () => {
  const router = useRouter();
  const { isAuthenticated, loading: authLoading } = useAuth();
  const [account, setAccount] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (authLoading) return;
    const token = getAuthToken();
    if (token && isGuestClaims(decodeToken(token))) {
      router.replace(MENU_PATH);
      return;
    }
    if (!isAuthenticated) {
      router.replace(LOGIN_PATH);
      return;
    }

    setPageLoading(true);
    fetchAccount()
      .then(setAccount)
      .catch((err) => {
        console.error("Error loading account:", err);
        setError(errorMessage(err, "Couldn't load your account. Please try again."));
      })
      .finally(() => setPageLoading(false));
  }, [authLoading, isAuthenticated, router]);

  useEffect(() => () => setPageLoading(false), []);

  return (
    <CustomerPageContainer sx={{ maxWidth: 560 }}>
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          gap: 1,
          mt: { xs: 3, sm: 2 },
          mb: 3,
        }}
      >
        <Button
          component={NextLink}
          href="/"
          aria-label="Back to home"
          sx={{ minWidth: 0, p: 1, borderRadius: 999, color: "text.primary" }}
        >
          <MaterialSymbol name="arrow_back" size={24} />
        </Button>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700 }}>
          Account
        </Typography>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      {account && (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2, mb: 4 }}>
          <Section title="Your name">
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Signed in as {account.email}
            </Typography>
            {account.can_change_name ? (
              <NameForm account={account} onSaved={setAccount} />
            ) : (
              <>
                <Typography sx={{ fontWeight: 600 }}>
                  {account.first_name} {account.last_name}
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                  Ask an admin to change your name.
                </Typography>
              </>
            )}
          </Section>

          <Section title="Password">
            {account.has_password ? (
              <PasswordForm />
            ) : (
              <Typography variant="body2" color="text.secondary">
                You sign in with Google, so there&apos;s no password to change here.
              </Typography>
            )}
          </Section>
        </Box>
      )}
    </CustomerPageContainer>
  );
};

export default AccountPage;
