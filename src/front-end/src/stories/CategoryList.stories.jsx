import React, { useState } from "react";
import CategoryList from "@/components/admin/CategoryList";
import CategoryManagementDialog from "@/components/admin/CategoryManagementDialog";
import { http, HttpResponse, delay } from "msw";
import api from "@/config/api";

// 0 = unlimited; toddler 0 = same as child; enums are strings
const category = (category_id, category_name, description, limits = {}) => ({
  category_id,
  category_name,
  description,
  image_url: null,
  total_views: 0,
  total_view_seconds: 0,
  last_viewed_at: "0001-01-01T00:00:00",
  adult_limit: 0,
  child_limit: 0,
  senior_limit: 0,
  toddler_limit: 0,
  per_order_limit: 0,
  over_limit_behavior: "Block",
  menuItems: [],
  ...limits,
});

const mockCategories = [
  category(
    1,
    "Sashimi",
    "Fresh sliced raw fish, ordered by the piece. Maximum 30 pieces per order.",
    { per_order_limit: 30 },
  ),
  category(
    2,
    "Nigiri",
    "Hand-pressed sushi and gunkan. 1 piece of each type per order.",
  ),
  category(3, "Maki", "Small rolls. 2 of each type per order."),
  category(
    4,
    "Rolls",
    "Inside-out and specialty rolls. 2 of each type per order.",
  ),
  category(
    5,
    "Hot Stone",
    "Cooked at your table on a hot stone. Maximum 1 per person; $5 for each extra item.",
    {
      adult_limit: 1,
      child_limit: 1,
      senior_limit: 1,
      over_limit_behavior: "Charge",
    },
  ),
  category(6, "Cooked/Hot", "Appetizers, soups, and salads."),
  category(
    7,
    "Add On",
    "Premium sashimi, nigiri, rolls, and ice cream charged per piece.",
  ),
];

api.get = async (url) => {
  if (url.toLowerCase() === "/category") {
    return { data: mockCategories };
  }
  if (url.startsWith("/Category/get_menu_by_category/")) {
    const error = new Error("Not found");
    error.response = {
      status: 404,
      data: "No menu items found for the specified category.",
    };
    throw error;
  }
  return { data: [] };
};
api.put = async (url, data) => {
  if (url.startsWith("/Category/update_category/")) {
    const id = parseInt(url.split("/").pop());
    return { data: { ...data, category_id: id } };
  }
  return { data: {} };
};
api.post = async (url, data) => {
  if (url === "/Category") {
    return { data: { ...data, category_id: 999 } };
  }
  return { data: {} };
};
api.delete = async (url) => {
  if (url.startsWith("/Category/delete_category/")) {
    return { data: "Category deleted successfully." };
  }
  return { data: {} };
};

const meta = {
  title: "Admin/CategoryList",
  component: CategoryList,
  parameters: {
    msw: [
      http.get("/category", async () => {
        await delay(200);
        return HttpResponse.json(mockCategories, { status: 200 });
      }),
      http.put(
        "/Category/update_category/:categoryId",
        async ({ request, params }) => {
          await delay(200);
          const body = await request.json();
          return HttpResponse.json(
            { ...body, category_id: parseInt(params.categoryId) },
            { status: 200 },
          );
        },
      ),
      http.post("/Category", async ({ request }) => {
        await delay(200);
        const body = await request.json();
        return HttpResponse.json(
          { ...body, category_id: 999 },
          { status: 200 },
        );
      }),
      http.delete("/Category/delete_category/:categoryId", async () => {
        await delay(200);
        return HttpResponse.json("Category deleted successfully.", {
          status: 200,
        });
      }),
    ],
  },
};

export default meta;

export const Default = (args) => {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState(null);

  const handleEditCategory = (category) => {
    setSelectedCategory(category);
    setDialogOpen(true);
  };

  const handleCreateCategory = () => {
    setSelectedCategory(null);
    setDialogOpen(true);
  };

  const handleDialogClose = () => {
    setDialogOpen(false);
    setSelectedCategory(null);
  };

  return (
    <>
      <CategoryList
        onEditCategory={handleEditCategory}
        onCreateCategory={handleCreateCategory}
        {...args}
      />
      <CategoryManagementDialog
        open={dialogOpen}
        onClose={handleDialogClose}
        selectedCategory={selectedCategory}
        onSuccess={handleDialogClose}
      />
    </>
  );
};
