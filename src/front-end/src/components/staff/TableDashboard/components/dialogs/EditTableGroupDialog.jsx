import { useState, useEffect } from "react";
import { TextField } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { useTable } from "../../context/TableContext";
import api from "@/config/api";
import serverMessage from "@/utils/serverMessage";

const EditTableGroupDialog = ({ open, group }) => {
  const { closeDialog, refreshData } = useTable();
  const [groupName, setGroupName] = useState("");
  const [error, setError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (group) setGroupName(group.group_Name || "");
  }, [group]);

  const handleClose = () => {
    setError(null);
    closeDialog();
  };

  const handleSubmit = async () => {
    setIsSubmitting(true);
    setError(null);
    try {
      await api.put(`/TableGroup/${group.tableGroup_Id}`, { Group_Name: groupName.trim() });
      await refreshData();
      handleClose();
    } catch (err) {
      setError(serverMessage(err, "Couldn't update the table group."));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={handleClose}
      title="Edit Table Group"
      subtitle={group?.group_Name}
      error={error}
      onErrorClose={() => setError(null)}
      onSubmit={handleSubmit}
      submitLabel="Save Changes"
      busyLabel="Saving..."
      busy={isSubmitting}
      submitDisabled={!groupName.trim()}
    >
      <TextField
        label="Group Name"
        required
        autoFocus
        value={groupName}
        onChange={(e) => setGroupName(e.target.value)}
      />
    </FormDialog>
  );
};

export default EditTableGroupDialog;
