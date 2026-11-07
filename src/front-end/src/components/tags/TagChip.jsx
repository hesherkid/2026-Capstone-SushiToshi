import React from "react";
import { Chip } from "@mui/material";
import { X, Plus } from "lucide-react";
import PropTypes from "prop-types";

const FALLBACK_BG = "#cccccc";
const MIN_CONTRAST = 4.5;

export const parseHex = (hex) => {
  if (typeof hex !== "string") return null;
  let h = hex.trim().replace(/^#/, "");
  if (h.length === 3) {
    h = h
      .split("")
      .map((c) => c + c)
      .join("");
  }
  if (!/^[0-9a-f]{6}$/i.test(h)) return null;
  return [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16));
};

const toHex = (rgb) =>
  `#${rgb.map((v) => v.toString(16).padStart(2, "0")).join("")}`;

// WCAG relative luminance (sRGB)
const channel = (c) => {
  const s = c / 255;
  return s <= 0.04045 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
};

export const relativeLuminance = ([r, g, b]) =>
  0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);

export const contrastRatio = (rgbA, rgbB) => {
  const a = relativeLuminance(rgbA);
  const b = relativeLuminance(rgbB);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
};

// Prefer dark tint of the tag colour, or light grey, fall back to black or white
export const getContrastColor = (hexColor) => {
  const bg = parseHex(hexColor) ?? parseHex(FALLBACK_BG);
  const preferred = [bg.map((v) => Math.round(v * 0.3)), [0xeb, 0xeb, 0xeb]];
  const best = (candidates) =>
    candidates
      .map((rgb) => ({ rgb, ratio: contrastRatio(rgb, bg) }))
      .sort((x, y) => y.ratio - x.ratio)[0];

  const pick = best(preferred);
  if (pick.ratio >= MIN_CONTRAST) return toHex(pick.rgb);
  return toHex(
    best([
      [0, 0, 0],
      [255, 255, 255],
    ]).rgb,
  );
};

const getSizeStyles = (size = "l") => {
  switch (size) {
    case "s":
      return {
        height: "16px",
        "& .MuiChip-label": {
          px: 1,
          fontSize: "0.65rem",
          lineHeight: "14px",
          py: 0.25,
        },
        "& .MuiChip-deleteIcon": {
          width: "12px",
          height: "12px",
          margin: "0 2px",
        },
      };
    case "m":
      return {
        height: "20px",
        "& .MuiChip-label": {
          px: 1.25,
          fontSize: "0.75rem",
          lineHeight: "16px",
          py: 0.25,
        },
        "& .MuiChip-deleteIcon": {
          width: "14px",
          height: "14px",
          margin: "0 3px",
        },
      };
    case "xl":
      return {
        height: "32px",
        "& .MuiChip-label": {
          px: 1.5,
          fontSize: "1rem",
          lineHeight: "28px",
          py: 0.25,
        },
        "& .MuiChip-deleteIcon": {
          width: "18px",
          height: "18px",
          margin: "0 5px",
        },
      };
    case "l":
    default:
      return {
        height: "24px",
        "& .MuiChip-label": {
          px: 1.5,
          fontSize: "0.85rem",
          lineHeight: "20px",
          py: 0.25,
        },
        "& .MuiChip-deleteIcon": {
          width: "16px",
          height: "16px",
          margin: "0 4px",
        },
      };
  }
};

const getHitAreaStyles = (size, isActionChip) =>
  isActionChip && (size === "s" || size === "m")
    ? {
        position: "relative",
        "&::before": {
          content: '""',
          position: "absolute",
          left: 0,
          right: 0,
          top: "50%",
          height: "24px",
          transform: "translateY(-50%)",
        },
      }
    : {};

const TagChip = ({
  tag,
  isActionChip = false,
  isUsedFor,
  isSelected = false,
  onToggle,
  size = "l",
}) => {
  const toggle = (event) => {
    event?.stopPropagation();
    if (typeof onToggle === "function") onToggle(tag.tag_id);
  };

  const rawColor = tag.tag_color || tag.color_code;
  const tagColor = parseHex(rawColor) ? rawColor : FALLBACK_BG;
  const tagName = tag.tag_name || tag.name || "Unnamed Tag";
  const contrastColor = getContrastColor(tagColor);
  const iconSize = size === "s" ? 12 : size === "m" ? 14 : 16;

  const chipProps = {
    label: tagName,
    sx: {
      backgroundColor: tagColor,
      color: contrastColor,
      border: `1px solid ${contrastColor}40`,
      cursor: "pointer",
      fontWeight: 500,
      "&:hover": {
        opacity: 0.9,
        backgroundColor: tagColor,
      },
      "&.Mui-focusVisible": {
        backgroundColor: tagColor,
        outline: (theme) => `2px solid ${theme.palette.primary.main}`,
        outlineOffset: "2px",
      },
      "& .MuiChip-deleteIcon": {
        color: contrastColor,
        backgroundColor:
          isUsedFor === "menu-item" ? `${contrastColor}20` : "transparent",
        borderRadius: "50%",
        padding: isUsedFor === "menu-item" ? "2px" : "0",
        "&:hover": {
          color: contrastColor,
          backgroundColor:
            isUsedFor === "menu-item" ? `${contrastColor}40` : "transparent",
        },
      },
      ...getSizeStyles(size),
      ...getHitAreaStyles(size, isActionChip),
    },
  };

  if (isActionChip) {
    if (isUsedFor === "tag-select") {
      chipProps.onClick = toggle;
      chipProps.onDelete = toggle;
      chipProps["aria-label"] = tagName;
      chipProps["aria-pressed"] = isSelected;
      chipProps.deleteIcon = isSelected ? (
        <X size={iconSize} aria-hidden="true" />
      ) : (
        <Plus size={iconSize} aria-hidden="true" />
      );
    } else if (isUsedFor === "menu-item") {
      chipProps.onClick = toggle;
      chipProps.onDelete = toggle;
      chipProps["aria-label"] = `Remove ${tagName}`;
      chipProps.deleteIcon = <X size={iconSize} aria-hidden="true" />;
    }
  }

  return <Chip {...chipProps} />;
};

TagChip.propTypes = {
  tag: PropTypes.shape({
    tag_id: PropTypes.number.isRequired,
    tag_name: PropTypes.string,
    name: PropTypes.string,
    tag_color: PropTypes.string,
    color_code: PropTypes.string,
  }).isRequired,
  isActionChip: PropTypes.bool,
  isUsedFor: PropTypes.oneOf(["tag-select", "menu-item"]),
  isSelected: PropTypes.bool,
  onToggle: PropTypes.func,
  size: PropTypes.oneOf(["s", "m", "l", "xl"]),
};

export default TagChip;
