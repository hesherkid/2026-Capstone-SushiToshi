import { Montserrat, Open_Sans } from "next/font/google";
import "@/app/globals.css";

import Providers from "@/app/providers";
import { MATERIAL_SYMBOLS_URL } from "@/components/common/MaterialSymbol";

// Self-hosted by next/font; the theme and globals.css use these through the CSS variables
const montserrat = Montserrat({
  variable: "--font-montserrat",
  subsets: ["latin"],
});

const openSans = Open_Sans({
  variable: "--font-open-sans",
  subsets: ["latin"],
});

// Pages set a short title ("Orders") and the template adds the app name
export const metadata = {
  title: {
    default: "Sushi Toshi",
    template: "%s | Sushi Toshi",
  },
};

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <head>
        {/* Google Material Symbols for the customer pages' icons */}
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link rel="stylesheet" href={MATERIAL_SYMBOLS_URL} />
      </head>
      <body className={`${montserrat.variable} ${openSans.variable}`}>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
