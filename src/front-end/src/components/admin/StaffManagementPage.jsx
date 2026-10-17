import React, { useState, useEffect } from "react";
import { Dialog } from "@mui/material";
import StaffManagementForm from "@/components/admin/StaffManagementForm";
import { getStaffColumns, isActiveAccount } from "@/components/admin/StaffList";
import PasswordChangeDialog from "@/components/admin/PasswordChangeDialog";
import api from "@/config/api";
import ManagementPage from "@/components/common/ManagementPage";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import { useAuth } from "@/hooks/useAuth";

// TODO: Missing Staff api

const StaffManagementPage = () => {
  const [staff, setStaff] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [formOpen, setFormOpen] = useState(false);
  const [passwordDialogOpen, setPasswordDialogOpen] = useState(false);
  const [selectedStaff, setSelectedStaff] = useState(null);
  const [editMode, setEditMode] = useState(false);
  const [successMessage, setSuccessMessage] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [staffToDeactivate, setStaffToDeactivate] = useState(null);
  const { userRole, userEmail } = useAuth();
  const isAdmin = userRole === "admin";
  const isSelf = (staff) =>
    !!userEmail && staff?.email?.toLowerCase() === userEmail.toLowerCase();
  const fetchStaff = async () => {
    try {
      setLoading(true);

      const response = await api.get("/staff");

      if (response.status !== 200)
        throw new Error("Failed to fetch staff members");

      const data = response.data;
      setStaff(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchStaff();
  }, []);

  const handleStatusChange = async (staffMember, newStatus) => {
    try {
      setSubmitting(true);
      setError(null);

      await api.put(`/staff/${staffMember.user_id}/status`, {
        status: newStatus,
      });

      await fetchStaff();

      setSuccessMessage(`Staff member ${
          newStatus === "Active" ? "reactivated" : "deactivated"
        } successfully`);
    } catch (err) {
      console.error("Error updating staff status:", err);
      const detail = err.response?.data;
      setError(
        typeof detail === "string" && detail
          ? detail
          : "Failed to update staff member status"
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleCreateStaff = async (formData) => {
    try {
      setSubmitting(true);

      let role = formData.role;
      formData.status = "active";
      // delete formData.role;

      // await api.post(`/staff/?role=${role}`, formData);
      await api.post(`/staff/`, formData);
      await fetchStaff();
      setFormOpen(false);
      setSuccessMessage("Staff member created successfully");
    } catch (err) {
      console.error("Error creating staff member:", err);
      setError(err.response?.data?.detail || "Failed to create staff member");
    } finally {
      setSubmitting(false);
    }
  };

  const handleUpdateStaff = async (formData) => {
    try {
      setSubmitting(true);

      const response = await api.put(
        `/staff/${selectedStaff.user_id}`,
        formData
      );

      if (response.status !== 200)
        throw new Error("Failed to update staff member");

      await fetchStaff();
      setFormOpen(false);
      setSelectedStaff(null);
      setEditMode(false);
      setSuccessMessage("Staff member updated successfully");
    } catch (err) {
      // Keep the form in edit mode so the admin can fix and resubmit
      const detail = err.response?.data;
      setError(typeof detail === "string" && detail ? detail : err.message);
    } finally {
      setSubmitting(false);
    }
  };

  const handleChangePassword = async (passwordData) => {
    try {
      setSubmitting(true);

      const response = await api.post(
        `/staff/${selectedStaff.user_id}/reset-password`,
        { new_password: passwordData.new_password }
      );
      if (response.status !== 200) {
        throw new Error(response.data?.detail || "Failed to reset password");
      }

      setPasswordDialogOpen(false);
      setSuccessMessage("Password reset successfully");
    } catch (err) {
      setError(err.message);
    } finally {
      setSubmitting(false);
      setSelectedStaff(null);
    }
  };

  return (
    <ManagementPage
      title="Staff Management"
      addLabel="Create New Staff Account"
      onAdd={() => {
        setEditMode(false);
        setSelectedStaff(null);
        setFormOpen(true);
      }}
      error={formOpen || passwordDialogOpen ? null : error}
      onErrorClose={() => setError(null)}
      success={successMessage}
      onSuccessClose={() => setSuccessMessage(null)}
      gridProps={{
        rows: staff,
        columns: getStaffColumns({
          onEdit: (staff) => {
            setSelectedStaff(staff);
            setEditMode(true);
            setFormOpen(true);
          },
          onChangePassword: (staff) => {
            setSelectedStaff(staff);
            setPasswordDialogOpen(true);
          },
          // Deactivating asks first; reactivating happens straight away
          onToggleStatus: isAdmin
            ? (staff) =>
                isActiveAccount(staff)
                  ? setStaffToDeactivate(staff)
                  : handleStatusChange(staff, "Active")
            : undefined,
          isSelf,
        }),
        loading,
        getRowId: (row) => row.user_id,
      }}
    >
      <Dialog
        open={formOpen}
        onClose={() => setFormOpen(false)}
        maxWidth="sm"
        fullWidth
      >
        <StaffManagementForm
          initialData={editMode ? selectedStaff : null}
          onSubmit={editMode ? handleUpdateStaff : handleCreateStaff}
          isLoading={submitting}
          error={error}
          mode={editMode ? "edit" : "create"}
          lockRole={editMode && isSelf(selectedStaff)}
        />
      </Dialog>

      <ConfirmDialog
        open={!!staffToDeactivate}
        title="Deactivate account"
        message={
          staffToDeactivate &&
          `Deactivate ${staffToDeactivate.first_name} ${staffToDeactivate.last_name}? They won't be able to sign in until you reactivate them.`
        }
        confirmLabel="Deactivate"
        onConfirm={() => handleStatusChange(staffToDeactivate, "Inactive")}
        onClose={() => setStaffToDeactivate(null)}
      />

      <PasswordChangeDialog
        open={passwordDialogOpen}
        onClose={() => setPasswordDialogOpen(false)}
        onSubmit={handleChangePassword}
        staffMember={selectedStaff}
        isLoading={submitting}
        error={error}
      />

    </ManagementPage>
  );
};

export default StaffManagementPage;