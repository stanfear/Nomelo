import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: path.resolve(__dirname, "../Nomelo.Server/wwwroot"),
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      "/api": "http://localhost:5000",
      "/login": "http://localhost:5000",
      "/logout": "http://localhost:5000",
      "/signin-oidc": "http://localhost:5000",
      "/signout-callback-oidc": "http://localhost:5000",
    },
  },
});
