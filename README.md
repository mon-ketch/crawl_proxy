
// GUIDE TO CALCULATE THE NUMBER OF IPs IN A RANGE

// Example: IP range from 14.160.0.0 → 14.162.5.255

// Formula:

// Number of IPs = (A2 - A1) * 256^3

//                + (B2 - B1) * 256^2

//                + (C2 - C1) * 256^1

//                + (D2 - D1) * 256^0

// Then add 1 because the first IP is also counted.

// Example:

// Start IP: 14.160.0.0

// End IP:   14.162.5.255

// A: 14 → 14 → (14-14)*256^3 = 0

// B: 162-160 → 2*256^2 = 131072

// C: 5-0 → 5*256 = 1280

// D: 255-0 → 255*1 = 255


// Total number of IPs = 0 + 131072 + 1280 + 255 + 1 = 132608 IPs

