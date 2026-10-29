/** @type {import('tailwindcss').Config} */

// Turns a `--color-*` CSS variable (stored as an "R G B" triplet, e.g.
// "--color-brand-500: 90 117 255;") into a Tailwind color function that
// still supports opacity modifiers (bg-brand-500/40, text-white/80, etc).
// This is what lets every existing utility class in the app respond to
// the `data-theme` attribute set by ThemeSwitcher, with zero changes to
// the class names used throughout the codebase.
function withOpacity(variableName) {
  return ({ opacityValue }) =>
    opacityValue !== undefined ? `rgb(var(${variableName}) / ${opacityValue})` : `rgb(var(${variableName}))`;
}

export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        // Overrides Tailwind's built-in pure-white so translucent panels/
        // borders (bg-white/[0.03], border-white/10) and heading text
        // (text-white) invert correctly in the light theme. Spots that
        // must stay literally white regardless of theme (e.g. button
        // labels on brand gradients) use text-[#fff] instead.
        white: withOpacity('--color-ink'),
        slate: {
          50: withOpacity('--color-slate-50'),
          100: withOpacity('--color-slate-100'),
          200: withOpacity('--color-slate-200'),
          300: withOpacity('--color-slate-300'),
          400: withOpacity('--color-slate-400'),
          500: withOpacity('--color-slate-500'),
          600: withOpacity('--color-slate-600'),
          700: withOpacity('--color-slate-700'),
          800: withOpacity('--color-slate-800'),
          900: withOpacity('--color-slate-900'),
          950: withOpacity('--color-slate-950'),
        },
        brand: {
          50: withOpacity('--color-brand-50'),
          100: withOpacity('--color-brand-100'),
          200: withOpacity('--color-brand-200'),
          300: withOpacity('--color-brand-300'),
          400: withOpacity('--color-brand-400'),
          500: withOpacity('--color-brand-500'),
          600: withOpacity('--color-brand-600'),
          700: withOpacity('--color-brand-700'),
          800: withOpacity('--color-brand-800'),
          900: withOpacity('--color-brand-900'),
          950: withOpacity('--color-brand-950'),
        },
        accent: {
          400: withOpacity('--color-accent-400'),
          500: withOpacity('--color-accent-500'),
        },
      },
      fontFamily: {
        sans: [
          "-apple-system",
          "BlinkMacSystemFont",
          '"Segoe UI"',
          "Inter",
          "Roboto",
          '"Helvetica Neue"',
          "Arial",
          "sans-serif",
        ],
      },
      spacing: {
        18: "4.5rem",
        22: "5.5rem",
      },
      borderRadius: {
        "4xl": "2rem",
      },
      backgroundImage: {
        "mesh-gradient":
          "radial-gradient(at 20% 20%, rgba(90,117,255,0.35) 0px, transparent 50%), radial-gradient(at 80% 0%, rgba(236,72,153,0.25) 0px, transparent 50%), radial-gradient(at 0% 80%, rgba(90,117,255,0.2) 0px, transparent 50%), radial-gradient(at 80% 90%, rgba(124,58,237,0.25) 0px, transparent 50%)",
      },
      keyframes: {
        float: {
          "0%, 100%": { transform: "translateY(0px)" },
          "50%": { transform: "translateY(-10px)" },
        },
        "fade-in": {
          "0%": { opacity: "0", transform: "translateY(8px)" },
          "100%": { opacity: "1", transform: "translateY(0)" },
        },
        shimmer: {
          "0%": { backgroundPosition: "-200% 0" },
          "100%": { backgroundPosition: "200% 0" },
        },
      },
      animation: {
        float: "float 6s ease-in-out infinite",
        "fade-in": "fade-in 0.5s ease-out both",
        shimmer: "shimmer 2.5s linear infinite",
      },
    },
  },
  plugins: [
    require("@tailwindcss/forms"),
    require("@tailwindcss/typography"),
  ],
}
