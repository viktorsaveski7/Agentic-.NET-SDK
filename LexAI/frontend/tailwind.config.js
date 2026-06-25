/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        bg: "#0F1117",
        surface: "#1A1D27",
        border: "#2A2D3E",
        accent: "#6366F1",
        "accent-hover": "#818CF8",
        "text-primary": "#F1F5F9",
        "text-secondary": "#94A3B8",
        high: "#10B981",
        medium: "#F59E0B",
        low: "#EF4444",
      },
      fontFamily: {
        sans: ["Inter", "system-ui", "sans-serif"],
        mono: ["'JetBrains Mono'", "monospace"],
      },
      keyframes: {
        "pulse-gradient": {
          "0%, 100%": { opacity: "0.35", transform: "translateX(-10%)" },
          "50%": { opacity: "0.7", transform: "translateX(10%)" },
        },
      },
      animation: {
        "pulse-gradient": "pulse-gradient 8s ease-in-out infinite",
      },
    },
  },
  plugins: [],
};
