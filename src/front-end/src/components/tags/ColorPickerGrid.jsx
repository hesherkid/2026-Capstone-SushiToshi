import React from "react";
import { Box, Button, ButtonBase, useTheme } from "@mui/material";
import {
  Refresh as RefreshIcon,
  Check as CheckIcon,
} from "@mui/icons-material";

const ColorPickerGrid = ({ colors, selected, onSelect, onRefresh }) => {
  const theme = useTheme();

  return (
    <Box sx={{ width: "100%" }}>
      <Box sx={{ display: "flex", justifyContent: "flex-end", mb: 2 }}>
        <Button size="small" startIcon={<RefreshIcon />} onClick={onRefresh}>
          Refresh Colors
        </Button>
      </Box>
      <Box
        role="group"
        aria-label="Tag colour"
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "repeat(3, 1fr)", sm: "repeat(5, 1fr)" },
          gap: 1,
        }}
      >
        {colors.map((color) => {
          const isSelected = selected === color;
          const textColor = theme.palette.getContrastText(color);

          return (
            <ButtonBase
              key={color}
              onClick={() => onSelect(color)}
              aria-pressed={isSelected}
              aria-label={`Tag colour ${color}`}
              sx={{
                width: "100%",
                aspectRatio: "1 / 1",
                backgroundColor: color,
                color: textColor,
                borderRadius: 1,
                border: isSelected
                  ? `3px solid ${theme.palette.text.primary}`
                  : "1px solid rgba(0, 0, 0, 0.23)",
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
                gap: 0.25,
                fontSize: { xs: "0.75rem", sm: "0.9rem" },
                overflow: "hidden",
                padding: "2px",
                "&:hover": { opacity: 0.9 },
                "&.Mui-focusVisible": {
                  outline: `2px solid ${theme.palette.primary.main}`,
                  outlineOffset: "2px",
                },
              }}
            >
              {isSelected && <CheckIcon aria-hidden="true" fontSize="small" />}
              {color}
            </ButtonBase>
          );
        })}
      </Box>
    </Box>
  );
};

export default ColorPickerGrid;
