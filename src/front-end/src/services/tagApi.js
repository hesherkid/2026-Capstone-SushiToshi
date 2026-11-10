import api from "@/config/api";

// Tag endpoints used by the tag management screen.
// axios rejects any status outside 200-299, so failures arrive as errors.

export const getAllTags = () => api.get("/Tag").then((res) => res.data);

export const createTag = (tag) => api.post("/Tag", tag).then((res) => res.data);

// The id goes in the URL only: in development the API rejects keys TagUpdateDto doesn't have
export const updateTag = ({ tag_id, tag_name, tag_color }) =>
  api.put(`/Tag/${tag_id}`, { tag_name, tag_color }).then((res) => res.data);

export const deleteTag = (tagId) => api.delete(`/tag/${tagId}`);

export const getTagMenuItems = (tagId) =>
  api.get(`/tag/${tagId}/menu-items`).then((res) => res.data);

// This route lives on MenuItemController, but only this screen uses it
export const removeTagFromItem = (itemId, tagId) =>
  api.delete(`/menuItem/${itemId}/tags/${tagId}`);
