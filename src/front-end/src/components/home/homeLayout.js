// Responsive card grid
export const gridColumns = (min) => ({
  display: "grid",
  gap: 2,
  gridTemplateColumns: `repeat(auto-fill, minmax(min(100%, ${min}px), 1fr))`,
});

export const statsGrid = {
  display: "grid",
  gap: { xs: 1, sm: 2 },
  gridTemplateColumns: "repeat(3, minmax(0, 1fr))",
};

export const todayLabel = () =>
  new Date().toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric" });
