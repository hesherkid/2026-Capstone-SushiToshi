"use client";

import { useState } from "react";
import { Button, ListItemText, Menu, MenuItem } from "@mui/material";
import { Download } from "lucide-react";

// Text a spreadsheet would treat as a formula (OWASP CSV injection guidance).
const FORMULA_START = /^[=+\-@\t\r]/;

const csvCell = (value) => {
  if (value === null || value === undefined) return "";
  let text = String(value);
  if (typeof value === "string" && FORMULA_START.test(text)) text = `'${text}`;
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
};

const triggerDownload = (fileName, href) => {
  const link = document.createElement("a");
  link.href = href;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
};

export const downloadCsv = (fileName, rows) => {
  const csv = rows.map((row) => row.map(csvCell).join(",")).join("\r\n");
  // The BOM tells Excel the file is UTF-8, so characters like "–" and "é" display correctly.
  const url = URL.createObjectURL(
    new Blob(["\uFEFF", csv], { type: "text/csv;charset=utf-8" }),
  );
  triggerDownload(`${fileName}.csv`, url);
  setTimeout(() => URL.revokeObjectURL(url), 1000);
};

//Saves a Chart.js chart as a PNG.
export const downloadChartPng = (chart, fileName) => {
  if (!chart) return;
  const source = chart.canvas;
  const canvas = document.createElement("canvas");
  canvas.width = source.width;
  canvas.height = source.height;
  const context = canvas.getContext("2d");
  context.fillStyle = "#ffffff";
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.drawImage(source, 0, 0);
  triggerDownload(`${fileName}.png`, canvas.toDataURL("image/png"));
};

// Opens the browser's print dialog, where "Save as PDF" is also available.
export const printReport = () => {
  setTimeout(() => window.print(), 300);
};

export const exportFileName = (report, part, range) =>
  [report, part, range && `${range.from}_to_${range.to}`]
    .filter(Boolean)
    .join("_");

export const ExportMenu = ({ label = "Export", options }) => {
  const [anchorEl, setAnchorEl] = useState(null);
  const close = () => setAnchorEl(null);

  return (
    <>
      <Button
        size="small"
        variant="outlined"
        startIcon={<Download size={16} />}
        aria-haspopup="menu"
        aria-expanded={Boolean(anchorEl)}
        onClick={(e) => setAnchorEl(e.currentTarget)}
        sx={{ displayPrint: "none" }}
      >
        {label}
      </Button>
      <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={close}>
        {options.map((option) => (
          <MenuItem
            key={option.label}
            disabled={option.disabled}
            onClick={() => {
              close();
              option.onClick();
            }}
          >
            <ListItemText
              primary={option.label}
              secondary={option.description}
            />
          </MenuItem>
        ))}
      </Menu>
    </>
  );
};

export default ExportMenu;
