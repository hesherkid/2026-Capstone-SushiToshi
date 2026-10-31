import React from "react";
import { Box, Button } from "@mui/material";
import { Refresh as RefreshIcon } from "@mui/icons-material";

const ColorPickerGrid = ({ colors, selected, onSelect, onRefresh }) => (
  <Box sx={{ width: "100%" }}>
    <Box sx={{ display: "flex", justifyContent: "flex-end", mb: 2 }}>
      <Button size="small" startIcon={<RefreshIcon />} onClick={onRefresh}>
        Refresh Colors
      </Button>
    </Box>
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: { xs: "repeat(3, 1fr)", sm: "repeat(5, 1fr)" },
        gap: 1,
      }}
    >
      {colors.map((color) => (
        <Box
          key={color}
          onClick={() => onSelect(color)}
          sx={{ position: "relative", width: "100%", paddingBottom: "100%" }}
        >
          <Box
            sx={{
              position: "absolute",
              top: 0,
              left: 0,
              right: 0,
              bottom: 0,
              backgroundColor: color,
              borderRadius: 1,
              cursor: "pointer",
              border:
                selected === color
                  ? "2px solid black"
                  : "1px solid rgba(0, 0, 0, 0.23)",
              "&:hover": { opacity: 0.9 },
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: { xs: "0.75rem", sm: "0.9rem" },
              overflow: "hidden",
              padding: "2px",
            }}
          >
            {color}
          </Box>
        </Box>
      ))}
    </Box>
  </Box>
);

export default ColorPickerGrid;
