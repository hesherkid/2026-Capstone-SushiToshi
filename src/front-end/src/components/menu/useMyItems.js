import { useCallback, useEffect, useState } from "react";
import { decodeToken, extractRoles, isTokenExpired } from "@/config/auth";
import { getAuthToken, isGuestClaims } from "@/utils/auth";
import {
  addFavourite,
  fetchFavourites,
  fetchOrderedBefore,
  removeFavourite,
} from "@/services/myItems";

// Who's browsing the menu: favourites and "Order again" are for customers with an account
const whoIsBrowsing = () => {
  const token = getAuthToken();
  if (!token || isTokenExpired(token)) return { isCustomer: false, isGuest: false };
  const claims = decodeToken(token);
  const isGuest = localStorage.getItem("guest") === "true" || isGuestClaims(claims);
  return { isCustomer: !isGuest && extractRoles(claims).includes("Customer"), isGuest };
};

/**
 * The signed-in customer's favourite items and the items they've ordered before, as Sets of
 * item IDs. Guests and signed-out visitors get empty lists and canSave false.
 * toggleFavourite updates straight away and puts it back if the server says no.
 */
const useMyItems = ({ onError } = {}) => {
  const [canSave, setCanSave] = useState(false);
  const [isGuest, setIsGuest] = useState(false);
  const [favourites, setFavourites] = useState(() => new Set());
  const [orderedBefore, setOrderedBefore] = useState(() => new Set());

  useEffect(() => {
    const who = whoIsBrowsing();
    setIsGuest(who.isGuest);
    setCanSave(who.isCustomer);
    if (!who.isCustomer) return;

    let cancelled = false;
    // Either list failing just leaves it empty; the menu still works without them
    fetchFavourites()
      .then((ids) => !cancelled && setFavourites(new Set(ids)))
      .catch(() => {});
    fetchOrderedBefore()
      .then((ids) => !cancelled && setOrderedBefore(new Set(ids)))
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, []);

  const setFavourite = (itemId, on) =>
    setFavourites((current) => {
      const next = new Set(current);
      if (on) next.add(itemId);
      else next.delete(itemId);
      return next;
    });

  const toggleFavourite = useCallback(
    async (itemId) => {
      const wasFavourite = favourites.has(itemId);
      setFavourite(itemId, !wasFavourite);
      try {
        await (wasFavourite ? removeFavourite(itemId) : addFavourite(itemId));
      } catch {
        setFavourite(itemId, wasFavourite);
        onError?.("Couldn't update your favourites. Please try again.");
      }
    },
    [favourites, onError],
  );

  return { canSave, isGuest, favourites, orderedBefore, toggleFavourite };
};

export default useMyItems;
