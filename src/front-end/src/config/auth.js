// config/auth.js
/**
 * Custom claims namespace for roles (keep for backend compatibility)
 */
export const ROLES_CLAIM = 'roles';
export const ROLE_CLAIM_TYPE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'; // .NET ClaimTypes.Role

/**
 * Extract roles from JWT token claims
 * @param {object} tokenClaims - The decoded JWT token claims
 * @returns {array} - Array of role strings
 */
export function extractRoles(tokenClaims) {
    if (!tokenClaims) return [];
    
    // Check multiple possible role claim locations
    const roles = tokenClaims.roles || 
                  tokenClaims[ROLES_CLAIM] || 
                  tokenClaims[ROLE_CLAIM_TYPE] || // ✅ .NET ClaimTypes.Role
                  tokenClaims.role || // ✅ singular 'role'
                  [];
    
    return Array.isArray(roles) ? roles : [roles];
}


/**
 * Check if user has any of the specified roles
 * @param {object} tokenClaims - The decoded JWT token claims or user object
 * @param {array} rolesToCheck - Array of role strings to check
 * @returns {boolean} - True if user has any of the roles
 */
export function userHasAnyRole(tokenClaims, rolesToCheck) {
    if (!tokenClaims) {
        return false;
    }
    
    if (typeof tokenClaims !== 'object' || !Array.isArray(rolesToCheck)) {
        console.error("Invalid arguments: tokenClaims must be an object and rolesToCheck must be an array.");
        return false;
    }

    const roles = extractRoles(tokenClaims);
    
    if (!Array.isArray(roles)) {
        console.error("Invalid token: 'roles' claim must be an array.");
        return false;
    }

    return rolesToCheck.some(role => roles.includes(role));
}

/**
 * Check if user has a specific role
 * @param {object} tokenClaims - The decoded JWT token claims
 * @param {string} role - Role string to check
 * @returns {boolean} - True if user has the role
 */
export function userHasRole(tokenClaims, role) {
    return userHasAnyRole(tokenClaims, [role]);
}

/**
 * Decode JWT token to get claims
 * @param {string} token - JWT token string
 * @returns {object} - Decoded token claims
 */
export function decodeToken(token) {
    try {
        const base64Url = token.split('.')[1];
        const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
        const jsonPayload = decodeURIComponent(
            atob(base64)
                .split('')
                .map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
                .join('')
        );
        return JSON.parse(jsonPayload);
    } catch (error) {
        console.error('Error decoding token:', error);
        return null;
    }
}

/**
 * Get user roles from token
 * @param {string} accessToken - JWT access token
 * @returns {array} - Array of role strings
 */
export function getUserRoles(accessToken) {
    if (!accessToken) return [];
    
    const tokenClaims = decodeToken(accessToken);
    return extractRoles(tokenClaims);
}

/**
 * Map roles to app-specific roles
 * @param {array} roles - Array of role strings
 * @returns {string} - Primary app role
 */
export function mapToAppRole(roles) {
    if (!Array.isArray(roles)) return 'customer';
    
    if (roles.includes('Admin') || roles.includes('admin')) return 'admin';
    if (roles.includes('Staff') || roles.includes('staff')) return 'staff';
    if (roles.includes('Customer') || roles.includes('customer')) return 'customer';
    
    return 'customer'; // Default role
}

/**
 * Get current user from stored token
 * @returns {object|null} - Decoded user claims or null
 */
export function getCurrentUserFromToken() {
    if (typeof window === 'undefined') return null;
    
    const token = localStorage.getItem('authToken');
    if (!token) return null;
    
    return decodeToken(token);
}

/**
 * Check if token is expired
 * @param {string} token - JWT token string
 * @returns {boolean} - True if token is expired
 */
export function isTokenExpired(token) {
    if (!token) return true;
    
    const claims = decodeToken(token);
    if (!claims || !claims.exp) return true;
    
    // exp is in seconds, Date.now() is in milliseconds
    return claims.exp * 1000 < Date.now();
}
