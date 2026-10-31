import { useState } from "react";
import { getTagMenuItems, removeTagFromItem } from "@/services/tagApi";
import { serverMessage } from "@/utils/serverMessage";

export const useTagMenuItems = () => {
  const [isOpen, setIsOpen] = useState(false);
  const [tag, setTag] = useState(null);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState(null);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);

  const openFor = async (nextTag) => {
    setTag(nextTag);
    setError(null);
    setSuccess(null);
    setIsOpen(true);

    setLoading(true);
    setLoadError(null);
    try {
      setItems(await getTagMenuItems(nextTag.tag_id));
    } catch (err) {
      setLoadError(err?.response?.data?.detail || "Failed to fetch menu items");
      setItems([]);
    } finally {
      setLoading(false);
    }
  };

  const close = () => {
    setIsOpen(false);
    setError(null);
    setSuccess(null);
  };

  const removeItem = async (item) => {
    setError(null);
    setSuccess(null);
    try {
      await removeTagFromItem(item.item_id, tag.tag_id);
      setItems((prev) => prev.filter((i) => i.item_id !== item.item_id));
      setSuccess(`Tag removed from ${item.name}`);
    } catch (err) {
      setError(serverMessage(err, "Failed to remove tag from item"));
    }
  };

  return {
    isOpen,
    tag,
    items,
    loading,
    loadError,
    error,
    success,
    openFor,
    close,
    removeItem,
    clearError: () => setError(null),
    clearSuccess: () => setSuccess(null),
  };
};

export default useTagMenuItems;
