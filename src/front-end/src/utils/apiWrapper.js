import { isHtmlPage } from "./serverMessage";

/**
 * Creates a reusable API wrapper that handles loading states, errors, and success callbacks.
 * This eliminates the repetitive try-catch-finally blocks in API hooks.
 * 
 * @param {Object} config - Configuration object
 * @param {Function} config.setIsLoading - Function to set loading state
 * @param {Function} config.setActionError - Function to set error state
 * @param {Function} config.handleError - Function to handle errors
 * @param {Function} config.onSuccess - Success callback (optional)
 * @returns {Function} Wrapper function for API calls
 */
export const createApiWrapper = ({ 
  setIsLoading, 
  setActionError, 
  handleError, 
  onSuccess 
}) => {
  /**
   * Wraps an API call with standard error handling and loading states.
   * 
   * @param {string} context - Name of the operation (for error logging)
   * @param {Function} apiCall - Async function that performs the API call
   * @param {Object} options - Optional configuration
   * @param {boolean} options.triggerSuccess - Whether to call onSuccess callback
   * @param {*} options.defaultReturn - Value to return on error (default: null)
   * @returns {Promise} Result of the API call
   */
  return async (context, apiCall, options = {}) => {
    const { triggerSuccess = false, defaultReturn = null, successMessage = null } = options;

    try {
      setActionError(null);
      setIsLoading(true);
      
      const result = await apiCall();
      
      // Show success notification if message provided
      if (successMessage && typeof notifySuccess === "function") {
        notifySuccess(successMessage);
      }
      
      // Call onSuccess callback for data refresh
      if (triggerSuccess && typeof onSuccess === "function") {
        await onSuccess();
      }
      
      return result;
    } catch (error) {
      const errorData = error.response?.data;
      const detailedError = {
        ...error,
        userMessage: errorData?.detail ||
                    errorData?.message ||
                    errorData?.error ||
                    (isHtmlPage(errorData) ? "Couldn't reach the server. Please try again." : null) ||
                    (typeof errorData === 'string' ? errorData : null) ||
                    error.message
      };

      handleError(detailedError, context);
      return defaultReturn;
    } finally {
      setIsLoading(false);
    }
  };
};