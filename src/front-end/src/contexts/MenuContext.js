"use client";

import React, { createContext, useContext, useState } from "react";

const MenuContext = createContext();

export const useMenu = () => useContext(MenuContext);

export const MenuProvider = ({ children }) => {
  const [menuQuantities, setMenuQuantities] = useState({});

  const addItem = (menuName, id, name, price) => {
    setMenuQuantities((prevQuantities) => ({
      ...prevQuantities,
      [menuName]: {
        ...prevQuantities[menuName],
        [id]: {
          name,
          price,
          quantity: (prevQuantities[menuName]?.[id]?.quantity || 0) + 1,
        },
      },
    }));
  };

  const removeItem = (menuName, id) => {
    setMenuQuantities((prevQuantities) => {
      const menuItems = prevQuantities[menuName] || {};
      if (menuItems[id]?.quantity > 1) {
        return {
          ...prevQuantities,
          [menuName]: {
            ...menuItems,
            [id]: {
              ...menuItems[id],
              quantity: menuItems[id].quantity - 1,
            },
          },
        };
      } else {
        const { [id]: _, ...remainingItems } = menuItems;
        return { ...prevQuantities, [menuName]: remainingItems };
      }
    });
  };

  const resetQuantities = () => setMenuQuantities({});

  return (
    <MenuContext.Provider
      value={{ menuQuantities, addItem, removeItem, resetQuantities }}
    >
      {children}
    </MenuContext.Provider>
  );
};
