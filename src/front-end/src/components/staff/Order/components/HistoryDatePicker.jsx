import dayjs from "dayjs";
import { DatePicker } from "@mui/x-date-pickers/DatePicker";
import { PickersDay } from "@mui/x-date-pickers/PickersDay";

const DATE_FORMAT = "YYYY-MM-DD";

// A calendar day with a dot under the number when that day has orders
const OrderDay = ({ orderDays, ...props }) => {
  const { day, outsideCurrentMonth } = props;
  const hasOrders =
    !outsideCurrentMonth && orderDays.has(day.format(DATE_FORMAT));

  if (!hasOrders) return <PickersDay {...props} />;

  return (
    <PickersDay
      {...props}
      data-has-orders="true"
      aria-label={`${day.format("MMMM D")}, has orders`}
      title="Has orders"
      sx={{
        fontWeight: 700,
        position: "relative",
        "&::after": {
          content: '""',
          position: "absolute",
          bottom: 4,
          left: "50%",
          transform: "translateX(-50%)",
          width: 5,
          height: 5,
          borderRadius: "50%",
          backgroundColor: "primary.main",
        },
        "&.Mui-selected::after": { backgroundColor: "primary.contrastText" },
      }}
    />
  );
};

// A date field for Order History whose calendar marks the days that have orders.
// value and onChange use "yyyy-MM-dd" strings, the same as the filters and the API
const HistoryDatePicker = ({
  id,
  label,
  value,
  onChange,
  minDate,
  maxDate,
  error,
  helperText,
  orderDays,
  loadMonth,
  isLoadingDays,
}) => {
  const parsed = value ? dayjs(value) : null;

  return (
    <DatePicker
      label={label}
      value={parsed}
      onChange={(next) =>
        onChange(next && next.isValid() ? next.format(DATE_FORMAT) : "")
      }
      minDate={minDate ? dayjs(minDate) : undefined}
      maxDate={maxDate ? dayjs(maxDate) : undefined}
      // The calendar opens on the selected month, or this month when the field is empty
      onOpen={() => loadMonth((parsed?.isValid() ? parsed : dayjs()).toDate())}
      onMonthChange={(month) => loadMonth(month.toDate())}
      onYearChange={(year) => loadMonth(year.toDate())}
      loading={isLoadingDays}
      enableAccessibleFieldDOMStructure={false}
      slots={{ day: OrderDay }}
      slotProps={{
        day: { orderDays },
        textField: {
          id,
          size: "small",
          error,
          helperText,
          sx: { width: 170 },
        },
      }}
    />
  );
};

export default HistoryDatePicker;
