import api from "@/config/api";

// Canadian province and territory codes, as the API and canada-holidays.ca use them
export const PROVINCE_CODES = [
  "AB", "BC", "MB", "NB", "NL", "NS", "NT", "NU", "ON", "PE", "QC", "SK", "YT",
];

// Provinces, loadable years and defaults for the Holidays page
export const getHolidayOptions = async () => (await api.get("/holidays/options")).data;

export const listHolidays = async ({ province, year, status }) =>
  (await api.get("/holidays", { params: { province, year, status: status || undefined } })).data;

// Load button: new dates come back as Pending; dates already listed are skipped
export const importHolidays = async ({ province, year, includeOptional }) =>
  (await api.post("/holidays/import", {
    province_code: province,
    year,
    include_optional: includeOptional,
  })).data;

export const approveHolidays = async (ids) => (await api.post("/holidays/approve", { ids })).data;

export const rejectHolidays = async (ids) => (await api.post("/holidays/reject", { ids })).data;

// Added by hand, so approved straight away
export const addHoliday = async ({ province, date, name }) =>
  (await api.post("/holidays", { province_code: province, date, name })).data;

export const updateHoliday = async (holidayId, { date, name }) =>
  (await api.put(`/holidays/${holidayId}`, { date, name })).data;

export const deleteHoliday = async (holidayId) => {
  await api.delete(`/holidays/${holidayId}`);
};
