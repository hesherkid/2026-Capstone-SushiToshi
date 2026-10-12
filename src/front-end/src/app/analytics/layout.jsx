import AnalyticsNav from "@/components/analytics/AnalyticsNav";

export default function AnalyticsLayout({ children }) {
  return (
    <>
      <AnalyticsNav />
      {children}
    </>
  );
}
