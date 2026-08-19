/** @type {import('next').NextConfig} */
const nextConfig = {
  // Base path for production deployment behind nginx proxy
  basePath: process.env.NEXT_PUBLIC_BASE_PATH || '/metronic/starter-kits/nextjs',

  // Asset prefix for static assets
  assetPrefix: process.env.NEXT_PUBLIC_BASE_PATH || '/metronic/starter-kits/nextjs',

  // Standalone output for Docker deployment
  output: 'standalone',
};

export default nextConfig;
