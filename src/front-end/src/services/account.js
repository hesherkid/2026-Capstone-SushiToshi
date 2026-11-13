import api from "@/config/api";
import storage from "@/utils/storage";

// The signed-in user's own account (guests get 403): email, name, role, and has_password
// (false for accounts that only sign in with Google)

export const fetchAccount = async () => (await api.get("/me/account")).data;

// Also updates the name saved at sign-in, which the home page greets with
export const updateAccount = async ({ firstName, lastName }) => {
  const account = (await api.put("/me/account", { first_name: firstName, last_name: lastName })).data;

  try {
    const saved = JSON.parse(storage.get("user") || "null") || {};
    storage.set(
      "user",
      JSON.stringify({ ...saved, first_name: account.first_name, last_name: account.last_name }),
    );
  } catch {
    // A broken saved user only costs the greeting
  }

  return account;
};

// A wrong current password is a 400 with a message
export const changePassword = ({ currentPassword, newPassword }) =>
  api.post("/me/account/change-password", {
    current_password: currentPassword,
    new_password: newPassword,
  });
