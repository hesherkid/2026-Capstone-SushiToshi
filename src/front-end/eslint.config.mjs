// For more info, see https://github.com/storybookjs/eslint-plugin-storybook#configuration-flat-config-format
import storybook from "eslint-plugin-storybook";

import nextVitals from "eslint-config-next/core-web-vitals";

const eslintConfig = [{
  // On its own so it applies to every file, not just the rules below
  ignores: [
    "node_modules/**",
    ".next/**",
    "out/**",
    "build/**",
    "coverage/**",
    "next-env.d.ts",
  ],
}, ...nextVitals, {
  rules: {
    "react-hooks/exhaustive-deps": "off",
    "react-hooks/set-state-in-effect": "off",
  },
}, ...storybook.configs["flat/recommended"]];

export default eslintConfig;
