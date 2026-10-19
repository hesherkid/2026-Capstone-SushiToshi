// Lets a page show its loading state in the app bar's spinner instead of a spinner of its own,
// so there's only ever one loading indicator on screen.

// Fired with detail.loading = true/false; the app bar listens for it
export const PAGE_LOADING_EVENT = "page-loading-changed";

export const setPageLoading = (loading) => {
  if (typeof window === "undefined") return;
  window.dispatchEvent(new CustomEvent(PAGE_LOADING_EVENT, { detail: { loading: Boolean(loading) } }));
};
