import React, { useState } from "react";
import { Box, TextField, InputAdornment } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { generateColorSet } from "@/utils/colorGenerator";
import ColorPickerGrid from "./ColorPickerGrid";

const COLOR_CHOICES = 15;

const TagFormDialog = ({
  open,
  mode,
  tag,
  existingColors,
  onSave,
  onClose,
}) => {
  const isAdd = mode === "add";

  const [availableColors, setAvailableColors] = useState(() =>
    generateColorSet(COLOR_CHOICES, existingColors),
  );
  const [values, setValues] = useState(() =>
    isAdd
      ? { tag_name: "", tag_color: availableColors[0] ?? "" }
      : {
          tag_id: tag.tag_id,
          tag_name: tag.tag_name,
          tag_color: tag.tag_color,
        },
  );
  const [showColorPicker, setShowColorPicker] = useState(false);
  const [nameMissing, setNameMissing] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [saving, setSaving] = useState(false);

  const nameBlank = !values.tag_name.trim();

  const handleColorSelect = (color) => {
    setValues((prev) => ({ ...prev, tag_color: color }));
    setShowColorPicker(false);
  };

  const handleRefreshColors = () => {
    const newColors = generateColorSet(COLOR_CHOICES, existingColors);
    setAvailableColors(newColors);
    if (newColors.length > 0) {
      setValues((prev) => ({ ...prev, tag_color: newColors[0] }));
    }
  };

  const handleSubmit = async () => {
    if (nameBlank) {
      setNameMissing(true);
      return;
    }
    setSaving(true);
    try {
      await onSave(values);
    } catch (err) {
      setSaveError(err?.response?.data?.detail || `Failed to ${mode} tag`);
    } finally {
      setSaving(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={isAdd ? "Add New Tag" : "Update Tag"}
      error={saveError}
      onErrorClose={() => setSaveError(null)}
      onSubmit={handleSubmit}
      submitLabel={isAdd ? "Save" : "Update"}
      busyLabel="Saving..."
      busy={saving}
    >
      <TextField
        autoFocus
        label="Tag Name"
        value={values.tag_name}
        onChange={(e) =>
          setValues((prev) => ({ ...prev, tag_name: e.target.value }))
        }
        fullWidth
        error={nameMissing && nameBlank}
        helperText={nameMissing && nameBlank ? "Tag name is required" : ""}
      />
      <TextField
        label="Color"
        value={values.tag_color}
        onClick={() => setShowColorPicker(true)}
        slotProps={{
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <Box
                  sx={{
                    width: 24,
                    height: 24,
                    backgroundColor: values.tag_color,
                    borderRadius: 1,
                    border: "1px solid rgba(0, 0, 0, 0.23)",
                    cursor: "pointer",
                  }}
                />
              </InputAdornment>
            ),
            readOnly: true,
            sx: { cursor: "pointer" },
          },
        }}
        fullWidth
      />

      {showColorPicker && (
        <ColorPickerGrid
          colors={availableColors}
          selected={values.tag_color}
          onSelect={handleColorSelect}
          onRefresh={handleRefreshColors}
        />
      )}
    </FormDialog>
  );
};

export default TagFormDialog;
