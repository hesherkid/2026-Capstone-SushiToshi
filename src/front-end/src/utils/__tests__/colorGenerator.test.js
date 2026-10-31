import {
  hexToRgb,
  rgbToHex,
  colorDistance,
  generatePastelColor,
  generateColorSet,
} from "../colorGenerator";

// Small seeded random number generator (mulberry32), so results repeat on every run
const seeded = (seed) => () => {
  seed = (seed + 0x6d2b79f5) | 0;
  let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
};

// Same regex as TagBaseDTO.Tag_Color
const BACKEND_HEX = /^#[0-9A-Fa-f]{6}$/;

describe("hexToRgb / rgbToHex", () => {
  it("converts both ways", () => {
    expect(hexToRgb("#ff8000")).toEqual([255, 128, 0]);
    expect(rgbToHex([255, 128, 0])).toBe("#ff8000");
  });
});

describe("colorDistance", () => {
  it("is 0 for the same colour", () => {
    expect(colorDistance("#abcdef", "#abcdef")).toBe(0);
  });

  it("is 765 for black and white", () => {
    expect(colorDistance("#000000", "#ffffff")).toBe(765);
  });
});

describe("generatePastelColor", () => {
  it("gives the lowest pastel when random returns 0", () => {
    expect(generatePastelColor(() => 0)).toBe("#787878");
  });

  it("lowers the blue channel when random is near 1", () => {
    expect(generatePastelColor(() => 0.999)).toBe("#e0e0c2");
  });

  it("keeps every channel between 120 and 224 and passes the DTO regex", () => {
    const random = seeded(1);
    for (let i = 0; i < 500; i++) {
      const color = generatePastelColor(random);
      expect(color).toMatch(BACKEND_HEX);
      hexToRgb(color).forEach((channel) => {
        expect(channel).toBeGreaterThanOrEqual(120);
        expect(channel).toBeLessThanOrEqual(224);
      });
    }
  });
});

describe("generateColorSet", () => {
  it("returns the requested count when there is room", () => {
    expect(generateColorSet(15, [], seeded(42))).toHaveLength(15);
  });

  it("returns an empty list for a count of 0", () => {
    expect(generateColorSet(0, [], seeded(42))).toEqual([]);
  });

  it("keeps new colours at least 50 apart from each other and from existing ones", () => {
    const existing = ["#a0c0e0", "#e0a0c0"];
    const colors = generateColorSet(15, existing, seeded(7));
    const all = [...existing, ...colors];
    all.forEach((a, i) =>
      all.slice(i + 1).forEach((b) => {
        expect(colorDistance(a, b)).toBeGreaterThanOrEqual(50);
      }),
    );
  });

  it("never returns a colour that already exists", () => {
    const existing = generateColorSet(10, [], seeded(3));
    const colors = generateColorSet(15, existing, seeded(3));
    colors.forEach((color) => expect(existing).not.toContain(color));
  });

  it("stops and returns fewer colours when every candidate is the same", () => {
    expect(generateColorSet(15, [], () => 0.5)).toHaveLength(1);
  });

  it("ignores empty or missing existing colours", () => {
    const colors = generateColorSet(5, ["", null, undefined], seeded(9));
    expect(colors).toHaveLength(5);
  });

  it("gives the same output for the same seed", () => {
    expect(generateColorSet(15, [], seeded(11))).toEqual(
      generateColorSet(15, [], seeded(11)),
    );
  });
});
