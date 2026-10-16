import { TableProvider } from "./context/TableContext";
import TableDashboardInner from "./components/TableDashboardInner";

const TableDashboard = () => (
  <TableProvider>
    <TableDashboardInner />
  </TableProvider>
);

export default TableDashboard;
