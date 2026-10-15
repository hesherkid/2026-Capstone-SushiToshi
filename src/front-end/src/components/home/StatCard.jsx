import React from "react";
import NextLink from "next/link";
import { Card, CardActionArea, Box, Typography, Skeleton } from "@mui/material";
import { alpha } from "@mui/material/styles";

export const StatCard = ({ label, value, icon: Icon, href, highlight = false, loading = false }) => {
  const content = (
    <Box
      sx={{
        display: "flex",
        flexDirection: { xs: "column", sm: "row" },
        alignItems: "center",
        textAlign: { xs: "center", sm: "left" },
        gap: { xs: 1, sm: 2 },
        p: { xs: 1.5, sm: 2.5 },
      }}
    >
      <Box
        sx={{
          width: { xs: 36, sm: 48 },
          height: { xs: 36, sm: 48 },
          borderRadius: 2,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          flexShrink: 0,
          bgcolor: highlight ? "warning.main" : "primary.main",
          color: "common.white",
        }}
      >
        <Icon size={24} aria-hidden />
      </Box>
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="h4" component="p" sx={{ fontWeight: 700, lineHeight: 1.1 }}>
          {loading ? <Skeleton width={40} /> : value}
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ fontSize: { xs: "0.75rem", sm: "0.875rem" } }}>
          {label}
        </Typography>
      </Box>
    </Box>
  );

  return (
    <Card
      variant="outlined"
      sx={{
        height: "100%",
        borderColor: highlight ? "warning.main" : "divider",
        bgcolor: (theme) =>
          highlight ? alpha(theme.palette.warning.main, 0.08) : theme.palette.background.paper,
      }}
    >
      {href ? (
        <CardActionArea component={NextLink} href={href} sx={{ height: "100%" }}>
          {content}
        </CardActionArea>
      ) : (
        content
      )}
    </Card>
  );
};

export default StatCard;
