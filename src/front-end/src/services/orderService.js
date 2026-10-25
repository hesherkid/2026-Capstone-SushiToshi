import api from "@/config/api";

const isGuest = () => typeof window !== "undefined" && localStorage.getItem("guest") === "true";

/**
 * Service for managing orders and order items
 */
export const orderService = {
  /**
   * Creates a new order for a dining session
   * @param {Object} orderData - Order creation data
   * @param {number} orderData.session_Id - Session ID
   * @param {number} orderData.bill_Id - Bill ID
   * @returns {Promise<Object>} Created order details
   */


  //Clean this up if time
  createOrder: async (orderData) => {

    if (isGuest()) {
    orderData.Request_By_Oid = localStorage.getItem("guest_oid");
    orderData.Request_By_Name = "Guest";
  }

    //const apiClient = api;
    const apiClient =  api;

    try {
      const response = await apiClient.post("/Order", orderData);
      return response.data;
    } catch (error) {

      if (error.response) {

      } else {
        console.error("⚠️ No response received from API");
      }
      throw error;
    }
  },
  // ORDER MANAGEMENT
  /**
   * Approves a pending order (Staff/Admin only)
   * Sets order status to 'Processing'
   * @param {number} orderId - Order ID to approve
   * @returns {Promise<Object>} Updated order with items
   */
  approveOrder: async (orderId) => {
    const response = await api.patch(`/Order/${orderId}/approve`);
    return response.data;
  },

  /**
   * Cancels a pending order
   * @param {number} orderId - Order ID to cancel
   * @returns {Promise<Object>} Cancelled order details
   */
  cancelOrder: async (orderId) => {
    const response = await api.patch(`/Order/${orderId}/cancel`);
    return response.data;
  },

  /**
   * Marks entire order as completed/delivered (Staff/Admin only)
   * @param {number} orderId - Order ID to complete
   * @returns {Promise<Object>} Completed order with all items
   */
  completeOrder: async (orderId) => {
    const response = await api.patch(`/Order/${orderId}/complete`);
    return response.data;
  },

  // ORDER ITEMS MANAGEMENT

  /**
   * Adds items to an existing order
   * @param {number} orderId - Order ID
   * @param {Array<Object>} items - Array of items to add
   * @param {number} items[].menu_Id - Menu ID
   * @param {number} items[].item_Id - Item ID
   * @param {number} items[].quantity - Quantity
   * @param {number} items[].price_at_time - Price at time of order
   * @returns {Promise<Array>} Array of added order items
   */
  addOrderItems: async (orderId, items) => {
    const apiClient = api;
    const response = await apiClient.post(`/Order/${orderId}/items`, items);

    return response.data;
  },

  /**
   * Updates a specific order item (quantity or price)
   * @param {number} orderId - Order ID
   * @param {number} itemId - Order Item ID
   * @param {Object} updateData - Update data
   * @param {number} [updateData.quantity] - New quantity
   * @param {number} [updateData.price_at_time] - New price (Staff only)
   * @returns {Promise<Object>} Updated order item
   */
  updateOrderItem: async (orderId, itemId, updateData) => {
    const response = await api.patch(
      `/Order/${orderId}/items/${itemId}`,
      updateData
    );
    return response.data;
  },

  /**
   * Marks a specific order item as completed/delivered (Staff/Admin only)
   * @param {number} orderId - Order ID
   * @param {number} itemId - Order Item ID
   * @returns {Promise<Object>} Completed order item
   */
  completeOrderItem: async (orderId, itemId) => {
    const response = await api.patch(
      `/Order/${orderId}/items/${itemId}/complete`
    );
    return response.data;
  },

  /**
   * Removes an item from an order (Staff/Admin only)
   * @param {number} orderId - Order ID
   * @param {number} orderItemId - Order Item ID to remove
   * @returns {Promise<string>} Success message
   */
  removeOrderItem: async (orderId, orderItemId) => {
    const response = await api.delete(
      `/Order/${orderId}/items/${orderItemId}`
    );
    return response.data;
  },

  // ORDER RETRIEVAL

  // TODO: This method needs filters by session or date range instead of returning all orders.
  /**
   * Gets all orders (Staff/Admin only)
   * @returns {Promise<Array>} Array of all orders
   */
  getAllOrders: async () => {
    const response = await api.get("/Order");
    return response.data;
  },

  /**
   * Gets all orders based on location (Staff/Admin only)
   * @returns {Promise<Array>} Array of all orders by location
   */
  getAllOrdersByLocation: async (locationId = null) => {
    const params = locationId ? { locationId } : {};
    const response = await api.get("/Order/all_orders", { params });
    return response.data;
  },

  /**
   * Gets detailed information about a specific order
   * @param {number} orderId - Order ID
   * @returns {Promise<Object>} Order details with customer info and items
   */
  getOrderById: async (orderId) => {
    const response = await api.get(`/Order/${orderId}`);
    return response.data;
  },

  /**
   * Gets all orders for a specific dining session
   * @param {number} sessionId - Session ID
   * @returns {Promise<Array>} Array of orders with items
   */
  getOrdersBySession: async (sessionId, billId = null) => {
    const params = billId ? { bill_id: billId } : {};
    const response = await api.get(`/Order/session/${sessionId}`, { params });
    return response.data;
  },

  /**
   * Gets all orders for the current user's active session
   * @param {number} [billId] - Optional bill ID for filtering
   * @returns {Promise<Array>} Array of orders for active session
   */
  getActiveSessionOrders: async (billId = null) => {
    const params = billId ? { bill_id: billId } : {};
    const response = await api.get("/Order/active-session/orders", { params });
    return response.data;
  },
};

export default orderService;