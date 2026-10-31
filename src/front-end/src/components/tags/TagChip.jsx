import React from "react";
import { Chip } from "@mui/material";
import { X, Plus } from "lucide-react";
import PropTypes from "prop-types";

const getContrastColor = (hexColor) => {
  // Add safety check for undefined or invalid hex color
  if (!hexColor || typeof hexColor !== "string" || !hexColor.startsWith("#")) {
    return "#EBEBEB"; // Return default color if invalid
  }

  const r = parseInt(hexColor.slice(1, 3), 16);
  const g = parseInt(hexColor.slice(3, 5), 16);
  const b = parseInt(hexColor.slice(5, 7), 16);

  const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

  if (luminance < 0.61) {
    return "#EBEBEB";
  }

  const factor = 0.3;
  const newR = Math.round(r * factor);
  const newG = Math.round(g * factor);
  const newB = Math.round(b * factor);
  const newColor = `#${newR.toString(16).padStart(2, "0")}${newG
    .toString(16)
    .padStart(2, "0")}${newB.toString(16).padStart(2, "0")}`;
  return newColor;
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
  }
};

const TagChip = ({
  tag,
  isActionChip,
  isUsedFor,
  isSelected,
  onToggle,
  size = "l",
}) => {
  const handleClick = () => {
    if (onToggle && typeof onToggle === "function") {
      onToggle(tag.tag_id);
    }
  };

  const handleDelete = (event) => {
    event.stopPropagation();
    if (onToggle && typeof onToggle === "function") {
      onToggle(tag.tag_id);
    }
  };

  // Support both naming conventions: tag_color/color_code and tag_name/name
  const tagColor = tag.tag_color || tag.color_code || "#cccccc";
  const tagName = tag.tag_name || tag.name || "Unnamed Tag";

  const contrastColor = getContrastColor(tagColor);

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
      "& .MuiChip-deleteIcon": {
        color: contrastColor,
        backgroundColor:
          isUsedFor === "menu-item" ? `${contrastColor}20` : "transparent",
        borderRadius: "50%",
        padding: isUsedFor === "menu-item" ? "2px" : "0",
        "&:hover": {
          backgroundColor:
            isUsedFor === "menu-item" ? `${contrastColor}40` : "transparent",
        },
      },
      ...getSizeStyles(size),
    },
  };

  if (isActionChip) {
    if (isUsedFor === "tag-select") {
      chipProps.onClick = handleClick;
      chipProps.deleteIcon = isSelected ? (
        <X size={size === "s" ? 12 : size === "m" ? 14 : 16} />
      ) : (
        <Plus size={size === "s" ? 12 : size === "m" ? 14 : 16} />
      );
      chipProps.onDelete = handleDelete;
    } else if (isUsedFor === "menu-item") {
      chipProps.onDelete = handleDelete;
      chipProps.deleteIcon = (
        <X size={size === "s" ? 12 : size === "m" ? 14 : 16} />
      );
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

TagChip.defaultProps = {
  isActionChip: false,
  isSelected: false,
  size: "l",
};

export default TagChip;
