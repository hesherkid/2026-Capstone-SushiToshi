import api from "@/config/api";

// A signed-in customer's favourites and past orders (guests get 403). Each list is item IDs

export const fetchFavourites = async () => (await api.get("/me/favourites")).data;

export const addFavourite = (itemId) => api.put(`/me/favourites/${itemId}`);

export const removeFavourite = (itemId) => api.delete(`/me/favourites/${itemId}`);

// Most recently ordered first, from every visit
export const fetchOrderedBefore = async () => (await api.get("/me/ordered-before")).data;
