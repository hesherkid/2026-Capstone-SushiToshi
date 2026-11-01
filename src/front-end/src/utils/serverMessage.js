// Truns an axios error into text for alert.
// The API sends errors as a plain string, { message } (MenuItemController) or { detail }
// TagController fall back when none of those are there

// An HTML page instead of an API error, e.g. ngrok's error page when the backend is down or restarting
export const isHtmlPage = (data) => typeof data === "string" && data.trimStart().startsWith("<");

export const serverMessage = (err, fallback) => {
  const data = err?.response?.data;
  if (typeof data === "string" && data.trim() && !isHtmlPage(data)) return data;
  for (const key of ["message", "detail"]) {
    if (typeof data?.[key] === "string" && data[key].trim()) return data[key];
  }
  // ASP.NET validation errors: { errors: { Rates: ["..."], Tax_Rate: ["..."] } }
  const fieldErrors = data?.errors ? Object.values(data.errors).flat().filter(Boolean) : [];
  if (fieldErrors.length) return fieldErrors.join(" ");
  return fallback;
};

export default serverMessage;
