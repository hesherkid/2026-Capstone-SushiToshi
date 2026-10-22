"use client";

import React, { useEffect, useState } from "react";
import { decodeToken, isTokenExpired } from "@/config/auth";
import { getAuthToken, isGuestClaims } from "@/utils/auth";
import { useRouter } from "next/navigation";
import { api } from '@/config/api'
import useAuth from "@/hooks/useAuth";

const isGuestAccount = () => {
  if (typeof window === "undefined") {
    return false;
  }

  if (localStorage.getItem("guest") === "true") {
    return true;
  }

  const token = getAuthToken();
  if (!token || isTokenExpired(token)) {
    return false;
  }

  return isGuestClaims(decodeToken(token));
};

// Home page for customers (and signed-out visitors); guests are sent to the menu
const CustomerHome = () => {
  const [userName, setUserName] = useState(null);
  const [isGuest, setIsGuest] = useState(false);
  const [ready, setReady] = useState(false);
  const [locations, setLocations] = useState([]); // ← Changed from location to locations
  const [selectedLocation, setSelectedLocation] = useState(""); // ← Added this state
  const [selectedLocationName , setSelectedLocationName] = useState("")
  const [loading, setLoading] = useState(false); // ← Added loading state
  const router = useRouter();
  const { isAuthenticated: isAuthenticatedHook, userEmail } = useAuth();


useEffect(() => {
  const getLocation = async () => {
    try {
      setLoading(true);
      const response = await api.get("/location");
      setLocations(response.data);
      
      // Auto-select first location if only one exists
      if (response.data && response.data.length === 1) {
        setSelectedLocation(response.data[0].name);
      }
      
      // Use response.data instead of selectedLocation
      const locationId = Number(localStorage.getItem("locationId"));
      const filteredLocation = response.data.find(loc => loc.location_Id === locationId);
      
      // If you want to set it in state
      if (filteredLocation) {
        setSelectedLocationName(filteredLocation.name);
      }
      
    } catch (error) {
      console.error('Error fetching locations:', error);
    } finally {
      setLoading(false);
    }
  };

  getLocation();
}, []);


  useEffect(() => {
    const init = async () => {
      let name = null;
      let guestMode = false;

      // Check if user is authenticated via token
      const token = getAuthToken();
      if (token && !isTokenExpired(token)) {
        const claims = decodeToken(token);
        if (claims) {
          // Try to get name from different possible claim fields
          name = claims.name || 
                 claims.given_name || 
                 claims.email?.split('@')[0] || 
                 userEmail?.split('@')[0] ||
                 "User";
        }
      }

      // Check if guest mode
      if (!name && isGuestAccount()) {
        guestMode = true;
        name = "Guest";
      }

      setIsGuest(guestMode);
      setUserName(name);
      setReady(true);
    };

    init();
  }, [userEmail]);

  useEffect(() => {
    if (isGuestAccount()) {
      router.replace("/menu/full-menu");
    }
  }, [router, userEmail]);

  if (!ready) return null;
  if (isGuestAccount()) {
    return null;
  }

  return (
    <div>
      <div
        style={{
          padding: "3rem 2rem",
          maxWidth: "800px",
          margin: "0 auto",
          textAlign: "center",
        }}
      >
        <h1 style={{ fontSize: "2.2rem", marginBottom: "0.5rem", fontWeight: "600" }}>
          Welcome to Sushi Toshi{userName ? `, ${userName}` : ""}!
        </h1>

        <p style={{ color: "#555", fontSize: "1.1rem", marginBottom: "2rem" }}>
          All You Can Eat Authentic Japanese Food.
        </p>

      </div>
    </div>
  );
};

export default CustomerHome;