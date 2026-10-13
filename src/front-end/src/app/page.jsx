"use client";

import React, { useEffect, useState } from "react";
import { decodeToken, isTokenExpired } from "@/config/auth";
import { getAuthToken } from "@/utils/auth";
import { useRouter } from "next/navigation";
import { api } from '@/config/api'
import useAuth from "@/hooks/useAuth";
import { loginUser } from "@/utils/auth";

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

  const claims = decodeToken(token);
  const email = (claims?.email || claims?.["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"] || "").toLowerCase();
  return email === "guestemail@email.com";
};

const HomePage = () => {
  const [userName, setUserName] = useState(null);
  const [isGuest, setIsGuest] = useState(false);
  const [ready, setReady] = useState(false);
  const [tableNumber, setTableNumber] = useState("");
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
      console.log(`Locations: ${JSON.stringify(response.data, null, 2)}`);
      
      // Auto-select first location if only one exists
      if (response.data && response.data.length === 1) {
        setSelectedLocation(response.data[0].name);
      }
      
      // Use response.data instead of selectedLocation
      const locationId = Number(localStorage.getItem("locationId"));
      const filteredLocation = response.data.find(loc => loc.location_Id === locationId);
      console.log('Filtered location:', filteredLocation);
      
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

  const handleContinueAsGuest = async() => {
    const response = await loginUser(guestEmail,guestPassword);

    console.log("AfterLogin");
    router.push("/");
    localStorage.setItem("guest", "true");
    setIsGuest(true);
    setUserName("Guest");
    setReady(true);
  };

  const handleJoinTable = () => {
    if (!selectedLocation) {
      alert("Please select a location.");
      return;
    }
    if (!tableNumber.trim()) {
      alert("Please enter a table number.");
      return;
    }
    router.push(`/join?location=${selectedLocation}&table=${tableNumber.trim()}`);
  };

  if (!ready) return null;
  if (isGuestAccount()) {
    return null;
  }

  const showGuestButton = !userName && !isGuest;

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

        {/* Show join table section for both guests AND logged-in users */}
        {(isGuest || userName) && (
          <div style={{ marginTop: "2rem" }}>
            {localStorage.getItem("tableNumber") ? (
              <>
                <h3>Welcome To {selectedLocationName} </h3>
                <h3>
                  You're seated at table: {localStorage.getItem("tableNumber")}
                  {/* {localStorage.getItem("locationId") && ` at location ${localStorage.getItem("locationId")}`} */}
                </h3>
                <div style={{
                  display: "flex",
                  justifyContent: "center",
                  gap: "1rem",
                  marginTop: "1rem"
                }}>
                  <button
                    // onClick={() => {
                    //   localStorage.removeItem("tableNumber");
                    //   localStorage.removeItem("locationId");
                    //   window.location.reload();
                    //   handleJoinTable
                    // }}
                    onClick={() => {
                      const locationId = localStorage.getItem("locationId");
                      const tableNum = localStorage.getItem("tableNumber");
                      
                      if (locationId && tableNum) {
                        router.push(`/join?location=${locationId}&table=${tableNum}`);
                      }
                    }}
                    style={{
                      backgroundColor: "#388e3c",
                      color: "white",
                      border: "none",
                      borderRadius: "8px",
                      padding: "8px 16px",
                      cursor: "pointer",
                    }}
                  >
                    Join Session
                  </button>
                  <button
                    onClick={() => {
                      // localStorage.removeItem("tableNumber");
                      // localStorage.removeItem("locationId");
                      window.location.reload();
                    }}
                    style={{
                      backgroundColor: "#f57c00",
                      color: "white",
                      border: "none",
                      borderRadius: "8px",
                      padding: "8px 16px",
                      cursor: "pointer",
                    }}
                  >
                    Change Table
                  </button>
                </div>


              </>
            ) : (
              <>
                <h3>Join a Table</h3>
                
                {/* Location Picker */}
                <select
                  value={selectedLocation}
                  onChange={(e) => setSelectedLocation(e.target.value)}
                  disabled={loading || locations.length === 0}
                  style={{
                    padding: "10px",
                    borderRadius: "6px",
                    border: "1px solid #ccc",
                    marginRight: "10px",
                    marginBottom: "10px",
                    width: "100%",
                    maxWidth: "300px",
                  }}
                >
                  <option value="">Select Location</option>
                  {locations.map((location) => (
                    <option key={location.location_Id} value={location.location_Id}>
                      {location.name || location.address || `Location ${location.location_number}`}
                    </option>
                  ))}
                </select>

                {/* Table Number Input */}
                <div style={{ display: "flex", alignItems: "center", gap: "10px", justifyContent: "center" }}>
                  <input
                    type="number"
                    placeholder="Table #"
                    value={tableNumber}
                    onChange={(e) => setTableNumber(e.target.value)}
                    disabled={!selectedLocation}
                    style={{
                      padding: "10px",
                      borderRadius: "6px",
                      border: "1px solid #ccc",
                      flex: 1,
                      maxWidth: "200px",
                    }}
                  />
                  <button
                    onClick={handleJoinTable}
                    disabled={!selectedLocation || !tableNumber.trim()}
                    style={{
                      backgroundColor: selectedLocation && tableNumber.trim() ? "#388e3c" : "#ccc",
                      color: "white",
                      border: "none",
                      borderRadius: "8px",
                      padding: "10px 22px",
                      cursor: selectedLocation && tableNumber.trim() ? "pointer" : "not-allowed",
                    }}
                  >
                    Join Table
                  </button>
                </div>
              </>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default HomePage;