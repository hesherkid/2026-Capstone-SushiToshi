import { Geist, Geist_Mono } from "next/font/google";
import "@/app/globals.css";

import Providers from "@/app/providers";
import { MATERIAL_SYMBOLS_URL } from "@/components/common/MaterialSymbol";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
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
      <body className={`${geistSans.variable} ${geistMono.variable}`}>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
