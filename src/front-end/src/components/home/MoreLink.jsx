import React from "react";
import NextLink from "next/link";
import { Link } from "@mui/material";

// Home page alerts only preview the first few rows; the rest live on the full page
export const PREVIEW_LIMIT = 5;

// "+3 more" link under a preview list; renders nothing when nothing is hidden
export const MoreLink = ({ hiddenCount, href, noun }) => {
  if (hiddenCount <= 0) return null;
  return (
    <Link
      component={NextLink}
      href={href}
      color="inherit"
      underline="always"
      sx={{ display: "inline-block", mt: 0.5, fontWeight: 600, fontSize: "0.875rem" }}
    >
      +{hiddenCount} more {noun}
    </Link>
  );
};

export default MoreLink;
