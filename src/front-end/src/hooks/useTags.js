import { useCallback, useEffect, useState } from "react";
import { getAllTags, createTag, updateTag, deleteTag } from "@/services/tagApi";

export const useTags = () => {
  const [tags, setTags] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      setTags(await getAllTags());
    } catch {
      setError("Failed to fetch tags");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const create = async (tag) => {
    await createTag(tag);
    refresh();
  };

  const update = async (tag) => {
    await updateTag(tag);
    refresh();
  };

  const remove = async (tagId) => {
    await deleteTag(tagId);
    refresh();
  };

  const clearError = () => setError(null);

  return { tags, loading, error, clearError, create, update, remove };
};

export default useTags;
