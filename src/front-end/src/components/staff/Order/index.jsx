import { OrderDashboardProvider } from "./context/OrderDashboardContext";
import OrderDashboardInner from "./components/OrderDashboardInner";

const OrderDashboard = () => (
  <OrderDashboardProvider>
    <OrderDashboardInner />
  </OrderDashboardProvider>
);

export default OrderDashboard;
