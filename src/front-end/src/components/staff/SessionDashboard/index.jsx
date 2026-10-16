import { SessionProvider } from "./context/SessionContext";
import SessionDashboardInner from "./components/SessionDashboardInner";

const SessionDashboard = () => (
  <SessionProvider live>
    <SessionDashboardInner />
  </SessionProvider>
);

export default SessionDashboard;
