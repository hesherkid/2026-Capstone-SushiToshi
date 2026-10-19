import TableRestaurant from "@mui/icons-material/TableRestaurant";

// Material UI's table icon, sized with `size` like the lucide icons around it
const TableIcon = ({ size = 24, sx, ...props }) => (
  <TableRestaurant sx={{ fontSize: size, ...sx }} {...props} />
);

export default TableIcon;
