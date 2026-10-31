const PASTEL_MIN = 120; // lowest value a channel can have
const PASTEL_SPREAD = 105; // channels start between 120 and 224
const CHANNEL_DIP = 30; // how far one channel is lowered so the colour has a tint

// First try to find colours that are clearly different (75 apart)
const STRICT_DISTANCE = 75;
const STRICT_ATTEMPTS = 1000;
// And if that can't fill the set, accept colours that are a closer (50 apart)
const LOOSE_DISTANCE = 50;
const LOOSE_ATTEMPTS = 1000;

export const hexToRgb = (hex) => {
  const red = parseInt(hex.slice(1, 3), 16);
  const green = parseInt(hex.slice(3, 5), 16);
  const blue = parseInt(hex.slice(5, 7), 16);
  return [red, green, blue];
};

const toHexPair = (value) => value.toString(16).padStart(2, "0");

export const rgbToHex = (rgb) => {
  const [red, green, blue] = rgb;
  return `#${toHexPair(red)}${toHexPair(green)}${toHexPair(blue)}`;
};

export const colorDistance = (hexA, hexB) => {
  const [r1, g1, b1] = hexToRgb(hexA);
  const [r2, g2, b2] = hexToRgb(hexB);
  return Math.abs(r1 - r2) + Math.abs(g1 - g2) + Math.abs(b1 - b2);
};

const randomChannel = (random) =>
  Math.floor(random() * PASTEL_SPREAD) + PASTEL_MIN;

export const generatePastelColor = (random = Math.random) => {
  const rgb = [
    randomChannel(random),
    randomChannel(random),
    randomChannel(random),
  ];

  const dipped = Math.floor(random() * 3);
  rgb[dipped] = Math.max(PASTEL_MIN, rgb[dipped] - CHANNEL_DIP);

  return rgbToHex(rgb);
};

const isTooClose = (color, others, minDistance) =>
  others.some((other) => colorDistance(color, other) < minDistance);

export const generateColorSet = (
  count,
  existingColors = [],
  random = Math.random,
) => {
  const existing = existingColors.filter(Boolean);
  const picked = [];

  const addColors = (minDistance, attempts) => {
    for (let i = 0; i < attempts && picked.length < count; i++) {
      const candidate = generatePastelColor(random);
      if (!isTooClose(candidate, [...existing, ...picked], minDistance)) {
        picked.push(candidate);
      }
    }
  };

  addColors(STRICT_DISTANCE, STRICT_ATTEMPTS);
  addColors(LOOSE_DISTANCE, LOOSE_ATTEMPTS);

  return picked;
};
