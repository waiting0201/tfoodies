// Typed design tokens mirroring tokens.css, for use in TS (theme objects, charts, etc.).
// Source of truth: reference/old/tfoodies/Content/styles/main.css.

export const colors = {
  primary: "#26b7bc",
  primaryDark: "#156467",
  primaryDarker: "#007382",
  primaryAlt: "#1d8e92",
  accent: "#ea5520",
  gold: "#eda02f",
  goldDark: "#db8c0a",
  olive: "#95ad25",
  danger: "#d0021b",
  text: "#393939",
  textSoft: "#3e3e3e",
  muted: "#9b9b9b",
  border: "#e1e1e1",
  borderSoft: "#e6e6e6",
  bgSoft: "#f2f2f2",
  white: "#ffffff",
} as const;

export const fonts = {
  body: `"Open Sans", "Noto Sans TC", "Helvetica Neue", Arial, "微軟正黑體", sans-serif`,
  heading: `"GFS Didot", Didot, serif`,
} as const;

export const breakpoints = {
  sm: 576,
  md: 768,
  lg: 992,
  xl: 1200,
} as const;

export type Colors = typeof colors;
