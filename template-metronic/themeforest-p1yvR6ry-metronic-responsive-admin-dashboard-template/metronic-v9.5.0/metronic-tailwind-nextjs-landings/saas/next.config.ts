/** @type {import('next').NextConfig} */
const nextConfig = {
	// For local development, basePath is '/'
	// This file will be overwritten during deployment with the appropriate basePath
	images: {
		remotePatterns: [
      {
        protocol: "https",
        hostname: "images.unsplash.com",
        port: "",
        pathname: "/**",
      },
    ],
	},
};

export default nextConfig;
