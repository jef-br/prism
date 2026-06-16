import type { Metadata } from "next";
import type { ReactNode } from "react";

import "../styles/PRISM-theme.css";
import "../styles/workbench.css";

export const metadata: Metadata = {
  title: "PRISM Workbench",
  description: "Web workbench for PRISM pipeline inspection."
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
