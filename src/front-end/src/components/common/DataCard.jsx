import React from "react";
import { Card, CardContent } from "@mui/material";

export const DataCard = ({ children, elevation = 1, sx = {} }) => {
  return (
    <Card
      elevation={elevation}
      sx={{
        ...sx,
      }}
    >
      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
        {children}
      </CardContent>
    </Card>
  );
};
