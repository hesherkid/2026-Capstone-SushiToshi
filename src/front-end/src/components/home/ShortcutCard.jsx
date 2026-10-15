import React from "react";
import NextLink from "next/link";
import { Card, CardActionArea, Box, Typography, Badge } from "@mui/material";
import { visuallyHidden } from "@mui/utils";
import { ChevronRight } from "lucide-react";

export const ShortcutCard = ({ title, description, href, icon: Icon, badge = 0 }) => {
  return (
    <Card variant="outlined" sx={{ height: "100%" }}>
      <CardActionArea
        component={NextLink}
        href={href}
        sx={{ height: "100%", p: 2, display: "flex", alignItems: "center", gap: 2 }}
      >
        <Badge
          badgeContent={badge > 0 ? badge : null}
          color="warning"
          max={99}
          slotProps={{ badge: { "aria-hidden": true } }}
        >
          <Box
            sx={{
              width: 44,
              height: 44,
              borderRadius: 2,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              bgcolor: "action.hover",
              color: "primary.main",
            }}
          >
            <Icon size={22} aria-hidden />
          </Box>
        </Badge>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography sx={{ fontWeight: 600 }}>
            {title}
            {badge > 0 && (
              <Box component="span" sx={visuallyHidden}>
                {` (${badge} pending)`}
              </Box>
            )}
          </Typography>
          {description && (
            <Typography variant="body2" color="text.secondary">
              {description}
            </Typography>
          )}
        </Box>
        <ChevronRight size={20} aria-hidden />
      </CardActionArea>
    </Card>
  );
};

export default ShortcutCard;
